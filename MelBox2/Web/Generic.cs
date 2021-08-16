using Grapevine;
using MelBoxGsm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    public static partial class Html
    {
        public static string Page(string path, Dictionary<string, string> insert)
        {
            if (!File.Exists(path))
                return "<p>Datei nicht gefunden: <i>" + path + "</i><p>";

            string template = System.IO.File.ReadAllText(path);

            StringBuilder sb = new StringBuilder(template);

            if (insert != null)
            {
                foreach (var key in insert.Keys)
                {
                    sb.Replace(key, insert[key]);
                }
            }

            return sb.ToString();
        }

        public static Dictionary<string, string> ReadCookies(IHttpContext context)
        {
            Dictionary<string, string> cookies = new Dictionary<string, string>();

            foreach (Cookie cookie in context.Request.Cookies)
            {
                cookies.Add(cookie.Name, cookie.Value);
            }

            return cookies;
        }

        public static async Task PageAsync(IHttpContext context, string titel, string body, string userName = "")
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Titel", titel },
                { "@Quality", Gsm.SignalQuality.ToString() },
                { "@Inhalt", body },
                { "@User", userName }
            };

            string html = Page(Server.Html_Skeleton, pairs);

            await context.Response.SendResponseAsync(html).ConfigureAwait(false);
        }

        /// <summary>
        /// POST-Inhalte lesen
        /// </summary>
        /// <param name="context"></param>
        /// <returns>Key-Value-Pair</returns>
        public static Dictionary<string, string> Payload(IHttpContext context)
        {
            System.IO.Stream body = context.Request.InputStream;
            System.IO.StreamReader reader = new System.IO.StreamReader(body);

            string[] pairs = reader.ReadToEnd().Split('&');

            Dictionary<string, string> payload = new Dictionary<string, string>();

            foreach (var pair in pairs)
            {
                string[] item = pair.Split('=');

                if (item.Length > 1)
                    payload.Add(item[0], WebUtility.UrlDecode(item[1]));
            }

            return payload;
        }

        internal static string ButtonNew(string root)
        {
            return $"<button style='width:20%' class='w3-button w3-block w3-blue w3-section w3-padding w3-margin-right w3-col type='submit' formaction='/{root}/new'>Neu</button>\r\n";
        }

        internal static string ButtonDelete(string root, int id)
        {
            return $"<button style='width:20%' class='w3-button w3-block w3-pink w3-section w3-padding w3-col type='submit' formaction='/{root}/delete/{id}'>Löschen</button>\r\n";
        }

        /// <summary>
        /// 'Popup'-Nachrichten
        /// </summary>
        /// <param name="prio"></param>
        /// <param name="caption"></param>
        /// <param name="alarmText"></param>
        /// <returns></returns>
        public static string Alert(int prio, string caption, string alarmText)
        {
            StringBuilder builder = new StringBuilder();

            switch (prio)
            {
                case 1:
                    builder.Append("<div class='w3-panel w3-margin-left w3-pale-red w3-leftbar w3-border-red'>\n");
                    break;
                case 2:
                    builder.Append("<div class='w3-panel w3-margin-left w3-pale-yellow w3-leftbar w3-border-yellow'>\n");
                    break;
                case 3:
                    builder.Append("<div class='w3-panel w3-margin-left w3-pale-green w3-leftbar w3-border-green'>\n");
                    break;
                default:
                    builder.Append("<div class='w3-panel w3-margin-left w3-pale-blue w3-leftbar w3-border-blue'>\n");
                    break;
            }

            builder.Append(" <h3>" + caption + "</h3>\n");
            builder.Append(" <p>" + alarmText + "</p>\n");
            builder.Append("</div>\n");

            return builder.ToString();
        }

    }
}
