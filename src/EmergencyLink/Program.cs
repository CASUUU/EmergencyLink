using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using EmergencyLink.Forms;

namespace EmergencyLink
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0] == "--self-test")
            {
                return SelfTest.Run();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }

    internal static class SelfTest
    {
        public static int Run()
        {
            EmergencyServer server = null;
            LinkClient organizer = null;
            LinkClient player = null;
            LinkClient teammate = null;

            AutoResetEvent playerAlerted = new AutoResetEvent(false);
            AutoResetEvent approvedSeen = new AutoResetEvent(false);
            string alertBatch = "";

            try
            {
                AppConfig config = AppConfig.CreateDefault();
                config.RoomName = "self-test-room";
                config.Password = "self-test-password";
                config.Port = 0;
                config.MaxOfficialCalls = 1;
                config.BatchWindowSeconds = 30;

                server = new EmergencyServer();
                server.Start(config);
                int port = server.ActualPort;

                organizer = new LinkClient();
                player = new LinkClient();
                teammate = new LinkClient();

                player.MessageReceived += delegate(Dictionary<string, string> message)
                {
                    if (Protocol.Get(message, "cmd") == "alert")
                    {
                        alertBatch = Protocol.Get(message, "batch");
                        Dictionary<string, string> delivered = new Dictionary<string, string>();
                        delivered["cmd"] = "delivered";
                        delivered["batch"] = alertBatch;
                        player.Send(delivered);

                        Dictionary<string, string> ack = new Dictionary<string, string>();
                        ack["cmd"] = "ack";
                        ack["batch"] = alertBatch;
                        player.Send(ack);
                        playerAlerted.Set();
                    }
                };

                organizer.MessageReceived += delegate(Dictionary<string, string> message)
                {
                    if (Protocol.Get(message, "cmd") == "state" && Protocol.Get(message, "batches").IndexOf("approved") >= 0)
                    {
                        approvedSeen.Set();
                    }
                };

                organizer.Connect("127.0.0.1", port, config.RoomName, config.Password, "self-organizer", RoleNames.Organizer);
                player.Connect("127.0.0.1", port, config.RoomName, config.Password, "self-player", RoleNames.Player);
                teammate.Connect("127.0.0.1", port, config.RoomName, config.Password, "self-teammate", RoleNames.Teammate);

                Thread.Sleep(500);

                Dictionary<string, string> phase = new Dictionary<string, string>();
                phase["cmd"] = "phase";
                phase["phase"] = PhaseNames.InMatch;
                organizer.Send(phase);

                Thread.Sleep(200);

                Dictionary<string, string> alert = new Dictionary<string, string>();
                alert["cmd"] = "alert";
                alert["type"] = AlertTypes.Official;
                alert["target"] = "self-player";
                teammate.Send(alert);

                if (!playerAlerted.WaitOne(3000)) return 2;

                Dictionary<string, string> approve = new Dictionary<string, string>();
                approve["cmd"] = "approve";
                approve["batch"] = alertBatch;
                organizer.Send(approve);

                if (!approvedSeen.WaitOne(3000)) return 3;
                return 0;
            }
            catch
            {
                return 1;
            }
            finally
            {
                if (teammate != null) teammate.Close();
                if (player != null) player.Close();
                if (organizer != null) organizer.Close();
                if (server != null) server.Stop();
            }
        }
    }
}
