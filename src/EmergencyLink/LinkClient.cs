using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace EmergencyLink
{
    public sealed class LinkClient : IDisposable
    {
        private TcpClient _tcpClient;
        private StreamReader _reader;
        private StreamWriter _writer;
        private Thread _listenThread;
        private readonly object _sendLock = new object();
        private bool _running;

        public event Action<Dictionary<string, string> > MessageReceived;
        public event Action<string> StatusChanged;

        public bool IsConnected
        {
            get { return _running && _tcpClient != null && _tcpClient.Connected; }
        }

        public void Connect(string host, int port, string roomName, string password, string displayName, string role)
        {
            Close();

            _tcpClient = new TcpClient();
            _tcpClient.Connect(host, port);
            NetworkStream stream = _tcpClient.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, new UTF8Encoding(false));
            _writer.AutoFlush = true;
            _running = true;

            _listenThread = new Thread(ListenLoop);
            _listenThread.IsBackground = true;
            _listenThread.Start();

            Dictionary<string, string> join = new Dictionary<string, string>();
            join["cmd"] = "join";
            join["room"] = roomName;
            join["password"] = password;
            join["name"] = displayName;
            join["role"] = role;
            Send(join);
        }

        public void Send(Dictionary<string, string> fields)
        {
            lock (_sendLock)
            {
                if (_writer == null) return;
                _writer.WriteLine(Protocol.Encode(fields));
            }
        }

        public void Close()
        {
            _running = false;
            try
            {
                if (_tcpClient != null) _tcpClient.Close();
            }
            catch
            {
            }

            _reader = null;
            _writer = null;
            _tcpClient = null;
            OnStatus("未连接");
        }

        private void ListenLoop()
        {
            OnStatus("已连接，等待服务器确认");
            try
            {
                while (_running)
                {
                    string line = _reader.ReadLine();
                    if (line == null) break;
                    Dictionary<string, string> fields = Protocol.Decode(line);
                    Action<Dictionary<string, string> > handler = MessageReceived;
                    if (handler != null) handler(fields);
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
                _running = false;
                OnStatus("连接已断开");
            }
        }

        private void OnStatus(string message)
        {
            Action<string> handler = StatusChanged;
            if (handler != null) handler(message);
        }

        public void Dispose()
        {
            Close();
        }
    }
}
