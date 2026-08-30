using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace EmergencyLink
{
    public static class Protocol
    {
        public const string RecordSeparator = "\u001e";
        public const string UnitSeparator = "\u001f";

        public static string Encode(Dictionary<string, string> fields)
        {
            StringBuilder builder = new StringBuilder();
            bool first = true;
            foreach (KeyValuePair<string, string> item in fields)
            {
                if (!first) builder.Append("&");
                first = false;
                builder.Append(WebUtility.UrlEncode(item.Key));
                builder.Append("=");
                builder.Append(WebUtility.UrlEncode(item.Value ?? String.Empty));
            }
            return builder.ToString();
        }

        public static Dictionary<string, string> Decode(string line)
        {
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (String.IsNullOrEmpty(line)) return fields;
            string[] parts = line.Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                int eq = part.IndexOf('=');
                if (eq < 0)
                {
                    fields[WebUtility.UrlDecode(part)] = String.Empty;
                }
                else
                {
                    string key = WebUtility.UrlDecode(part.Substring(0, eq));
                    string value = WebUtility.UrlDecode(part.Substring(eq + 1));
                    fields[key] = value;
                }
            }
            return fields;
        }

        public static string Get(Dictionary<string, string> fields, string key)
        {
            string value;
            if (fields.TryGetValue(key, out value)) return value;
            return String.Empty;
        }

        public static int GetInt(Dictionary<string, string> fields, string key, int fallback)
        {
            int value;
            if (Int32.TryParse(Get(fields, key), out value)) return value;
            return fallback;
        }

        public static bool GetBool(Dictionary<string, string> fields, string key)
        {
            string value = Get(fields, key);
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static string PackRecord(params string[] values)
        {
            return String.Join(UnitSeparator, values);
        }

        public static string[] SplitRecords(string value)
        {
            if (String.IsNullOrEmpty(value)) return new string[0];
            return value.Split(new string[] { RecordSeparator }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string[] SplitUnits(string value)
        {
            if (String.IsNullOrEmpty(value)) return new string[0];
            return value.Split(new string[] { UnitSeparator }, StringSplitOptions.None);
        }
    }
}
