using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace QBittorrent.Client.Internal
{
    internal class CompatibleFormUrlEncodedContent : StringContent
    {
        public CompatibleFormUrlEncodedContent(IEnumerable<(string key, string value)> fields) 
            : base(GetContent(fields), null, "application/x-www-form-urlencoded")
        {
        }

        private static string GetContent(IEnumerable<(string key, string value)> fields)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var field in fields)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Encode(field.key));
                builder.Append('=');
                builder.Append(Encode(field.value));
            }

            return builder.ToString();

            string Encode(string data)
            {
                if (String.IsNullOrEmpty(data))
                {
                    return String.Empty;
                }
                return Uri.EscapeDataString(data);
            }
        }
    }
}
