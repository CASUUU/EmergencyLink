using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace EmergencyLink
{
    public static class NetUtil
    {
        public static string GetLocalIpSummary(int port)
        {
            List<string> items = new List<string>();
            try
            {
                IPHostEntry entry = Dns.GetHostEntry(Dns.GetHostName());
                for (int i = 0; i < entry.AddressList.Length; i++)
                {
                    IPAddress address = entry.AddressList[i];
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    {
                        items.Add(address.ToString() + ":" + port.ToString());
                    }
                }
            }
            catch
            {
            }

            if (items.Count == 0) items.Add("127.0.0.1:" + port.ToString());
            return String.Join("  |  ", items.ToArray());
        }
    }
}
