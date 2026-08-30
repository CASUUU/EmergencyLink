using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EmergencyLink.Forms
{
    public sealed class MainForm : Form
    {
        private const int HotkeyId = 0x454C;
        private const int ModAlt = 0x0001;
        private const int ModControl = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private EmergencyServer _server;
        private LinkClient _client;
        private OverlayForm _overlay;
        private string _currentRole = "";
        private string _currentName = "";
        private string _currentPhase = PhaseNames.Preparation;
        private string _lastAlertBatch = "";
        private readonly List<AlertView> _currentBatches = new List<AlertView>();

        private TextBox _serverRoom;
        private TextBox _serverPassword;
        private NumericUpDown _serverPort;
        private NumericUpDown _serverMaxCalls;
        private NumericUpDown _serverBatchSeconds;
        private Button _startOrganizerServerButton;
        private Button _startManagerServerButton;
        private Button _stopServerButton;
        private Label _serverAddressLabel;

        private TextBox _connectHost;
        private NumericUpDown _connectPort;
        private TextBox _connectRoom;
        private TextBox _connectPassword;
        private TextBox _displayName;
        private ComboBox _roleCombo;
        private Button _connectButton;
        private Button _disconnectButton;
        private Label _connectionStatusLabel;

        private Label _phaseLabel;
        private Label _quotaLabel;
        private NumericUpDown _manageMaxCalls;
        private NumericUpDown _manageBatchSeconds;
        private Button _applyConfigButton;
        private Button _phasePreparationButton;
        private Button _phaseTestButton;
        private Button _phaseMatchButton;
        private Button _phaseEndedButton;
        private ListBox _batchList;
        private Button _approveButton;
        private Button _closeBatchButton;

        private Label _playerStatusLabel;
        private Button _ackButton;
        private Button _showOverlayButton;
        private Button _hideOverlayButton;

        private ComboBox _targetPlayerCombo;
        private Button _sendTestAlertButton;
        private Button _sendOfficialAlertButton;
        private Label _teammateStatusLabel;

        private TextBox _membersBox;
        private TextBox _logBox;

        public MainForm()
        {
            Text = "EmergencyLink";
            Font = new Font("Microsoft YaHei UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1060;
            Height = 760;
            MinimumSize = new Size(960, 680);

            _overlay = new OverlayForm();
            BuildUi();
            RefreshRoleUi();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt, (int)Keys.R);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(Handle, HotkeyId);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == HotkeyId)
            {
                SendAcknowledgement();
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_client != null) _client.Close();
            if (_server != null) _server.Stop();
            if (_overlay != null) _overlay.Close();
            base.OnFormClosing(e);
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            Panel top = new Panel();
            top.Dock = DockStyle.Fill;
            root.Controls.Add(top, 0, 0);

            GroupBox serverGroup = new GroupBox();
            serverGroup.Text = "本机服务器";
            serverGroup.SetBounds(12, 10, 505, 185);
            top.Controls.Add(serverGroup);

            _serverRoom = AddTextBox(serverGroup, "房间名", 20, 28, "match-room");
            _serverPassword = AddTextBox(serverGroup, "房间密码", 20, 64, "123456");
            _serverPort = AddNumber(serverGroup, "端口", 20, 100, 1, 65535, 5050);
            _serverMaxCalls = AddNumber(serverGroup, "连麦次数", 245, 28, 0, 99, 3);
            _serverBatchSeconds = AddNumber(serverGroup, "同批时间(秒)", 245, 64, 5, 300, 30);

            _startOrganizerServerButton = new Button();
            _startOrganizerServerButton.Text = "主办方电脑兼服务器";
            _startOrganizerServerButton.SetBounds(245, 100, 150, 30);
            _startOrganizerServerButton.Click += delegate { StartLocalServer(RoleNames.Organizer); };
            serverGroup.Controls.Add(_startOrganizerServerButton);

            _startManagerServerButton = new Button();
            _startManagerServerButton.Text = "管理者兜底服务器";
            _startManagerServerButton.SetBounds(400, 100, 95, 30);
            _startManagerServerButton.Click += delegate { StartLocalServer(RoleNames.Manager); };
            serverGroup.Controls.Add(_startManagerServerButton);

            _stopServerButton = new Button();
            _stopServerButton.Text = "停止服务器";
            _stopServerButton.SetBounds(20, 136, 100, 28);
            _stopServerButton.Click += delegate { StopLocalServer(); };
            serverGroup.Controls.Add(_stopServerButton);

            _serverAddressLabel = new Label();
            _serverAddressLabel.SetBounds(130, 134, 360, 44);
            _serverAddressLabel.Text = "未启动";
            serverGroup.Controls.Add(_serverAddressLabel);

            GroupBox connectGroup = new GroupBox();
            connectGroup.Text = "连接房间";
            connectGroup.SetBounds(530, 10, 505, 185);
            top.Controls.Add(connectGroup);

            _connectHost = AddTextBox(connectGroup, "服务器地址", 20, 28, "127.0.0.1");
            _connectPort = AddNumber(connectGroup, "端口", 20, 64, 1, 65535, 5050);
            _connectRoom = AddTextBox(connectGroup, "房间名", 20, 100, "match-room");
            _connectPassword = AddTextBox(connectGroup, "房间密码", 245, 28, "123456");
            _displayName = AddTextBox(connectGroup, "显示名称", 245, 64, Environment.MachineName);

            Label roleLabel = new Label();
            roleLabel.Text = "角色";
            roleLabel.SetBounds(245, 105, 78, 20);
            connectGroup.Controls.Add(roleLabel);
            _roleCombo = new ComboBox();
            _roleCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _roleCombo.Items.AddRange(new object[] { "主办方", "管理者", "选手-比赛", "选手-队友" });
            _roleCombo.SelectedIndex = 2;
            _roleCombo.SetBounds(326, 101, 150, 25);
            connectGroup.Controls.Add(_roleCombo);

            _connectButton = new Button();
            _connectButton.Text = "连接";
            _connectButton.SetBounds(20, 136, 90, 30);
            _connectButton.Click += delegate { ConnectAsSelectedRole(); };
            connectGroup.Controls.Add(_connectButton);

            _disconnectButton = new Button();
            _disconnectButton.Text = "断开";
            _disconnectButton.SetBounds(116, 136, 90, 30);
            _disconnectButton.Click += delegate { DisconnectClient(); };
            connectGroup.Controls.Add(_disconnectButton);

            _connectionStatusLabel = new Label();
            _connectionStatusLabel.Text = "未连接";
            _connectionStatusLabel.SetBounds(218, 141, 260, 24);
            connectGroup.Controls.Add(_connectionStatusLabel);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            root.Controls.Add(tabs, 0, 1);

            tabs.TabPages.Add(BuildManageTab());
            tabs.TabPages.Add(BuildPlayerTab());
            tabs.TabPages.Add(BuildTeammateTab());
            tabs.TabPages.Add(BuildStatusTab());
        }

        private TextBox AddTextBox(Control parent, string label, int x, int y, string value)
        {
            Label lab = new Label();
            lab.Text = label;
            lab.SetBounds(x, y + 4, 82, 22);
            parent.Controls.Add(lab);

            TextBox textBox = new TextBox();
            textBox.Text = value;
            textBox.SetBounds(x + 86, y, 130, 24);
            parent.Controls.Add(textBox);
            return textBox;
        }

        private NumericUpDown AddNumber(Control parent, string label, int x, int y, int min, int max, int value)
        {
            Label lab = new Label();
            lab.Text = label;
            lab.SetBounds(x, y + 4, 88, 22);
            parent.Controls.Add(lab);

            NumericUpDown number = new NumericUpDown();
            number.Minimum = min;
            number.Maximum = max;
            number.Value = value;
            number.SetBounds(x + 92, y, 80, 24);
            parent.Controls.Add(number);
            return number;
        }

        private TabPage BuildManageTab()
        {
            TabPage tab = new TabPage("管理/审批");

            _phaseLabel = new Label();
            _phaseLabel.Font = new Font(Font, FontStyle.Bold);
            _phaseLabel.SetBounds(20, 18, 220, 24);
            _phaseLabel.Text = "阶段：准备中";
            tab.Controls.Add(_phaseLabel);

            _quotaLabel = new Label();
            _quotaLabel.Font = new Font(Font, FontStyle.Bold);
            _quotaLabel.SetBounds(260, 18, 360, 24);
            _quotaLabel.Text = "连麦额度：3，已用：0，剩余：3，超额：0";
            tab.Controls.Add(_quotaLabel);

            _manageMaxCalls = AddNumber(tab, "连麦次数", 20, 58, 0, 99, 3);
            _manageBatchSeconds = AddNumber(tab, "同批时间(秒)", 245, 58, 5, 300, 30);
            _applyConfigButton = new Button();
            _applyConfigButton.Text = "应用配置";
            _applyConfigButton.SetBounds(435, 56, 100, 30);
            _applyConfigButton.Click += delegate { ApplyRuntimeConfig(); };
            tab.Controls.Add(_applyConfigButton);

            _phasePreparationButton = AddPhaseButton(tab, "准备中", PhaseNames.Preparation, 20, 104);
            _phaseTestButton = AddPhaseButton(tab, "赛前测试", PhaseNames.PreMatchTest, 116, 104);
            _phaseMatchButton = AddPhaseButton(tab, "比赛中", PhaseNames.InMatch, 212, 104);
            _phaseEndedButton = AddPhaseButton(tab, "结束比赛", PhaseNames.Ended, 308, 104);

            Label requestsLabel = new Label();
            requestsLabel.Text = "提醒批次";
            requestsLabel.SetBounds(20, 150, 120, 22);
            tab.Controls.Add(requestsLabel);

            _batchList = new ListBox();
            _batchList.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            _batchList.SetBounds(20, 174, 780, 275);
            tab.Controls.Add(_batchList);

            _approveButton = new Button();
            _approveButton.Text = "同意选中正式请求";
            _approveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _approveButton.SetBounds(820, 174, 180, 34);
            _approveButton.Click += delegate { ApproveSelectedBatch(); };
            tab.Controls.Add(_approveButton);

            _closeBatchButton = new Button();
            _closeBatchButton.Text = "关闭选中请求";
            _closeBatchButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _closeBatchButton.SetBounds(820, 218, 180, 34);
            _closeBatchButton.Click += delegate { CloseSelectedBatch(); };
            tab.Controls.Add(_closeBatchButton);

            Label note = new Label();
            note.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            note.SetBounds(820, 270, 190, 130);
            note.Text = "比赛中：测试通讯关闭。\r\n正式请求同意后立即扣次。\r\n剩余为 0 时仍可同意，记录为超额批准。\r\n管理者同意会记录为代审批。";
            tab.Controls.Add(note);

            return tab;
        }

        private Button AddPhaseButton(Control parent, string text, string phase, int x, int y)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(x, y, 88, 30);
            button.Click += delegate { SendPhase(phase); };
            parent.Controls.Add(button);
            return button;
        }

        private TabPage BuildPlayerTab()
        {
            TabPage tab = new TabPage("选手端");

            _playerStatusLabel = new Label();
            _playerStatusLabel.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
            _playerStatusLabel.SetBounds(30, 34, 860, 44);
            _playerStatusLabel.Text = "等待提醒";
            tab.Controls.Add(_playerStatusLabel);

            _ackButton = new Button();
            _ackButton.Text = "已收到（Ctrl+Alt+R）";
            _ackButton.SetBounds(30, 98, 210, 42);
            _ackButton.Click += delegate { SendAcknowledgement(); };
            tab.Controls.Add(_ackButton);

            _showOverlayButton = new Button();
            _showOverlayButton.Text = "显示悬浮件";
            _showOverlayButton.SetBounds(260, 98, 130, 42);
            _showOverlayButton.Click += delegate
            {
                _overlay.ShowAlert(AlertTypes.Test, _currentName, 1, false);
            };
            tab.Controls.Add(_showOverlayButton);

            _hideOverlayButton = new Button();
            _hideOverlayButton.Text = "隐藏悬浮件";
            _hideOverlayButton.SetBounds(404, 98, 130, 42);
            _hideOverlayButton.Click += delegate { _overlay.HideOverlay(); };
            tab.Controls.Add(_hideOverlayButton);

            Label note = new Label();
            note.SetBounds(30, 166, 820, 80);
            note.Text = "选手比赛时可以最小化主窗口，仅保留透明置顶悬浮件。悬浮件默认点击穿透，不抢鼠标焦点；回执建议使用 Ctrl+Alt+R。";
            tab.Controls.Add(note);

            return tab;
        }

        private TabPage BuildTeammateTab()
        {
            TabPage tab = new TabPage("队友端");

            Label targetLabel = new Label();
            targetLabel.Text = "目标选手";
            targetLabel.SetBounds(30, 34, 90, 24);
            tab.Controls.Add(targetLabel);

            _targetPlayerCombo = new ComboBox();
            _targetPlayerCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _targetPlayerCombo.SetBounds(124, 30, 220, 28);
            tab.Controls.Add(_targetPlayerCombo);

            _sendTestAlertButton = new Button();
            _sendTestAlertButton.Text = "确认发送测试提醒";
            _sendTestAlertButton.SetBounds(30, 84, 210, 46);
            _sendTestAlertButton.Click += delegate { ConfirmAndSendAlert(AlertTypes.Test); };
            tab.Controls.Add(_sendTestAlertButton);

            _sendOfficialAlertButton = new Button();
            _sendOfficialAlertButton.Text = "确认发起正式告警";
            _sendOfficialAlertButton.SetBounds(260, 84, 210, 46);
            _sendOfficialAlertButton.BackColor = Color.FromArgb(210, 52, 52);
            _sendOfficialAlertButton.ForeColor = Color.White;
            _sendOfficialAlertButton.Click += delegate { ConfirmAndSendAlert(AlertTypes.Official); };
            tab.Controls.Add(_sendOfficialAlertButton);

            _teammateStatusLabel = new Label();
            _teammateStatusLabel.Font = new Font(Font, FontStyle.Bold);
            _teammateStatusLabel.SetBounds(30, 156, 880, 40);
            _teammateStatusLabel.Text = "请选择目标选手";
            tab.Controls.Add(_teammateStatusLabel);

            Label note = new Label();
            note.SetBounds(30, 220, 850, 110);
            note.Text = "队友端不能输入自由文本。短时间内多次确认会合并为同一批次，但会再次触发选手端动画和声音。";
            tab.Controls.Add(note);

            return tab;
        }

        private TabPage BuildStatusTab()
        {
            TabPage tab = new TabPage("现场状态/日志");

            Label membersLabel = new Label();
            membersLabel.Text = "在线成员";
            membersLabel.SetBounds(20, 18, 100, 22);
            tab.Controls.Add(membersLabel);

            _membersBox = new TextBox();
            _membersBox.Multiline = true;
            _membersBox.ReadOnly = true;
            _membersBox.ScrollBars = ScrollBars.Vertical;
            _membersBox.SetBounds(20, 44, 360, 405);
            _membersBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            tab.Controls.Add(_membersBox);

            Label logLabel = new Label();
            logLabel.Text = "日志";
            logLabel.SetBounds(400, 18, 100, 22);
            tab.Controls.Add(logLabel);

            _logBox = new TextBox();
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.SetBounds(400, 44, 600, 405);
            _logBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            tab.Controls.Add(_logBox);

            return tab;
        }

        private void StartLocalServer(string hostRole)
        {
            try
            {
                AppConfig config = new AppConfig();
                config.RoomName = _serverRoom.Text.Trim();
                config.Password = _serverPassword.Text;
                config.Port = (int)_serverPort.Value;
                config.MaxOfficialCalls = (int)_serverMaxCalls.Value;
                config.BatchWindowSeconds = (int)_serverBatchSeconds.Value;

                if (String.IsNullOrEmpty(config.RoomName))
                {
                    MessageBox.Show("请填写房间名。");
                    return;
                }

                if (_server != null)
                {
                    _server.Stop();
                    _server = null;
                }
                _server = new EmergencyServer();
                _server.LogCreated += delegate(string line) { SafeAppendLog(line); };
                _server.Start(config);
                _serverAddressLabel.Text = "当前房间：" + config.RoomName + "\r\n地址：" + NetUtil.GetLocalIpSummary(_server.ActualPort);
                SetServerInputsEnabled(false);
                _connectHost.Text = "127.0.0.1";
                _connectPort.Value = _server.ActualPort;
                _connectRoom.Text = config.RoomName;
                _connectPassword.Text = config.Password;

                string localName = hostRole == RoleNames.Manager ? "管理者-本机" : "主办方-本机";
                _displayName.Text = localName;
                _roleCombo.SelectedItem = RoleNames.Display(hostRole);
                Connect("127.0.0.1", _server.ActualPort, config.RoomName, config.Password, localName, hostRole);
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动服务器失败：" + ex.Message + "\r\n请检查端口是否被占用，或是否已允许防火墙访问。");
            }
        }

        private void StopLocalServer()
        {
            DisconnectClient();
            if (_server != null)
            {
                _server.Stop();
                _server = null;
            }
            _serverAddressLabel.Text = "未启动";
            SetServerInputsEnabled(true);
            SafeAppendLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | 本机服务器已停止");
        }

        private void SetServerInputsEnabled(bool enabled)
        {
            _serverRoom.Enabled = enabled;
            _serverPassword.Enabled = enabled;
            _serverPort.Enabled = enabled;
            _serverMaxCalls.Enabled = enabled;
            _serverBatchSeconds.Enabled = enabled;
            _startOrganizerServerButton.Enabled = enabled;
            _startManagerServerButton.Enabled = enabled;
        }

        private void ConnectAsSelectedRole()
        {
            Connect(_connectHost.Text.Trim(), (int)_connectPort.Value, _connectRoom.Text.Trim(),
                _connectPassword.Text, _displayName.Text.Trim(), RoleNames.FromDisplay(Convert.ToString(_roleCombo.SelectedItem)));
        }

        private void Connect(string host, int port, string room, string password, string name, string role)
        {
            if (String.IsNullOrEmpty(host) || String.IsNullOrEmpty(room) || String.IsNullOrEmpty(name))
            {
                MessageBox.Show("请填写服务器地址、房间名和显示名称。");
                return;
            }

            try
            {
                DisconnectClient();
                _client = new LinkClient();
                _client.MessageReceived += OnClientMessage;
                _client.StatusChanged += delegate(string text) { SafeSetConnectionStatus(text); };
                _currentRole = role;
                _currentName = name;
                _client.Connect(host, port, room, password, name, role);
                RefreshRoleUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show("连接失败：" + ex.Message);
                SafeSetConnectionStatus("连接失败");
            }
        }

        private void DisconnectClient()
        {
            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
            _lastAlertBatch = "";
            _overlay.HideOverlay();
            SafeSetConnectionStatus("未连接");
            RefreshRoleUi();
        }

        private void OnClientMessage(Dictionary<string, string> message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<Dictionary<string, string> >(OnClientMessage), message);
                return;
            }

            string command = Protocol.Get(message, "cmd");
            if (command == "welcome")
            {
                _currentName = Protocol.Get(message, "name");
                _currentRole = Protocol.Get(message, "role");
                SafeSetConnectionStatus("已加入：" + RoleNames.Display(_currentRole) + " " + _currentName);
                RefreshRoleUi();
            }
            else if (command == "state")
            {
                ApplyState(message);
            }
            else if (command == "alert")
            {
                ApplyAlert(message);
            }
            else if (command == "log")
            {
                SafeAppendLog(Protocol.Get(message, "line"));
            }
            else if (command == "error")
            {
                string error = Protocol.Get(message, "message");
                SafeAppendLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | 错误：" + error);
                MessageBox.Show(error, "EmergencyLink");
            }
        }

        private void ApplyState(Dictionary<string, string> message)
        {
            _currentPhase = Protocol.Get(message, "phase");
            int maxCalls = Protocol.GetInt(message, "maxCalls", 3);
            int used = Protocol.GetInt(message, "usedOfficial", 0);
            int remaining = Protocol.GetInt(message, "remaining", 0);
            int over = Protocol.GetInt(message, "overLimit", 0);
            int batchSeconds = Protocol.GetInt(message, "batchSeconds", 30);

            _phaseLabel.Text = "阶段：" + PhaseNames.Display(_currentPhase);
            _quotaLabel.Text = "连麦额度：" + maxCalls.ToString() + "，已用：" + used.ToString() + "，剩余：" + remaining.ToString() + "，超额：" + over.ToString();
            if (!_manageMaxCalls.Focused) _manageMaxCalls.Value = Clamp(maxCalls, _manageMaxCalls.Minimum, _manageMaxCalls.Maximum);
            if (!_manageBatchSeconds.Focused) _manageBatchSeconds.Value = Clamp(batchSeconds, _manageBatchSeconds.Minimum, _manageBatchSeconds.Maximum);
            _membersBox.Text = Protocol.Get(message, "members");

            UpdatePlayers(Protocol.Get(message, "players"));
            UpdateBatches(Protocol.Get(message, "batches"));
            RefreshRoleUi();
        }

        private decimal Clamp(int value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void UpdatePlayers(string playersText)
        {
            string selected = Convert.ToString(_targetPlayerCombo.SelectedItem);
            _targetPlayerCombo.Items.Clear();
            string[] players = Protocol.SplitUnits(playersText);
            for (int i = 0; i < players.Length; i++)
            {
                if (!String.IsNullOrEmpty(players[i])) _targetPlayerCombo.Items.Add(players[i]);
            }
            if (!String.IsNullOrEmpty(selected) && _targetPlayerCombo.Items.Contains(selected))
            {
                _targetPlayerCombo.SelectedItem = selected;
            }
            else if (_targetPlayerCombo.Items.Count > 0)
            {
                _targetPlayerCombo.SelectedIndex = 0;
            }
        }

        private void UpdateBatches(string batchesText)
        {
            _currentBatches.Clear();
            _batchList.Items.Clear();
            string[] records = Protocol.SplitRecords(batchesText);
            for (int i = 0; i < records.Length; i++)
            {
                string[] units = Protocol.SplitUnits(records[i]);
                if (units.Length < 11) continue;
                AlertView view = new AlertView();
                view.Id = units[0];
                view.Type = units[1];
                view.Target = units[2];
                view.Status = units[3];
                Int32.TryParse(units[4], out view.Count);
                view.AckBy = units[5];
                view.ApprovedBy = units[6];
                view.IsOverLimit = units[7] == "1";
                view.Initiators = units[9];
                _currentBatches.Add(view);
                _batchList.Items.Add(view);
            }

            UpdateTeammateStatus();
        }

        private void ApplyAlert(Dictionary<string, string> message)
        {
            string batch = Protocol.Get(message, "batch");
            string type = Protocol.Get(message, "type");
            string target = Protocol.Get(message, "target");
            int count = Protocol.GetInt(message, "count", 1);
            bool overLimit = Protocol.GetBool(message, "overLimit");

            if (_currentRole == RoleNames.Player && String.Equals(target, _currentName, StringComparison.OrdinalIgnoreCase))
            {
                _lastAlertBatch = batch;
                _playerStatusLabel.Text = AlertTypes.Display(type) + "：队友请求与你连麦，请回执";
                _overlay.ShowAlert(type, target, count, overLimit);

                Dictionary<string, string> delivered = new Dictionary<string, string>();
                delivered["cmd"] = "delivered";
                delivered["batch"] = batch;
                SafeSend(delivered);
            }

            if (_currentRole == RoleNames.Teammate)
            {
                _teammateStatusLabel.Text = "提醒已发出/合并，等待选手回执和主办方处理";
            }
        }

        private void RefreshRoleUi()
        {
            bool connected = _client != null && _client.IsConnected;
            bool canManage = connected && RoleNames.CanManageRoom(_currentRole);
            bool canApprove = connected && RoleNames.CanApprove(_currentRole);
            bool isPlayer = connected && _currentRole == RoleNames.Player;
            bool isTeammate = connected && _currentRole == RoleNames.Teammate;

            _applyConfigButton.Enabled = canManage;
            _phasePreparationButton.Enabled = canManage;
            _phaseTestButton.Enabled = canManage;
            _phaseMatchButton.Enabled = canManage;
            _phaseEndedButton.Enabled = canManage;
            _approveButton.Enabled = canApprove;
            _closeBatchButton.Enabled = canApprove;

            _ackButton.Enabled = isPlayer && !String.IsNullOrEmpty(_lastAlertBatch);
            _showOverlayButton.Enabled = isPlayer;
            _hideOverlayButton.Enabled = isPlayer;

            _targetPlayerCombo.Enabled = isTeammate || _currentRole == RoleNames.Manager;
            _sendTestAlertButton.Enabled = connected && (isTeammate || _currentRole == RoleNames.Manager) && _currentPhase == PhaseNames.PreMatchTest;
            _sendOfficialAlertButton.Enabled = connected && (isTeammate || _currentRole == RoleNames.Manager) && _currentPhase == PhaseNames.InMatch;
        }

        private void ApplyRuntimeConfig()
        {
            Dictionary<string, string> message = new Dictionary<string, string>();
            message["cmd"] = "config";
            message["maxCalls"] = ((int)_manageMaxCalls.Value).ToString();
            message["batchSeconds"] = ((int)_manageBatchSeconds.Value).ToString();
            SafeSend(message);
        }

        private void SendPhase(string phase)
        {
            Dictionary<string, string> message = new Dictionary<string, string>();
            message["cmd"] = "phase";
            message["phase"] = phase;
            SafeSend(message);
        }

        private void ConfirmAndSendAlert(string type)
        {
            string target = Convert.ToString(_targetPlayerCombo.SelectedItem);
            if (String.IsNullOrEmpty(target))
            {
                MessageBox.Show("请先选择目标选手。");
                return;
            }

            string text = type == AlertTypes.Test
                ? "确认发送测试提醒？该操作不扣减正式连麦次数。"
                : "确认发起正式告警？该操作会通知选手和主办方。";
            DialogResult result = MessageBox.Show(text, "二次确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (result != DialogResult.OK) return;

            Dictionary<string, string> message = new Dictionary<string, string>();
            message["cmd"] = "alert";
            message["type"] = type;
            message["target"] = target;
            SafeSend(message);
        }

        private void SendAcknowledgement()
        {
            if (String.IsNullOrEmpty(_lastAlertBatch))
            {
                if (_currentRole == RoleNames.Player)
                {
                    _playerStatusLabel.Text = "当前没有需要回执的提醒";
                }
                return;
            }

            Dictionary<string, string> message = new Dictionary<string, string>();
            message["cmd"] = "ack";
            message["batch"] = _lastAlertBatch;
            SafeSend(message);
            _playerStatusLabel.Text = "已发送回执";
            _overlay.ShowAcknowledged();
            _lastAlertBatch = "";
            RefreshRoleUi();
        }

        private AlertView GetSelectedBatch()
        {
            return _batchList.SelectedItem as AlertView;
        }

        private void ApproveSelectedBatch()
        {
            AlertView batch = GetSelectedBatch();
            if (batch == null)
            {
                MessageBox.Show("请先选择一个正式请求。");
                return;
            }
            if (batch.Type != AlertTypes.Official)
            {
                MessageBox.Show("测试提醒无需审批。");
                return;
            }

            string text = batch.IsOverLimit ? "这是超额紧急请求，确认同意并记录超额批准？" : "确认同意该正式连麦请求并立即扣减一次？";
            if (MessageBox.Show(text, "确认同意", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;

            Dictionary<string, string> message = new Dictionary<string, string>();
            message["cmd"] = "approve";
            message["batch"] = batch.Id;
            SafeSend(message);
        }

        private void CloseSelectedBatch()
        {
            AlertView batch = GetSelectedBatch();
            if (batch == null) return;
            Dictionary<string, string> message = new Dictionary<string, string>();
            message["cmd"] = "close_batch";
            message["batch"] = batch.Id;
            SafeSend(message);
        }

        private void UpdateTeammateStatus()
        {
            if (_currentRole != RoleNames.Teammate) return;
            for (int i = 0; i < _currentBatches.Count; i++)
            {
                AlertView batch = _currentBatches[i];
                if (batch.Type == AlertTypes.Official || batch.Type == AlertTypes.Test)
                {
                    string status = AlertTypes.Display(batch.Type) + "：" + BatchStatus.Display(batch.Status);
                    if (!String.IsNullOrEmpty(batch.AckBy)) status += "，选手已回执";
                    if (!String.IsNullOrEmpty(batch.ApprovedBy)) status += "，主办方已同意";
                    if (batch.IsOverLimit) status += "，超额";
                    _teammateStatusLabel.Text = status;
                    return;
                }
            }
        }

        private void SafeSend(Dictionary<string, string> message)
        {
            if (_client == null || !_client.IsConnected)
            {
                MessageBox.Show("尚未连接房间。");
                return;
            }
            _client.Send(message);
        }

        private void SafeAppendLog(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SafeAppendLog), line);
                return;
            }
            if (String.IsNullOrEmpty(line)) return;
            _logBox.AppendText(line + Environment.NewLine);
        }

        private void SafeSetConnectionStatus(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SafeSetConnectionStatus), text);
                return;
            }
            _connectionStatusLabel.Text = text;
        }
    }
}
