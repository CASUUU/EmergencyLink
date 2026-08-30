using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace EmergencyLink
{
    public sealed class EmergencyServer : IDisposable
    {
        private sealed class ClientSession
        {
            public string Id;
            public string Name;
            public string Role;
            public TcpClient TcpClient;
            public StreamReader Reader;
            public StreamWriter Writer;
            public bool Joined;
            public DateTime LastSeen;
            public readonly object SendLock = new object();

            public void Send(Dictionary<string, string> fields)
            {
                lock (SendLock)
                {
                    if (Writer == null) return;
                    Writer.WriteLine(Protocol.Encode(fields));
                }
            }
        }

        private readonly object _sync = new object();
        private readonly List<ClientSession> _clients = new List<ClientSession>();
        private readonly List<AlertBatch> _batches = new List<AlertBatch>();
        private TcpListener _listener;
        private Thread _acceptThread;
        private bool _running;
        private AppConfig _config;
        private int _usedOfficialCalls;
        private int _overLimitApprovals;

        public event Action<string> LogCreated;
        public string Phase;
        public int ActualPort;

        public EmergencyServer()
        {
            Phase = PhaseNames.Preparation;
            _config = AppConfig.CreateDefault();
        }

        public void Start(AppConfig config)
        {
            Stop();

            _config = config.Clone();
            if (_config.Port < 0) _config.Port = 5050;
            if (_config.MaxOfficialCalls < 0) _config.MaxOfficialCalls = 0;
            if (_config.BatchWindowSeconds < 5) _config.BatchWindowSeconds = 5;

            _listener = new TcpListener(IPAddress.Any, _config.Port);
            _listener.Start();
            ActualPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;

            _acceptThread = new Thread(AcceptLoop);
            _acceptThread.IsBackground = true;
            _acceptThread.Start();

            Log("服务器已启动，房间 " + _config.RoomName + "，端口 " + ActualPort.ToString());
        }

        public void Stop()
        {
            _running = false;
            try
            {
                if (_listener != null) _listener.Stop();
            }
            catch
            {
            }

            lock (_sync)
            {
                for (int i = 0; i < _clients.Count; i++)
                {
                    try { _clients[i].TcpClient.Close(); }
                    catch { }
                }
                _clients.Clear();
            }

            _listener = null;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient tcp = _listener.AcceptTcpClient();
                    ClientSession session = new ClientSession();
                    session.Id = Guid.NewGuid().ToString("N");
                    session.TcpClient = tcp;
                    session.LastSeen = DateTime.Now;
                    NetworkStream stream = tcp.GetStream();
                    session.Reader = new StreamReader(stream, Encoding.UTF8);
                    session.Writer = new StreamWriter(stream, new UTF8Encoding(false));
                    session.Writer.AutoFlush = true;

                    Thread thread = new Thread(delegate() { ClientLoop(session); });
                    thread.IsBackground = true;
                    thread.Start();
                }
                catch (SocketException)
                {
                    if (_running) Log("服务器监听异常");
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private void ClientLoop(ClientSession session)
        {
            try
            {
                while (_running)
                {
                    string line = session.Reader.ReadLine();
                    if (line == null) break;
                    Dictionary<string, string> fields = Protocol.Decode(line);
                    ProcessMessage(session, fields);
                }
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                bool hadJoined = false;
                string displayName = "";
                lock (_sync)
                {
                    hadJoined = session.Joined;
                    displayName = session.Name;
                    _clients.Remove(session);
                }

                try { session.TcpClient.Close(); }
                catch { }

                if (hadJoined) Log(displayName + " 已离线");
                BroadcastState();
            }
        }

        private void ProcessMessage(ClientSession session, Dictionary<string, string> fields)
        {
            string command = Protocol.Get(fields, "cmd");
            session.LastSeen = DateTime.Now;

            if (command == "join")
            {
                HandleJoin(session, fields);
                return;
            }

            if (!session.Joined)
            {
                SendError(session, "尚未加入房间");
                return;
            }

            if (command == "alert") HandleAlert(session, fields);
            else if (command == "delivered") HandleDelivered(session, fields);
            else if (command == "ack") HandleAck(session, fields);
            else if (command == "approve") HandleApprove(session, fields);
            else if (command == "close_batch") HandleCloseBatch(session, fields);
            else if (command == "phase") HandlePhase(session, fields);
            else if (command == "config") HandleConfig(session, fields);
            else if (command == "ping") BroadcastState();
        }

        private void HandleJoin(ClientSession session, Dictionary<string, string> fields)
        {
            string room = Protocol.Get(fields, "room").Trim();
            string password = Protocol.Get(fields, "password");
            string name = Protocol.Get(fields, "name").Trim();
            string role = Protocol.Get(fields, "role").Trim();

            if (String.IsNullOrEmpty(name)) name = "未命名成员";
            if (String.IsNullOrEmpty(role)) role = RoleNames.Teammate;

            lock (_sync)
            {
                if (!String.Equals(room, _config.RoomName, StringComparison.OrdinalIgnoreCase) || password != _config.Password)
                {
                    SendError(session, "房间名或密码不正确。当前服务器房间：" + _config.RoomName);
                    try { session.TcpClient.Close(); }
                    catch { }
                    return;
                }

                session.Name = MakeUniqueName(name);
                session.Role = role;
                session.Joined = true;
                _clients.Add(session);
            }

            Dictionary<string, string> welcome = new Dictionary<string, string>();
            welcome["cmd"] = "welcome";
            welcome["id"] = session.Id;
            welcome["name"] = session.Name;
            welcome["role"] = session.Role;
            session.Send(welcome);

            Log(RoleNames.Display(session.Role) + " " + session.Name + " 已加入房间");
            BroadcastState();
        }

        private string MakeUniqueName(string requested)
        {
            string name = requested;
            int suffix = 2;
            bool exists = true;
            while (exists)
            {
                exists = false;
                for (int i = 0; i < _clients.Count; i++)
                {
                    if (_clients[i].Joined && String.Equals(_clients[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    name = requested + "-" + suffix.ToString();
                    suffix++;
                }
            }
            return name;
        }

        private void HandleAlert(ClientSession session, Dictionary<string, string> fields)
        {
            string type = Protocol.Get(fields, "type");
            string target = Protocol.Get(fields, "target").Trim();

            if (session.Role != RoleNames.Teammate && session.Role != RoleNames.Manager)
            {
                SendError(session, "只有队友或管理者可以发起提醒");
                return;
            }
            if (String.IsNullOrEmpty(target))
            {
                SendError(session, "请选择目标选手");
                return;
            }
            if (type == AlertTypes.Test && Phase != PhaseNames.PreMatchTest)
            {
                SendError(session, "只有赛前测试阶段可以发送测试提醒");
                return;
            }
            if (type == AlertTypes.Official && Phase != PhaseNames.InMatch)
            {
                SendError(session, "只有比赛中可以发送正式告警");
                return;
            }
            if (type != AlertTypes.Test && type != AlertTypes.Official)
            {
                SendError(session, "未知提醒类型");
                return;
            }

            AlertBatch batch;
            bool merged;
            lock (_sync)
            {
                batch = FindMergeableBatch(type, target);
                merged = batch != null;
                if (batch == null)
                {
                    batch = new AlertBatch();
                    batch.Id = DateTime.Now.ToString("HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
                    batch.Type = type;
                    batch.Target = target;
                    batch.Status = BatchStatus.Active;
                    batch.CreatedAt = DateTime.Now;
                    batch.UpdatedAt = batch.CreatedAt;
                    batch.IsOverLimit = type == AlertTypes.Official && GetRemainingCallsUnsafe() <= 0;
                    _batches.Add(batch);
                }

                batch.Count++;
                batch.UpdatedAt = DateTime.Now;
                if (!batch.Initiators.Contains(session.Name)) batch.Initiators.Add(session.Name);
            }

            string prefix = merged ? "合并提醒" : "新提醒";
            Log(prefix + "：" + session.Name + " -> " + target + "，类型 " + AlertTypes.Display(type));
            BroadcastAlert(batch);
            BroadcastState();
        }

        private AlertBatch FindMergeableBatch(string type, string target)
        {
            DateTime now = DateTime.Now;
            for (int i = _batches.Count - 1; i >= 0; i--)
            {
                AlertBatch batch = _batches[i];
                if (batch.Type == type &&
                    batch.Target == target &&
                    batch.Status == BatchStatus.Active &&
                    (now - batch.UpdatedAt).TotalSeconds <= _config.BatchWindowSeconds)
                {
                    return batch;
                }
            }
            return null;
        }

        private void HandleDelivered(ClientSession session, Dictionary<string, string> fields)
        {
            string batchId = Protocol.Get(fields, "batch");
            AlertBatch batch = FindBatch(batchId);
            if (batch == null) return;

            lock (_sync)
            {
                batch.DeliveredToPlayer = true;
                batch.DeliveredBy = session.Name;
                batch.DeliveredAt = DateTime.Now;
            }
            Log("选手端已收到提醒：" + session.Name + "，批次 " + batch.Id);
            BroadcastState();
        }

        private void HandleAck(ClientSession session, Dictionary<string, string> fields)
        {
            string batchId = Protocol.Get(fields, "batch");
            AlertBatch batch = FindBatch(batchId);
            if (batch == null)
            {
                SendError(session, "未找到提醒批次");
                return;
            }
            if (session.Role != RoleNames.Player && session.Role != RoleNames.Manager)
            {
                SendError(session, "只有目标选手可以发送回执");
                return;
            }
            if (session.Role == RoleNames.Player && !String.Equals(batch.Target, session.Name, StringComparison.OrdinalIgnoreCase))
            {
                SendError(session, "该提醒不是发给当前选手");
                return;
            }

            lock (_sync)
            {
                batch.AckBy = session.Name;
                batch.AckAt = DateTime.Now;
            }
            Log("选手回执：" + session.Name + " 已收到批次 " + batch.Id);
            BroadcastState();
        }

        private void HandleApprove(ClientSession session, Dictionary<string, string> fields)
        {
            if (!RoleNames.CanApprove(session.Role))
            {
                SendError(session, "只有主办方或管理者可以同意请求");
                return;
            }

            string batchId = Protocol.Get(fields, "batch");
            AlertBatch batch = FindBatch(batchId);
            if (batch == null)
            {
                SendError(session, "未找到提醒批次");
                return;
            }
            if (batch.Type != AlertTypes.Official)
            {
                SendError(session, "测试提醒无需审批");
                return;
            }

            bool overLimitApproval = false;
            lock (_sync)
            {
                if (batch.Status == BatchStatus.Approved)
                {
                    SendError(session, "该请求已经同意过，未重复扣减");
                    return;
                }

                overLimitApproval = GetRemainingCallsUnsafe() <= 0;
                batch.IsOverLimit = batch.IsOverLimit || overLimitApproval;
                batch.Status = BatchStatus.Approved;
                batch.ApprovedBy = session.Name;
                batch.ApprovedByRole = session.Role;
                batch.ApprovedAt = DateTime.Now;

                if (overLimitApproval) _overLimitApprovals++;
                else _usedOfficialCalls++;
            }

            if (session.Role == RoleNames.Manager)
            {
                Log("管理者代审批同意：" + session.Name + "，批次 " + batch.Id + (overLimitApproval ? "，超额批准" : ""));
            }
            else
            {
                Log("主办方同意：" + session.Name + "，批次 " + batch.Id + (overLimitApproval ? "，超额批准" : ""));
            }
            BroadcastState();
        }

        private void HandleCloseBatch(ClientSession session, Dictionary<string, string> fields)
        {
            if (!RoleNames.CanApprove(session.Role))
            {
                SendError(session, "只有主办方或管理者可以关闭请求");
                return;
            }
            string batchId = Protocol.Get(fields, "batch");
            AlertBatch batch = FindBatch(batchId);
            if (batch == null) return;
            lock (_sync)
            {
                if (batch.Status != BatchStatus.Approved) batch.Status = BatchStatus.Closed;
            }
            Log(session.Name + " 关闭批次 " + batch.Id);
            BroadcastState();
        }

        private void HandlePhase(ClientSession session, Dictionary<string, string> fields)
        {
            if (!RoleNames.CanManageRoom(session.Role))
            {
                SendError(session, "只有主办方或管理者可以切换比赛阶段");
                return;
            }
            string phase = Protocol.Get(fields, "phase");
            if (phase != PhaseNames.Preparation && phase != PhaseNames.PreMatchTest &&
                phase != PhaseNames.InMatch && phase != PhaseNames.Ended)
            {
                SendError(session, "未知比赛阶段");
                return;
            }
            Phase = phase;
            Log(session.Name + " 切换比赛阶段为 " + PhaseNames.Display(phase));
            BroadcastState();
        }

        private void HandleConfig(ClientSession session, Dictionary<string, string> fields)
        {
            if (!RoleNames.CanManageRoom(session.Role))
            {
                SendError(session, "只有主办方或管理者可以调整配置");
                return;
            }
            int maxCalls = Protocol.GetInt(fields, "maxCalls", _config.MaxOfficialCalls);
            int batchSeconds = Protocol.GetInt(fields, "batchSeconds", _config.BatchWindowSeconds);
            if (maxCalls < 0) maxCalls = 0;
            if (batchSeconds < 5) batchSeconds = 5;
            if (batchSeconds > 300) batchSeconds = 300;

            lock (_sync)
            {
                _config.MaxOfficialCalls = maxCalls;
                _config.BatchWindowSeconds = batchSeconds;
            }

            Log(session.Name + " 更新配置：连麦次数 " + maxCalls.ToString() + "，合并时间 " + batchSeconds.ToString() + " 秒");
            BroadcastState();
        }

        private AlertBatch FindBatch(string id)
        {
            lock (_sync)
            {
                for (int i = 0; i < _batches.Count; i++)
                {
                    if (_batches[i].Id == id) return _batches[i];
                }
            }
            return null;
        }

        private int GetRemainingCallsUnsafe()
        {
            int remaining = _config.MaxOfficialCalls - _usedOfficialCalls;
            if (remaining < 0) remaining = 0;
            return remaining;
        }

        private void BroadcastAlert(AlertBatch batch)
        {
            Dictionary<string, string> message = new Dictionary<string, string>();
            message["cmd"] = "alert";
            message["batch"] = batch.Id;
            message["type"] = batch.Type;
            message["target"] = batch.Target;
            message["count"] = batch.Count.ToString();
            message["overLimit"] = batch.IsOverLimit ? "1" : "0";
            Broadcast(message);
        }

        private void BroadcastState()
        {
            Dictionary<string, string> message = BuildStateMessage();
            Broadcast(message);
        }

        private Dictionary<string, string> BuildStateMessage()
        {
            Dictionary<string, string> message = new Dictionary<string, string>();
            StringBuilder members = new StringBuilder();
            List<string> players = new List<string>();
            List<string> batchRecords = new List<string>();

            lock (_sync)
            {
                message["cmd"] = "state";
                message["phase"] = Phase;
                message["room"] = _config.RoomName;
                message["maxCalls"] = _config.MaxOfficialCalls.ToString();
                message["usedOfficial"] = _usedOfficialCalls.ToString();
                message["remaining"] = GetRemainingCallsUnsafe().ToString();
                message["overLimit"] = _overLimitApprovals.ToString();
                message["batchSeconds"] = _config.BatchWindowSeconds.ToString();

                for (int i = 0; i < _clients.Count; i++)
                {
                    ClientSession client = _clients[i];
                    if (!client.Joined) continue;
                    members.AppendLine(RoleNames.Display(client.Role) + " | " + client.Name + " | 在线");
                    if (client.Role == RoleNames.Player) players.Add(client.Name);
                }

                int start = _batches.Count - 25;
                if (start < 0) start = 0;
                for (int i = _batches.Count - 1; i >= start; i--)
                {
                    AlertBatch batch = _batches[i];
                    string record = Protocol.PackRecord(
                        batch.Id,
                        batch.Type,
                        batch.Target,
                        batch.Status,
                        batch.Count.ToString(),
                        batch.AckBy ?? "",
                        batch.ApprovedBy ?? "",
                        batch.IsOverLimit ? "1" : "0",
                        batch.UpdatedAt.ToString("HH:mm:ss"),
                        String.Join(",", batch.Initiators.ToArray()),
                        batch.DeliveredToPlayer ? "1" : "0"
                    );
                    batchRecords.Add(record);
                }
            }

            message["members"] = members.ToString();
            message["players"] = String.Join(Protocol.UnitSeparator, players.ToArray());
            message["batches"] = String.Join(Protocol.RecordSeparator, batchRecords.ToArray());
            return message;
        }

        private void Broadcast(Dictionary<string, string> message)
        {
            List<ClientSession> sessions;
            lock (_sync)
            {
                sessions = new List<ClientSession>(_clients);
            }

            for (int i = 0; i < sessions.Count; i++)
            {
                try
                {
                    if (sessions[i].Joined) sessions[i].Send(message);
                }
                catch
                {
                }
            }
        }

        private void SendError(ClientSession session, string text)
        {
            Dictionary<string, string> message = new Dictionary<string, string>();
            message["cmd"] = "error";
            message["message"] = text;
            try { session.Send(message); }
            catch { }
        }

        private void Log(string text)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + text;
            WriteAuditLog(line);

            Action<string> handler = LogCreated;
            if (handler != null) handler(line);

            Dictionary<string, string> message = new Dictionary<string, string>();
            message["cmd"] = "log";
            message["line"] = line;
            Broadcast(message);
        }

        private void WriteAuditLog(string line)
        {
            try
            {
                string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                Directory.CreateDirectory(root);
                string file = Path.Combine(root, "audit-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                File.AppendAllText(file, line + Environment.NewLine, new UTF8Encoding(true));
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
