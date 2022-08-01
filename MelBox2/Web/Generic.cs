using Grapevine;
using MelBoxGsm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MelBox2
{
    public static partial class Html
    {
        public static int MaxTableRowsShow { get; set; } = 200;

        public static bool IsBitSet(this int value, int position)
        {
            // Return whether bit at position is set to 1.
            return (value & (1 << position)) != 0;
        }

        public static int SetBit(this int value, int position)
        {
            // Set a bit at position to 1.
            return value |= (1 << position);
        }


        #region generelle Seitendarstellung
        /// <summary>
        /// Liest Benuter anhand von Cookie aus
        /// </summary>
        /// <param name="context"></param>
        /// <param name="blockUser">true = Unbekannten Nutzern wird der weitere Zugriff verweigert</param>
        /// <returns></returns>
        internal static async Task<Person> GetLogedInUserAsync(IHttpContext context, bool blockUser = true)
        {
            _ = Html.ReadCookies(context).TryGetValue("MelBoxId", out string guid);

            Person user = new Person();

            if (guid == null || !Server.LogedInHash.TryGetValue(guid, out user))
            {
                if (blockUser)
                    await Routes.Home(context);
            }

            return user;
        }

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

        internal static async Task PageAsync(IHttpContext context, string titel, string body, Person user = null)
        {

            string connectIcon = Gsm.NetworkRegistration != Gsm.Registration.Registered ? "signal_cellular_connected_no_internet_0_bar" :
                Gsm.SignalQuality > 50 ? "signal_cellular_4_bar" :
                Gsm.SignalQuality > 20 ? "network_cell" :
                "signal_cellular_0_bar";


            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Titel", titel },
                { "@Quality", Gsm.SignalQuality.ToString() },
                { "@GsmConnect", connectIcon},
                { "@GsmWarning" , (Gsm.NetworkRegistration != Gsm.Registration.Registered ? "keine Verbindung" : string.Empty)},
                { "@Inhalt", body },
                { "@User", user == null ? string.Empty : user.Name}
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
                    payload.Add(item[0], WebUtility.UrlDecode(item[1]).EncodeHtml());
            }

            return payload;
        }

        #endregion


        #region html-Snippets

        internal static string ButtonNew(string root, string caption = "Neu")
        {
            return $"<button style='width:20%' class='w3-button w3-block w3-blue w3-section w3-padding w3-margin-right w3-col type='submit' formaction='/{root}/new'>{caption}</button>\r\n";
        }

        internal static string ButtonDelete(string root, int id)
        {
            return $"<button style='width:20%' class='w3-button w3-block w3-pink w3-section w3-padding w3-col' type='submit' formaction='/{root}/delete/{id}'>Löschen</button>\r\n";
        }

        internal static string ButtonDeleteDisabled(string tooltip)
        {
            return $"<button style='width:20%' class='w3-button w3-block w3-pink w3-section w3-padding w3-col type='submit' title='{tooltip}' disabled>Löschen</button>\r\n";
        }

        internal static string ButtonDownload(string table)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<a href='\\excel\\{table.ToLower()}' class='w3-button w3-white w3-display-position w3-badge material-icons-outlined' title='Download Tabelle \"{table}\"' style='top:140px;right:80px;';>file_download</a>");

            return sb.ToString();
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
                    builder.AppendLine("<div class='w3-panel w3-margin-left w3-pale-red w3-leftbar w3-border-red'>\n");
                    break;
                case 2:
                    builder.AppendLine("<div class='w3-panel w3-margin-left w3-pale-yellow w3-leftbar w3-border-yellow'>\n");
                    break;
                case 3:
                    builder.AppendLine("<div class='w3-panel w3-margin-left w3-pale-green w3-leftbar w3-border-green'>\n");
                    break;
                default:
                    builder.AppendLine("<div class='w3-panel w3-margin-left w3-pale-blue w3-leftbar w3-border-blue'>\n");
                    break;
            }

            builder.AppendLine(" <h3>" + caption + "</h3>\n");
            builder.AppendLine(" <p>" + alarmText + "</p>\n");
            builder.AppendLine("</div>\n");

            return builder.ToString();
        }

        internal static string WeekDayCheckBox(int blockDays)
        {
            StringBuilder html = new StringBuilder("<span>");

            Dictionary<int, string> valuePairs = new Dictionary<int, string>() {
                { 1, "Mo" },
                { 2, "Di" },
                { 3, "Mi" },
                { 4, "Do" },
                { 5, "Fr" },
                { 6, "Sa" },
                { 0, "So" },
            };

            foreach (var day in valuePairs.Keys)
            {
                string check = IsBitSet(blockDays, day) ? "checked" : string.Empty;
                string dayName = valuePairs[day];

                html.AppendLine($"<input name={dayName} class='w3-check' type='checkbox' {check} disabled>" + Environment.NewLine);
                html.AppendLine($"<label>{dayName} </label>");
            }

            return html.AppendLine("</span>").ToString();
        }

        /// <summary>
        /// F&auml;rbe den Tag im Kalender ein
        /// </summary>
        /// <param name="holydays"></param>
        /// <param name="date"></param>
        /// <param name="isMarked"></param>
        /// <param name="isHandoverDay">Tag an dem die Berietschaft startet oder endet</param>
        /// <returns></returns>
        private static string WeekDayColor(List<DateTime> holydays, DateTime date, bool isMarked, bool isHandoverDay)
        {
            string html = string.Empty;

            if (date == DateTime.Now.Date) //heute
                html += "<td class='w3-border-left w3-green'>";
            else if (holydays.Contains(date)) //Feiertag?
                html += "<td class='w3-border-left w3-pale-red'>";
            else if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) //Wochenende ?              
                html += "<td class='w3-border-left w3-sand'>";
            else
                html += "<td class='w3-border-left'>";

            if (date == DateTime.MinValue)
                html += "&nbsp;";
            else if (isHandoverDay)
                html += $"<span class='w3-tag stripe-1' title='&Uuml;bergabe'>{date:dd}.</span>";
            else if (isMarked)
                html += $"<span class='w3-tag w3-pale-green'>{date:dd}.</span>";
            // html += $"<text>{date.ToString("dd")}.</text>";
            else
                //html += $"<i class='w3-opacity'>{date.ToString("dd")}.</i>";
                html += $"<span class='w3-tag w3-light-gray w3-opacity'>{date:dd}.</span>";

            html += "</td>";

            return html;
        }

        internal static string ChooseDate(string root, DateTime date)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<div class='w3-container w3-center w3-padding '>");
            sb.AppendLine($"  <form id='chooseDate' method='get' action='/{root}' >");
            sb.AppendLine("    <button type='button' class='w3-button w3-padding' onclick='inc(-1);'><span class='material-icons-outlined'>arrow_back_ios</span></button>");
            sb.AppendLine($"   <input name='datum' id='anzeigedatum' type='date' title='Anzeigedatum, Auswahl + Enter' placeholder='{date:yyyy-MM-dd}' value='{date:yyyy-MM-dd}' min='{DateTime.Now.AddYears(-10).Date:yyyy-MM-dd}' max='{DateTime.Now.Date:yyyy-MM-dd}' onblur='inc(0);' onkeyup='onEnter();'>"); //onblur='inc(0);'
            sb.AppendLine("    <button type='button' class='w3-button w3-padding' onclick='inc(1);'><span class='material-icons-outlined'>arrow_forward_ios</span></button>");
            sb.AppendLine("    <input type='hidden' id='submitfilter' name='filter' disabled>");
            sb.AppendLine("   </form>");
            sb.AppendLine("</div>");

            sb.AppendLine("\r\n<script>");
            sb.AppendLine(" function inc(x) {");
            sb.AppendLine("   if (x != 0) {");
            sb.AppendLine("     document.getElementById('anzeigedatum').stepUp(x);");
            sb.AppendLine("   }");
            sb.AppendLine("   const input = document.getElementById('submitfilter');");
            sb.AppendLine("   const f = document.getElementById('tablefilter');");
            sb.AppendLine("   if (typeof f !== 'undefined' && f !== null && f.value.length > 2) { ");
            sb.AppendLine("     input.disabled = false; ");
            sb.AppendLine("     input.value = f;");
            sb.AppendLine("   }");
            //sb.AppendLine("   if (document.getElementById('anzeigedatum').value.length > 8) {");
            sb.AppendLine("   document.getElementById('chooseDate').submit(); ");
            sb.AppendLine(" }");
            sb.AppendLine(" function onEnter() {");
            sb.AppendLine("   if (event.key == 'Enter') { inc(0); }");
            sb.AppendLine(" }");
            sb.AppendLine("</script>\r\n");

            return sb.ToString();
        }

        internal static string SelectHourOfDay(int selected)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<select class='w3-select w3-pale-yellow' name='hour'>");
            for (int i = 0; i < 24; i++)
                sb.AppendLine($"<option value='{i}' {(i == selected ? "selected" : string.Empty)}>{i} Uhr</option>");

            sb.AppendLine("</select>");
            return sb.ToString();
        }

        /// <summary>
        /// Erzeugt Buttons, um die Benutzerliste zu filtern
        /// </summary>
        /// <param name="filter">Teil-String nach dem gefiltert werden soll</param>
        /// <returns></returns>
        internal static string AccountFilter(string filter)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Filter:</b>");
            sb.AppendLine($"<a href='/account' class='w3-button' title='Filter zur&uuml;cksetzen'><i class='w3-xlarge material-icons-outlined'>filter_none</i></a>");
            sb.AppendLine($"<a href='/account?company=-{filter}' class='w3-button' title='Filter Firma enth&auml;lt nicht &quot;{filter}&quot;'><i class='w3-xlarge material-icons-outlined'>filter_1</i></a>");
            sb.AppendLine($"<a href='/account?company={filter}' class='w3-button' title='Filter Firma enth&auml;lt &quot;{filter}&quot;'><i class='w3-xlarge material-icons-outlined'>filter_2</i></a>");

            return sb.ToString();
        }

        /// <summary>
        /// HTML-Input-Felder zum manuellen Setzen der Rufumleitungsnummer
        /// </summary>
        /// <param name="user">Angemeldeter Benutzer (für Berechtigung)</param>
        /// <returns>HTML-Input-Felder</returns>
        internal static string ManualUpdateCallworwardNumber(Person user)
        {
            if (user == null || user.Level < Server.Level_Reciever) return "<p><i class='w3-opacity'>Keine Berechtigung zum Bearbeiten</i></p>";
            bool isAdmin = user.Level >= Server.Level_Admin;
            string html;

            if (isAdmin) // frei einstellbar
                html = "<input class='w3-button w3-pale-yellow w3-half' name='phone' pattern='\\+?[\\d\\s-/]{8,}' tite='Telefonummer für Weiterleitung der Sprachanrufe' placeholder='Mobilnummer z.B. 0150 123456789'>" +
                        "<input type='submit' value='Nummer zwangsweise &auml;ndern' class='w3-button w3-blue w3-quarter'>";
            else if (user.Phone.Length > 9 && ulong.TryParse(user.Phone.TrimStart('+'), out ulong _)) //nur eigene Nummer
            {
                string phoneStr = user.Phone;
                if (phoneStr.StartsWith("+"))
                    phoneStr = "0" + user.Phone.Remove(0, 3); //'+49...' -> '0...'

                html = $"<input name='phone' type='hidden' value='{phoneStr}'>" +
                        $"<input class='w3-button w3-light-blue w3-threequarter' type='submit' value='Sprachanrufe dauerhaft an mich weiterleiten ({phoneStr})'>";
            }
            else
                html = "<p><i>F&uuml;r den angemeldeten Benutzer ist keine g&uuml;ltige Telefonnumer hinterlegt.</i></p>";

            if (Program.OverideCallForwardingNumber.Length > 0 && (isAdmin || user.Phone == Program.OverideCallForwardingNumber)) //nur Admin oder Benutzer selbst kann deaktivieren 
                html += "<input class='w3-button w3-quarter w3-sand' type='submit' formaction='/gsm/callforward/off' value='deaktivieren'>";

            return html;
        }

        /// <summary>
        /// HTML für zusätzliche Suchleiste Form "Liste Empfangene Nachrichten nach Absender"
        /// </summary>
        internal const string SearchBySenderForm = "<form id='senderForm' action='/in/special' class='w3-margin-top w3-row w3-light-grey' accept-charset='utf-8'>\r\n" +
            "<div class='w3-container w3-quarter'>Nachrichten dieses Absenders auflisten:</div>\r\n" +
            "<input class='w3-container w3-input w3-border w3-half' name='sender' type='text'>\r\n" +
            "<input class='w3-container w3-button w3-light-blue' type='submit' value='Anzeigen'>\r\n" +
            "</form>";

        /// <summary>
        /// Es dürfen keine HTML-Entitäten in der Datenbank gespeichert sein. Angeleht an Formatierung in Foren (siehe auch https://de.wikipedia.org/wiki/BBCode).
        /// </summary>
        /// <param name="s">string mit codierten HTML-Entitäten</param>
        /// <returns>string mit decodierten HTML-Entitäten</returns>
        public static string HtmlEntitiesDecode(this string s)
        {
            StringBuilder sb = new StringBuilder(s);

            sb.Replace("[br]", "</br>");
            sb.Replace("[b]", "<b>");
            sb.Replace("[/b]", "</b>");
            sb.Replace("[i]", "<i>");
            sb.Replace("[/i]", "</i>");
            sb.Replace("[u]", "<u>");
            sb.Replace("[/u]", "</u>");
            sb.Replace("[h1]", "<h3>");
            sb.Replace("[/h1]", "</h3>");
            sb.Replace("[h2]", "<h4>");
            sb.Replace("[/h2]", "</h4>");
            sb.Replace("[s]", "<s>");
            sb.Replace("[/s]", "</s>");

            return sb.ToString();
        }

        /// <summary>
        /// Codiert HTML-Entities, um Script-Injection zu verhindern.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string EncodeHtml(this string s)
        {
            if (s?.Length > 3)
                return s.Replace("<", "&lt;").Replace(">", "&gt;");

            return s;
        }

        #endregion


        #region Tabellendarstellung

        internal static string FromTable(System.Data.DataTable dt, bool isAuthorized, string root = "x")
        {
            string html = string.Empty;

            if (dt.Rows.Count > 2) //Filter nur, wenn etwas zum Filtern da ist
            //{
                html += "<p><input oninput=\"w3.filterHTML('#table1', '.item', this.value)\" class='w3-input' id='tablefilter' placeholder='Suche nach..'></p>\r\n";
                /* //EInträge ignorieren
                html += "<p><input oninput=\"filterIgnoreHTML('#table1', '.item', this.value)\" class='w3-input w3-half' id='tablefilter' placeholder='ignoriere..'>" +
                "<script>function filterIgnoreHTML (id, sel, filter) {" + 
                "var a, b, c, i, ii, iii, hit;" +              
                "a = w3.getElements(id);" +
                "for (i = 0; i < a.length; i++)" +
                "{" +
                "b = a[i].querySelectorAll(sel);" +
                "for (ii = 0; ii < b.length; ii++)" +
                "{" +
                " hit = 0;" +
                "if (b[ii].innerText.toUpperCase().indexOf(filter.toUpperCase()) > -1)" +
                "{" +
                " hit = 1;" +
                "}" +
                "c = b[ii].getElementsByTagName('*');" +
                "   for (iii = 0; iii < c.length; iii++)" +
                "   {" +
                "       if (c[iii].innerText.toUpperCase().indexOf(filter.toUpperCase()) > -1)" +
                "       {" +
                "           hit = 1;" +
                "       }" +
                "   }" +
                "   if (hit == 0)" + //hier der Unterschied zu w3.filterHTML();
                "   {" +
                "       b[ii].style.display = '';" +
                "   } else {" +
                "       b[ii].style.display = 'none';" +
                "   }" +
                "}}};</script></p>\r\n";
            }
                //*/
            html += "<table id='table1' class='w3-table-all'>\n";
            //add header row
            html += "<tr>";

            if (isAuthorized)
            {
                html += "<th>Edit</th>";
            }

            if (dt.Rows.Count > 100) // Große Tabellen nicht sortierbar machen, da zu rechenintensiv im Browser!  
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                    html += $"<th>" +
                            $"{dt.Columns[i].ColumnName.Replace('_', ' ')}" +
                            $"</th>";
            }
            else
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                    html += $"<th class='w3-hover-sand' onclick=\"w3.sortHTML('#table1', '.item', 'td:nth-child({i + 2})')\" title='Klicken zum sortieren'>&#8645;&nbsp;" +
                            $"{dt.Columns[i].ColumnName.Replace('_', ' ')}" +
                            $"</th>";
            }
            html += "</tr>\n";

            //add rows
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                html += "<tr class='item'>";

                if (isAuthorized)
                {
                    html += "<td>" +
                        "<a href='/" + root + "/" + dt.Rows[i][0].ToString() + "'><i class='material-icons-outlined'>edit</i></a>" +
                        "</td>";
                }

                for (int j = 0; j < dt.Columns.Count; j++)
                {
                    string colName = dt.Columns[j].ColumnName;
                    if (colName.StartsWith("Gesperrt"))
                    {
                        html += "<td>" + WeekDayCheckBox(int.Parse(dt.Rows[i][j].ToString())) + "</td>";
                    }
                    else if (colName == "Empfangen" || colName == "Gesendet" || colName == "Zeit")
                    {
                        string x = dt.Rows[i][j].ToString();
                        html += $"<td>{x.Replace("-", "&#8209;").Replace(" ", "&nbsp;")}</td>"; //non-breaking hyphen, non-breaking space
                    }
                    else if (colName.StartsWith("Via"))
                    {
                        if (int.TryParse(dt.Rows[i][j].ToString(), out int via))
                        {
                            bool phone = 0 != (via & (int)Sql.Via.Sms);
                            bool email = 0 != ((via & (int)Sql.Via.Email) | (via & (int)Sql.Via.PermanentEmail));
                            bool whitelist = 0 != (via & (int)Sql.Via.EmailWhitelist);
                            bool noCalls = 0 != (via & (int)Sql.Via.NoCalls);

                            html += "<td>";
                            if (phone) html += "<span class='material-icons-outlined'>smartphone</span>";
                            if (email) html += "<span class='material-icons-outlined'>email</span>";
                            if (whitelist) html += "<span class='material-icons-outlined'>markunread_mailbox</span>";
                            if (noCalls) html += "<span class='material-icons-outlined'>phone_disabled</span>";
                            html += "</td>";
                        }
                    }
                    else if (colName.StartsWith("Attr"))
                    {
                        html += "<td>";
                        if (dt.Rows[i][j].ToString().Contains("z"))
                            html += "<span class='material-icons-outlined' title='Autorisierter Absender'>markunread_mailbox</span>";
                        if (dt.Rows[i][j].ToString().Contains("y"))
                            html += "<span class='material-icons-outlined' title='Rufweiterleitung'>call</span>";
                        if (dt.Rows[i][j].ToString().Contains("x"))
                            html += "<span class='material-icons-outlined' title='Dauerempf&auml;nger'>loyalty</span>";
                        if (dt.Rows[i][j].ToString().Contains("w"))
                            html += "<span class='material-icons-outlined' title='Empf&auml;ngt Anrufe/SMS'>smartphone</span>";
                        if (dt.Rows[i][j].ToString().Contains("v"))
                            html += "<span class='material-icons-outlined' title='Empf&auml;ngt E-Mail'>email</span>";
                        if (dt.Rows[i][j].ToString().Contains("u"))
                            html += "<span class='material-icons-outlined' title='keine Sprachanrufe'>phone_disabled</span>";
                        html += "</td>";
                    }
                    else if (colName.Contains("Status"))
                    {
                        string val = dt.Rows[i][j].ToString();
                        string deliveryStatus = "-unbekannt-";
                        string detaildStatus = "-unbekannt-";
                        string icon = "error";

                        if (int.TryParse(val, out int confirmation))
                            deliveryStatus = Gsm.GetDeliveryStatus(confirmation, out detaildStatus, out icon);

                        html += $"<td><span class='material-icons-outlined {(confirmation < 3 ? "w3-text-teal" : "")}' title='Wert: [{val}] {deliveryStatus} - {detaildStatus}'>{icon}</span></td>";
                    }
                    else if(colName == "Inhalt")
                    {
                        bool isMelSysOk = false;
                        foreach (var trigger in Program.LifeMessageTrigger)
                        {
                            if (dt.Rows[i][j].ToString().Contains(trigger))
                            {
                                isMelSysOk = true;
                                break;
                            }
                        }

                        if (isMelSysOk)
                            html += "<td class='w3-text-gray'>" + dt.Rows[i][j].ToString() + "</td>";
                        else
                            html += "<td>" + dt.Rows[i][j].ToString() + "</td>";
                    }
                    else
                    {
                        html += "<td>" + dt.Rows[i][j].ToString() + "</td>";
                    }
                }
                html += "</tr>\n";
            }
            html += "</table>\n";
            return html;
        }

        /// <summary>
        /// Tabelle für Bereitschaftsplaner anzeigen
        /// </summary>
        /// <param name="user">angemeldeter Benutzer (für Berechtigung)</param>
        /// <returns>html-Tabelle</returns>
        internal static string FromShiftTable(Person user)
        {
            if (user == null) user = new Person() { Id = 0, Level = 0 };

            System.Data.DataTable dt = Sql.SelectShiftsCalendar();

            string html = Modal("Bereitschaft", InfoShift());
            html += "<p><input oninput=\"w3.filterHTML('#table1', '.item', this.value)\" class='w3-input' placeholder='Suche nach..'></p>\r\n";

            //Quelle: https://css-tricks.com/stripes-css/
            html += "<style>"; //gestreifter hintergund w3-amber / w3-pale-green
            html += ".stripe-1 {";
            html += "color:black; ";
            html += "background: repeating-linear-gradient(";
            html += "45deg,";
            html += "#f0e68c,";
            html += "#f0e68c, 10px,";//#ffc107
            html += "#ddffdd 10px,";
            html += "#ddffdd 20px";
            html += ");} ";
            html += "</style>";

            html += "<div class='w3-container'>";
            html += "<table class='w3-table-all'>\n";

            //add header row
            html += "<tr class='item'>";
            html += "<th>Edit</th><th>Nr</th><th>Name</th><th>Via</th><th>Beginn</th><th>Ende</th><th>KW</th><th>Mo</th><th>Di</th><th>Mi</th><th>Do</th><th>Fr</th><th>Sa</th><th>So</th>";
            html += "</tr>\n";

            List<DateTime> holydays = Sql.Holydays(DateTime.Now);
            holydays.AddRange(Sql.Holydays(DateTime.Now.AddYears(1))); // Feiertage auch im kommenden Jahr anzeigen

            DateTime lastRowEnd = DateTime.MinValue; //Merker, ob Bereitschaft über Kalenderwoche hinaus geht.
            int lastKW = 0;

            //add rows
            int numRows = dt.Rows.Count;
            for (int i = 0; i < numRows; i++)
            {
                _ = int.TryParse(dt.Rows[i]["ID"].ToString(), out int shiftId);
                _ = int.TryParse(dt.Rows[i]["PersonId"].ToString(), out int shiftContactId);
                string contactName = dt.Rows[i]["Name"].ToString();
                _ = int.TryParse(dt.Rows[i]["Via"].ToString(), out int via);
                _ = DateTime.TryParse(dt.Rows[i]["Start"].ToString(), out DateTime start);
                _ = DateTime.TryParse(dt.Rows[i]["End"].ToString(), out DateTime end);
                _ = int.TryParse(dt.Rows[i]["KW"].ToString(), out int kw);

                bool isOwner = user.Level >= Server.Level_Admin || user.Level >= Server.Level_Reciever && (user.Id == shiftContactId || shiftId == 0);
                #region Editier-Button

                if (isOwner)
                {
                    string route = shiftId == 0 ? start.ToShortDateString() : shiftId.ToString();

                    html += "<td>" +
                        "<a href='/shift/" + route + "'><i class='material-icons-outlined'>edit</i></a>" +
                        "</td>";
                }
                else
                    html += "<td></td>";

                #endregion

                #region Bereitschafts-Id
                html += "<td>" + shiftId + "</td>";
                #endregion

                #region Name
                if (isOwner)
                    html += "<td><a href='/account/" + shiftContactId + "'>" + contactName + "</a></td>";
                else
                    html += "<td>" + contactName + "</td>";
                #endregion

                #region Sendeweg                
                bool phone = ((Sql.Via)via & Sql.Via.Sms) > 0; //int 1= SMS - Siehe Tabelle SendWay
                bool email = ((Sql.Via)via & Sql.Via.Email) > 0 || ((Sql.Via)via & Sql.Via.PermanentEmail) > 0; //= IsBitSet(via, 1) || IsBitSet(via, 2);  // int 2 = Email, int 4 = immerEmail - Siehe Tabelle SendWay
                bool noCalls = 0 != (via & (int)Sql.Via.NoCalls);

                html += "<td>";
                if (phone) html += "<span class='material-icons-outlined' title='per SMS'>smartphone</span>";
                if (email) html += "<span class='material-icons-outlined' title='per Email'>email</span>";
                if (contactName.Length > 0 && !phone && !email)
                    html += "<span class='material-icons-outlined' title='kein Empfangsweg'>report_problem</span>";
                if (noCalls) html += "<span class='material-icons-outlined' title='keine Sprachanrufe'>phone_disabled</span>";

                html += "</td>";
                #endregion

                #region Beginn
                html += $"<td>{(start == DateTime.MinValue ? "&nbsp;" : end == DateTime.MinValue ? start.ToLocalTime().ToShortDateString() : start.ToLocalTime().ToString("g"))}</td>";
                #endregion

                #region Ende
                html += $"<td>{(end == DateTime.MinValue ? "&nbsp;" : end.ToLocalTime().ToString("g"))}</td>";
                #endregion

                #region Kalenderwoche
                if (lastKW == kw) //Doppelt vergebene KW hervorheben                
                    html += "<td><span class='w3-tag w3-amber' title='KW mit mehr als einem Empf&auml;nger'>" + kw.ToString("00") + "</span></td>";
                else
                    html += "<td>" + kw.ToString("00") + "</td>";

                lastKW = kw;
                #endregion

                #region Wochentage
                //Wenn in dieser Woche die selbe Person Bereitschaft hat wie in der Vorwoche, den Bereitschaftswechesel am Montag nicht hervorheben
                bool sameAsLastWeek = i > 0
                    && start.DayOfWeek == DayOfWeek.Monday
                    && dt.Rows[i - 1]["Name"].ToString() == contactName
                    && start.Date == lastRowEnd.Date;

                for (int j = 7; j < dt.Columns.Count; j++)
                {
                    var dateStr = dt.Rows[i][j].ToString();
                    bool isMarked = dateStr.EndsWith("x");
                    _ = DateTime.TryParse(dateStr.TrimEnd('x') ?? "", out DateTime date);
                    bool isHandoverDay = (isMarked && date.Date == start.Date && !sameAsLastWeek) || (isMarked && date.Date == end.Date);

                    html += WeekDayColor(holydays, date, isMarked, isHandoverDay);
                }
                #endregion

                html += "</tr>\n";

                lastRowEnd = end;
            }

            html += "</table>\n";
            html += "</div>\n";

            return html;
        }

        /// <summary>
        /// Tabelle, bei der Einträge nur durch den Autor bearebiet werden können.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        internal static string FromNotesTable(Person user)
        {
            System.Data.DataTable dt = Sql.SelectLastNotes();

            string html = $"<form><button style='width:20%' class='w3-button w3-block w3-blue w3-padding type='button' formaction='/notepad/0'>Neue Notiz</button></form>\r\n";

            if (dt.Rows.Count > 2) //Filter nur, wenn etwas zum Filtern da ist
                html += "<p><input oninput=\"w3.filterHTML('#table1', '.item', this.value)\" class='w3-input' placeholder='Suche nach..'></p>\r\n";

            html += "<table id='table1' class='w3-table-all'>\n";

            //add header row
            html += "<tr><th>Edit</th>";
            html += $"<th>{dt.Columns["Bearbeitet"].ColumnName}</th>";
            html += $"<th>{dt.Columns["Von"].ColumnName}</th>";
            html += $"<th>{dt.Columns["Kunde"].ColumnName}</th>";
            html += $"<th>{dt.Columns["Notiz"].ColumnName}</th>";
            html += "</tr>\n";

            //Inhalt
            for (int i = 0; i < dt.Rows.Count; i++)
            {

                html += "<tr class='item'><td>";

                if (dt.Rows[i]["VonId"].ToString() == user.Id.ToString())
                    html += "<a href='/notepad/" + dt.Rows[i][0].ToString() + "'><i class='material-icons-outlined'>edit</i></a>";

                html += "</td><td>" + dt.Rows[i]["Bearbeitet"].ToString().Replace("-", "&#8209;").Replace(" ", "&nbsp;") + "</td>"; //non-breaking hyphen, non-breaking space
                html += "<td>" + dt.Rows[i]["Von"].ToString() + "</td>";
                html += "<td>" + dt.Rows[i]["Kunde"].ToString() + "</td>";
                html += "<td>" + dt.Rows[i]["Notiz"].ToString().HtmlEntitiesDecode() + "</td>";

                html += "</tr>\n";
            }

            html += "</table>\n";
            return html;
        }

        #endregion


        #region Infotexte
        internal static string Modal(string title, string body)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<div class='w3-container'>");
            sb.AppendLine($"   <button onclick = \"document.getElementById('id02').style.display='block'\" class='w3-button w3-white w3-display-position w3-badge material-icons-outlined' title='{title}' style='top:140px;right:20px;';>info</button>");
            sb.AppendLine("    <div id = 'id02' class='w3-modal'>");
            sb.AppendLine("        <div class='w3-modal-content'>");
            sb.AppendLine("            <div class='w3-container'>");
            sb.AppendLine("             <span onclick = \"document.getElementById('id02').style.display='none'\" class='w3-button w3-display-topright' title='Fenster schlie&szlig;en'>&times;</span>");
            sb.AppendLine($"            <div class='w3-container w3-center'><h3>{title}</h3></div>");
            sb.AppendLine(body);
            sb.AppendLine("             <div class='w3-container'>&nbsp;</div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("</div>");

            return sb.ToString();
        }

        internal static string InfoAccount()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<p>Hier werden die MelBox2-Benutzer verwaltet. Benutzer sind m&ouml;gliche Sender und Empf&auml;nger.</p>");

            sb.AppendLine("<div class='w3-container w3-margin-top'><ul class='w3-ul 3-card'>");
            sb.AppendLine(" <li><span class='material-icons-outlined'>edit</span> &Ouml;ffnet die Maske zum &Auml;ndern des Benutzerkontos.</li>");
            sb.AppendLine(" <li><span class='material-icons-outlined'>person_add</span> &Ouml;ffnet die Maske zum Hinzuf&uuml;gen des Benutzerkontos.</li>");
            sb.AppendLine(" <li><hr></li>");
            sb.AppendLine(" <li><b>Filter</b> - nur bei meheren Eintr&auml;gen vorhanden.</li>");
            sb.AppendLine(" <li><span class='material-icons-outlined'>filter_none</span> Setzt alle Anzeigefilter zurück.</li>");
            sb.AppendLine(" <li><span class='material-icons-outlined'>filter_1</span> Filter 1 - Zeigt nur externe Kontakte.</li>");
            sb.AppendLine(" <li><span class='material-icons-outlined'>filter_2</span> Filter 2 - Zeigt nur interne Kontakte.</li>");
            sb.AppendLine(" <li><hr></li>");
            sb.AppendLine(" <li><b>Attribute</b></li>");
            sb.AppendLine($" <li><span class='material-icons-outlined'>smartphone</span>Versand von SMS an diesen Empf&auml;nger ist freigegeben. Siehe Bereitschaft.</li>");
            sb.AppendLine($" <li><span class='material-icons-outlined'>email</span>Versand von E-Mail an diesen Empf&auml;nger ist freigegeben. Siehe Bereitschaft.</li>");            
            sb.AppendLine("  <li><span class='material-icons-outlined'>call</span>Sprachanrufe werden aktuell an diesen Empf&auml;nger weitergeleitet.</li>");
            sb.AppendLine("  <li><span class='material-icons-outlined'>phone_disabled</span>Dieser Empf&auml;nger bekommt keine Sprachanrufe.</li>");
            sb.AppendLine($" <li><span class='material-icons-outlined'>loyalty</span>Dieser Empf&auml;nger wird bei allen eingehenden Nachrichten per Email benachrichtigt. Zurzeit gibt es {Sql.PermanentEmailRecievers} Abonnenten.</li>");
            sb.AppendLine("</ul></div>");

            sb.AppendLine("</div><table class='w3-table w3-margin'>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <th colspan='2'>Level</th>");
            sb.AppendLine("  <th>Rolle</th>");
            sb.AppendLine("  <th>Funktion</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td>&gt;=</td>");
            sb.AppendLine($" <td>{Sql.Level_Admin}</td>");
            sb.AppendLine("  <td>Admin</td>");
            sb.AppendLine("  <td>Benutzerverwaltung, Nachrichten Sperren, Bereitschaft einteilen</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td>&gt;=</td>");
            sb.AppendLine($" <td>{Sql.Level_Reciever}</td>");
            sb.AppendLine("  <td>Benutzer</td>");
            sb.AppendLine("  <td>Eigene Benutzerverwaltung, eigene Bereitschaft bearbeiten</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td>&lt;</td>");
            sb.AppendLine($" <td>{Sql.Level_Reciever}</td>");
            sb.AppendLine("  <td>Beobachter</td>");
            sb.AppendLine("  <td>nur Anzeige</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td>=</td>");
            sb.AppendLine($" <td>0</td>");
            sb.AppendLine("  <td>Aspirant</td>");
            sb.AppendLine("  <td>ohne Zugangsberechtigung, muss durch Admin freigeschaltet werden</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</table>");

            return sb.ToString();
        }

        internal static string InfoSent()
        {

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<table class='w3-table'>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <th>Symbol</th>");
            sb.AppendLine("  <th>Bedeutung</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td><span class='material-icons-outlined'>smartphone</span></td>");
            sb.AppendLine("  <td>per SMS</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr class='w3-border-bottom'>");
            sb.AppendLine("  <td><span class='material-icons-outlined'>email</span></td>");
            sb.AppendLine("  <td>per Email</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr><hr/></tr>");

            sb.AppendLine("<tr>");
            sb.AppendLine("  <td><span class='material-icons-outlined'>check_circle_outline</span></td>");
            sb.AppendLine("  <td>Senden erfolgreich</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td><span class='material-icons-outlined'>timer</span></td>");
            sb.AppendLine("  <td>Tempor&auml;rer Fehler - versucht weiter zu senden</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td><span class='material-icons-outlined'>highlight_off</span></td>");
            sb.AppendLine("  <td>Dauerhafter Fehler - Senden fehlgeschlagen</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td><span class='material-icons-outlined'>help_outline</span></td>");
            sb.AppendLine("  <td>-Sendestatus unbekannt-</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("<div class='w3-container'><ul class='w3-ul 3-card w3-border'>");
            //sb.AppendLine($" <li>Es werden max. {Html.MaxTableRowsShow} gesendete Nachrichten angezeigt.</li>");
            sb.AppendLine(" <li>&Uuml;ber die Schaltfl&auml;che &quot;Datum w&auml;hlen&quot; lassen sich die an einem bestimmten Tag versendeten Nachrichten anzeigen.</li>");
            sb.AppendLine(" <li>Nachrichten mit den Schlagworten &quot;" + String.Join("&quot;, &quot;", Program.LifeMessageTrigger) + "&quot; werden nicht an die Bereitschaft weitergeleitet.</li>");
            sb.AppendLine("</ul></div>");

            return sb.ToString();
        }

        internal static string InfoShift()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<table class='w3-table w3-bordered'>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <th>Farbe</th>");
            sb.AppendLine("  <th>Bedeutung</th>");
            sb.AppendLine("  <th colspan='2'>Normalzeit Bereitschaft</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td class='w3-light-gray'>13.</td>");
            sb.AppendLine("  <td>Wochentag</td>");
            sb.AppendLine("  <td>Mo-Do<br/>Fr</td>");
            sb.AppendLine("  <td>17 Uhr bis Folgetag 08 Uhr<br/>15 Uhr bis Folgetag 08 Uhr</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td class='w3-sand'>13.</td>");
            sb.AppendLine("  <td>Wochenende</td>");
            sb.AppendLine("  <td>Sa-So</td>");
            sb.AppendLine("  <td>08 Uhr bis Folgetag 08 Uhr</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td class='w3-pale-red'>13.</td>");
            sb.AppendLine("  <td>Feiertag</td>");
            sb.AppendLine("  <td>&nbsp;</td>");
            sb.AppendLine("  <td>08 Uhr bis Folgetag 08 Uhr</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td class='w3-green w3-text-black'>13.</td>");
            sb.AppendLine("  <td>Heute</td>");
            sb.AppendLine("  <td></td>");
            sb.AppendLine("  <td></td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <th>Farbe</th>");
            sb.AppendLine("  <th>Bedeutung</th>");
            sb.AppendLine("  <th colspan='2'>Zuweisung Empf&auml;nger</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td><span class='w3-tag w3-pale-green'>13.</span></td>");
            sb.AppendLine("  <td>zugewiesen</td>");
            sb.AppendLine("  <td colspan='2'>Nachrichten werden an den Empf&auml;nger aus Spalte &apos;Name&apos; weitergeleitet</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td><span class='w3-tag w3-tag stripe-1'>13.</span></td>");
            sb.AppendLine("  <td>Wechsel</td>");
            sb.AppendLine("  <td colspan='2'>An diesem Tag wechselt die Bereitschaft</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("  <td><span class='w3-tag w3-light-gray w3-opacity'>13.</span></td>");
            sb.AppendLine("  <td>nicht zugewiesen</td>");
            sb.AppendLine("  <td colspan='2'>Es ist kein Empf&auml;nger namentlich zugewiesen -<br/>Nachrichten werden an das Bereitschaftshandy weitergeleitet</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td><span class='w3-tag w3-amber'>13</span></td>");
            sb.AppendLine("  <td>Doppeleintrag</td>");
            sb.AppendLine("  <td colspan='2'>Diese Kalenderwoche wurde bereits eingerichtet</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <th>Symbol</th>");
            sb.AppendLine("  <th colspan='3'>Bedeutung</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine(" <td><span class='material-icons-outlined'>email</span></td>");
            sb.AppendLine(" <td colspan='3'>Benachrichtigung per Email</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine(" <td><span class='material-icons-outlined'>smartphone</span></td>");
            sb.AppendLine(" <td colspan='3'>Benachrichtigung per SMS</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine(" <td><span class='material-icons-outlined'>report_problem</span></td>");
            sb.AppendLine("  <td colspan='3'>Kein Benachrichtigungsweg aktiv (keine Weiterleitung)</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine(" <td><span class='material-icons-outlined'>edit</span></td>");
            sb.AppendLine("  <td colspan='3'>Öffnet eine Maske zur &Auml;nderung des Datensatzes</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("<div class='w3-container'><ul class='w3-ul 3-card w3-margin-top'>");
            sb.AppendLine("<li><b>Organisation</b></li>");
            sb.AppendLine("<li>Die Bereitschaft ist Kalenderwochenweise organisiert.<br/>Sie soll gew&ouml;hnlich von Montag 17 Uhr bis zum folgenden Montag 8 Uhr gehen.</li>");
            sb.AppendLine("<li>Es lassen sich individuelle Zeiten einrichten. Sind mehre Empf&auml;nger in einer Kalenderwoche eingerichtet, wird der Eintrag 'KW' farblich hervorgehoben.</li>");
            sb.AppendLine("<li>Andere zeitliche L&uuml;cken oder &Uuml;berschneidungen werden <b>nicht</b> gesondert hervorgehoben. Alle &Auml;nderungen liegen in der Verantwortung des jeweiligen Benutzers.</li>");
            sb.AppendLine("<li>Benutzer k&ouml;nnen eine neue Kalenderwoche einrichten oder eigene Zeiten bearbeiten.<br/>Administratoren k&ouml;nnen auch Zeiten anderer Benutzer &auml;ndern.</li>");
            sb.AppendLine("<li>Hinweis zu &Auml;nderungen: Eintr&auml;ge mit einer Dauer von weniger als 1 Std. sind ung&uuml;ltig und werden abgelehnt.<br/>Eintr&auml;ge k&ouml;nnen nur von Administratoren gel&ouml;scht werden.</li>");
            sb.AppendLine("<li>Die Weiterleitung von Sprachanrufen wird nur zur vollen Stunde gewechselt.</li>");

            sb.AppendLine("</ul></div>");

            return sb.ToString();
        }

        internal static string InfoRecieved(bool isAdmin)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<div class='w3-container'><ul class='w3-ul 3-card'>");
            sb.AppendLine(" <li>Hier werden die zuletzt empfangenen Nachrichten angezeigt.</li>");
            sb.AppendLine(" <li>&Uuml;ber die Schaltfl&auml;che <q>Datum w&auml;hlen</q> lassen sich die an einem bestimmten Tag eingegangenen Nachrichten anzeigen.</li>");
            sb.AppendLine(" <li>&Uuml;ber das unten stehenden Feld <q>Nachrichten dieses Absenders auflisten</q> kann die Liste aller Nachrichten eines beliebigen Absenders angezeigt werden.</li>");

            if (isAdmin) sb.AppendLine(" <li>Mit dem Button <span class='material-icons-outlined'>edit</span> &ouml;ffnet sich die Maske zum Sperren der nebenstehenden Nachricht.</li>");

            sb.AppendLine(" <li>Diese Seite wird nach 5 Minuten neu geladen, um neue Nachrichten anzuzeigen. Die Funktion kann mit dem Schalter am Ende der Seite ausgeschaltet werden.</li>");
            sb.AppendLine("</ul></div>");
            return sb.ToString();
        }

        internal static string InfoBlocked(bool isAdmin)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<div class='w3-container'><ul class='w3-ul 3-card'>");
            sb.AppendLine(" <li>Die hier angezeigten Nachrichten werden zu den angehakten Wochentagen <b>nicht</b> an die Bereitschaft weitergeleitet.</li>");
            sb.AppendLine(" <li>Liegt die Uhrzeit &apos;Beginn&apos; nach der Uhrzeit &apos;Ende&apos;, ist diese Nachricht bis zum n&auml;chsten Tag zur Uhrzeit &apos;Ende&apos; gesperrt.</li>");
            sb.AppendLine(" <li>Sind die Uhrzeit &apos;Beginn&apos; und &apos;Ende&apos; gleich, ist diese Nachricht 24 Stunden gesperrt.</li>");
            sb.AppendLine(" <li>Die Sperrzeiten k&ouml;nnen nur von Administratoren ge&auml;ndert werden.</li>");
            if (isAdmin) sb.AppendLine(" <li>Ist kein Wochentag angehakt, wird die Sperre aufgehoben.</li>");

            sb.AppendLine("</ul></div>");
            return sb.ToString();
        }

        internal static string InfoOverdue()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<table class='w3-table'>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td></td>");
            sb.AppendLine("  <td>Hier werden Sender (Anlagen) aufgelistet, die in regelm&auml;ßigen Abst&auml;nden eine Nachricht senden m&uuml;ssen (&uuml;berwachte Sender).</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td></td>");
            sb.AppendLine("  <td>Liegt der letzte Eingang einer Nachricht eines dieser &uuml;berwachten Sender l&auml;nger zur&uuml;ck als in Spalte &apos;Max&nbsp;Inaktiv&apos; angegeben, ist davon auszugehen, dass der Meldeweg gest&ouml;rt ist. (&uuml;berf&auml;llige Sender)</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("<tr>");
            sb.AppendLine("  <td></td>");
            sb.AppendLine("  <td>Gibt es &uuml;berf&auml;llige Sender, werden sie hier gesondert angezeigt. Bei den &uumlberf&auml;lligen Sendern muss die Meldekette &uuml;berpr&uuml;ft werden." +
                        "<ol> " +
                        "<li>Waren im Zeitraum &apos;Max&nbsp;Inaktiv&apos; Meldungen vorhanden?</li>" +
                        "<li>St&ouml;rmeldungsweiterleitung vor Ort eingeschaltet?</li>" +
                        "<li>GSM-Modem von Visu erreichbar? Fehlermeldungen von GSM-Modem?</li>" +
                        "<li>Empfangsqualität ausreichend?</li>" +
                        "<li>Bei EMail-Versand: Kunden-IT informieren</li>" +
                        "</td>");
            sb.AppendLine("</tr>");

            sb.AppendLine("</table>");

            return sb.ToString();
        }

        internal static string InfoLogin()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<div class='w3-container'><ul class='w3-ul 3-card'>");
            sb.AppendLine(" <li>Einige Funktionen und &Auml;nderungen sind nur durch eingeloggte Benutzer m&ouml;glich.</li>");
            sb.AppendLine(" <li>Einloggen k&ouml;nnen sich nur registrierte und freigeschaltete Benutzer.<br/>Die Freischaltung muss durch einen Administrator erfolgen.</li>");
            sb.AppendLine(" <li>Bei der Registrierung sind mindestens anzugeben:<br/>- ein noch ungenutzter Benutzername<br/>- ein pers&ouml;nliches Passwort");
            sb.AppendLine("</ul></div>");
            return sb.ToString();
        }

        internal static string InfoLog()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<div class='w3-container'><ul class='w3-ul 3-card'>");
            sb.AppendLine(" <li>Hier werden Änderungen und Ereignisse protokolliert.</li>");
            sb.AppendLine($" <li>Es werden maximal {Html.MaxTableRowsShow} Eintr&auml;ge angezeigt.</li>");
            sb.AppendLine(" <li>Per Button <span class='w3-button material-icons-outlined'>filter_1</span> können die Einträge nach Prioritäten vorgewählt werden.</li>");
            sb.AppendLine("</ul></div>");
            return sb.ToString();
        }

        internal static string InfoGsm(bool isAdmin)
        {
            return Html.Modal("Sendemedien",
                "<h3>GSM-Modem</h3><div class='w3-margin'>Hier werden wichtige Parameter zum GSM-Modem angezeigt.<br/>Das GSM-Modem empf&auml;ngt und versendet SMS und sorgt f&uuml;r die Rufweiterleitung.</div>" +
                Html.Alert(4, "Reinitialisieren", "Wenn das GSM-Modem nicht richtig funktioniert, kann eine Reinitialisierung helfen.<br/>Nur Administratoren können das Modem reinitialisieren.")
                + (isAdmin ?
                "<form class='w3-margin'>" +
                Html.ButtonNew("gsm", "Reinitialisieren") +
                "<span class='w3-margin w3-opacity'>Die Reinitialisierung dauert ca. 20 Sekunden.</span></form>" : string.Empty) +
                "<br/><div class='w3-margin'>Sprachanrufe können an eine fest vergebene Nummer oder an die aktuelle Rufbereitschaft weitergelietet werden. " +
                "Die Umschaltung bei Wechsel der Rufbereitschaft erfolgt immer zur vollen Stunde.</div>" +
                "<h3>E-Mail Server</h3><div class='w3-margin'>Hier werden Informationen zu E-Mail-Empfang und -Versand angezeigt.</div>" +
                "<h3>MelBox2</h3><div class='w3-margin'>Der hier hinterlegte Kontakt ist für die technische Betreuung von MelBox2 verantwortlich.</div>"
                );
        }

        internal static string InfoNotepad()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<div class='w3-container'><ul class='w3-ul 3-card'>");
            sb.AppendLine(" <li>Hier k&ouml;nnen freie Notizen hinterlegt werden.</li>");
            sb.AppendLine(" <li>Notizen k&ouml;nnen nur vom Verfasser ge&auml;ndert werden.</li>");
            sb.AppendLine(" <li>Die Notiz muss einer Anlage bzw. einem Kunden zugeordnet werden.</li>");
            sb.AppendLine(" <li>Die Formatieurng der Eintr&auml;ge lehnt sich an <a href=\"https://de.wikipedia.org/wiki/BBCode\">BBCode</a> an.</li>");
            sb.AppendLine("</ul></div>");
            return sb.ToString();
        }

        #endregion

    }
}
