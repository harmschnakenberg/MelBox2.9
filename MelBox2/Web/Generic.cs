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

        internal static async Task PageAsync(IHttpContext context, string titel, string body, Person user = null )
        {

            string connectIcon = Gsm.NetworkRegistration != Gsm.Registration.Registered ? "signal_cellular_off" :
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
                    payload.Add(item[0], WebUtility.UrlDecode(item[1]));
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

                html.Append($"<input name={dayName} class='w3-check' type='checkbox' {check} disabled>" + Environment.NewLine);
                html.Append($"<label>{dayName} </label>");
            }

            return html.Append("</span>").ToString();
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

        internal static string ChooseDate(string root)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("<button onclick=\"showMe('help2')\" class='w3-button w3-light-blue w3-display-position' style='top:140px;right:100px;' title='nur 1 Tag anzeigen'>Datum w&auml;hlen</button>");
            sb.Append("<div id='help2' class='w3-hide w3-container w3-center w3-pale-blue w3-padding '>");
            sb.Append($"  <form id='chooseDate' method='get' onsubmit='setDate()' action='/{root}'>");            
            sb.Append($"   <input name='datum' type='date' value='{DateTime.Now.Date:yyyy-MM-dd}'>");
            sb.Append("    <button class='w3-button w3-light-blue w3-padding type='submit' title='nur 1 Tag anzeigen'>Anzeigen</button>");
            sb.Append("   </form>");
            sb.Append("</div>");

            sb.Append("<script>");
            sb.Append("function showMe(id) {\r\n");
            sb.Append(" var x = document.getElementById(id);");
            sb.Append(" if (x.className.indexOf('w3-show') == -1)\r\n");
            sb.Append("  {");
            sb.Append("    x.className += 'w3-show';");
            sb.Append("  }\r\n");
            sb.Append("  else");
            sb.Append("  {");
            sb.Append("    x.className = x.className.replace('w3-show', '');");
            sb.Append("  }");
            sb.Append("}");

            sb.Append("</script>");

            return sb.ToString();
        }

        internal static string FormLogFilter()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("<table class='w3-table w3-bordered'>");
            sb.Append("<tr>");

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
                html = "<input class='w3-button w3-pale-yellow w3-half' name='phone' pattern='\\d{8,}' tite='Telefonummer für Weiterleitung der Sprachanrufe\\r\\n min. 8 Zahlen, keine weiteren Zeichen' placeholder='z.B. 0150123456789'>" +
                        "<input type='submit' value='Nummer zwangsweise &auml;ndern' class='w3-button w3-blue w3-quarter'>";
            else if (user.Phone.Length > 9 && ulong.TryParse(user.Phone.TrimStart('+'), out ulong _)) //nur eigene Nummer
            {
                string phoneStr = user.Phone;
                if (phoneStr.StartsWith("+"))
                    phoneStr = "0" + user.Phone.Remove(0, 3); //'+49...' -> '0...'

                html =  $"<input name='phone' type='hidden' value='{phoneStr}'>" +
                        $"<input class='w3-button w3-light-blue w3-threequarter' type='submit' value='Sprachanrufe dauerhaft an mich weiterleiten ({phoneStr})'>";
            }
            else
                html = "<p><i>F&uuml;r den angemeldeten Benutzer ist keine g&uuml;ltige Telefonnumer hinterlegt.</i></p>";

            if (Program.OverideCallForwardingNumber.Length > 0 && (isAdmin || user.Phone == Program.OverideCallForwardingNumber)) //nur Admin oder Benutzer selbst kann deaktivieren 
                html += "<input class='w3-button w3-quarter w3-sand' type='submit' formaction='/gsm/callforward/off' value='deaktivieren'>";

            return html;
        }

        #endregion


        #region Tabellendarstellung

        internal static string FromTable(System.Data.DataTable dt, bool isAuthorized, string root = "x")
        {
            string html = "<p><input oninput=\"w3.filterHTML('#table1', '.item', this.value)\" class='w3-input' placeholder='Suche nach..'></p>\r\n";

            html += "<table id='table1' class='w3-table-all'>\n";
            //add header row
            html += "<tr>";

            if (isAuthorized)
            {
                html += "<th>Edit</th>";
            }

            // int rows = ;

            if (dt.Rows.Count > 100) // Große Tabellen nicht sortierbar machen, da zu rechenintensiv!  
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                    html += $"<th>" +
                            $"{dt.Columns[i].ColumnName.Replace('_', ' ')}" +
                            $"</th>";
            }
            else
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                    html += $"<th class='w3-hover-sand' onclick=\"w3.sortHTML('#table1', '.item', 'td:nth-child({ i + 1 })')\" title='Klicken zum sortieren'>&#8645;&nbsp;" +
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
                    if (dt.Columns[j].ColumnName.StartsWith("Gesperrt"))
                    {
                        html += "<td>" + WeekDayCheckBox(int.Parse(dt.Rows[i][j].ToString())) + "</td>";
                    }
                    //else if (dt.Columns[j].ColumnName == "Empfangen" || dt.Columns[j].ColumnName == "Gesendet")
                    //{
                    //    string x = dt.Rows[i][j].ToString(); 
                    //    //if (DateTime.TryParse(x, out DateTime time))  //Zeitzone richtig?!
                    //    //    html += $"<td>{time.ToLocalTime()}</td>";
                    //    //else
                    //    html += $"<td>{x}</td>";
                    //}
                    else if (dt.Columns[j].ColumnName.StartsWith("Via"))
                    {
                        if (int.TryParse(dt.Rows[i][j].ToString(), out int via))
                        {
                            bool phone = 0 != (via & 1);
                            bool email = 0 != ((via & 2) | (via & 4));

                            html += "<td>";
                            if (phone) html += "<span class='material-icons-outlined'>smartphone</span>";
                            if (email) html += "<span class='material-icons-outlined'>email</span>";
                            html += "</td>";
                        }
                    }
                    else if (dt.Columns[j].ColumnName.StartsWith("Abo"))
                    {
                        html += "<td>";
                        if (dt.Rows[i][j].ToString().StartsWith("y"))
                            html += "<span class='material-icons-outlined'>call</span>";
                        else if (dt.Rows[i][j].ToString().StartsWith("x"))
                            html += "<span class='material-icons-outlined'>loyalty</span>";
                        html += "</td>";
                    }
                    else if (dt.Columns[j].ColumnName.Contains("Status"))
                    {
                        string val = dt.Rows[i][j].ToString();
                        string deliveryStatus = "-unbekannt-";
                        string detaildStatus = "-unbekannt-";
                        string icon = "error";

                        if (int.TryParse(val, out int confirmation))
                            deliveryStatus = Gsm.GetDeliveryStatus(confirmation, out detaildStatus, out icon);

                        html += $"<td><span class='material-icons-outlined' title='Wert: [{val}] {deliveryStatus} - {detaildStatus}'>{icon}</span></td>";
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


        internal static string FromShiftTable(Person user)
        {
            if (user == null) user = new Person() { Id = 0, Level = 0 };

            System.Data.DataTable dt = Sql.SelectShiftsCalendar();

            string html = Modal("Bereitschaft", InfoShift()); // "<p><input oninput=\"w3.filterHTML('#table1', '.item', this.value)\" class='w3-input' placeholder='Suche nach..'></p>\r\n";

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

            //add rows
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                _ = int.TryParse(dt.Rows[i]["ID"].ToString(), out int shiftId);
                _ = int.TryParse(dt.Rows[i]["PersonId"].ToString(), out int shiftContactId);
                string contactName = dt.Rows[i]["Name"].ToString();
                _ = int.TryParse(dt.Rows[i]["Via"].ToString(), out int via);
                _ = DateTime.TryParse(dt.Rows[i]["Start"].ToString(), out DateTime start);
                _ = DateTime.TryParse(dt.Rows[i]["End"].ToString(), out DateTime end);
                _ = int.TryParse(dt.Rows[i]["KW"].ToString(), out int kw);

                #region Editier-Button

                if (user.Level >= Server.Level_Admin || user.Level >= Server.Level_Reciever && (user.Id == shiftContactId || shiftId == 0))
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
                html += "<td>" + contactName + "</td>";
                #endregion

                #region Sendeweg                
                bool phone = ((Sql.Via)via & Sql.Via.Sms) > 0; //int 1= SMS - Siehe Tabelle SendWay
                bool email = ((Sql.Via)via & Sql.Via.Email) > 0 || ((Sql.Via)via & Sql.Via.PermanentEmail) > 0; //= IsBitSet(via, 1) || IsBitSet(via, 2);  // int 2 = Email, int 4 = immerEmail - Siehe Tabelle SendWay

                html += "<td>";
                if (phone) html += "<span class='material-icons-outlined' title='per SMS'>smartphone</span>";
                if (email) html += "<span class='material-icons-outlined' title='per Email'>email</span>";
                if (contactName.Length > 0 && !phone && !email) 
                    html += "<span class='material-icons-outlined' title='kein Empfangsweg'>report_problem</span>";
                html += "</td>";
                #endregion

                #region Beginn
                html += $"<td>{(start == DateTime.MinValue ? "&nbsp;" : end == DateTime.MinValue ? start.ToLocalTime().ToShortDateString() : start.ToLocalTime().ToString("g"))}</td>";
                #endregion

                #region Ende
                html += $"<td>{(end == DateTime.MinValue ? "&nbsp;" : end.ToLocalTime().ToString("g"))}</td>";
                #endregion

                #region Kalenderwoche
                html += "<td>" + kw.ToString("00") + "</td>";
                #endregion

                #region Wochentage
                //Wenn in dieser Woche die selbe Person Bereitschaft hat wie in der Vorwoche, den Bereitschaftswechesel am Montag nicht hervorheben
                bool sameAsLastWeek = i > 0 
                    && start.DayOfWeek == DayOfWeek.Monday
                    && dt.Rows[i-1]["Name"].ToString() == contactName 
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


        #endregion


        #region Infotexte
        internal static string Modal(string title, string body)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class='w3-container'>");
            sb.Append($"   <button onclick = \"document.getElementById('id02').style.display='block'\" class='w3-button w3-white w3-display-position w3-badge material-icons-outlined' title='{title}' style='top:140px;right:20px;';>info</button>");
            sb.Append("    <div id = 'id02' class='w3-modal'>");
            sb.Append("        <div class='w3-modal-content'>");
            sb.Append("            <div class='w3-container'>");
            sb.Append("             <span onclick = \"document.getElementById('id02').style.display='none'\" class='w3-button w3-display-topright' title='Fenster schlie&szlig;en'>&times;</span>");
            sb.Append($"            <div class='w3-container w3-center'><h3>{title}</h3></div>");
            sb.Append(body);
            sb.Append("             <div class='w3-container'>&nbsp;</div>");
            sb.Append("            </div>");
            sb.Append("        </div>");
            sb.Append("    </div>");
            sb.Append("</div>");
     
            return sb.ToString();
        }

        internal static string InfoAccount()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("<table class='w3-table w3-bordered'>");
            sb.Append("<tr>");
            sb.Append("  <th colspan='2'>Level</th>");
            sb.Append("  <th>Rolle</th>");
            sb.Append("  <th>Funktion</th>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td>&gt;=</td>");
            sb.Append($" <td>{Sql.Level_Admin}</td>");
            sb.Append("  <td>Admin</td>");
            sb.Append("  <td>Benutzerverwaltung, Nachrichten Sperren, Bereitschaft einteilen</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td>&gt;=</td>");
            sb.Append($" <td>{Sql.Level_Reciever}</td>");
            sb.Append("  <td>Benutzer</td>");
            sb.Append("  <td>Eigene Benutzerverwaltung, eigene Bereitschaft bearbeiten</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td>&lt;</td>");
            sb.Append($" <td>{Sql.Level_Reciever}</td>");
            sb.Append("  <td>Beobachter</td>");
            sb.Append("  <td>nur Anzeige</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td>=</td>");
            sb.Append($" <td>0</td>");
            sb.Append("  <td>Aspirant</td>");
            sb.Append("  <td>ohne Zugangsberechtigung, muss durch Admin freigeschaltet werden</td>");
            sb.Append("</tr>");
            sb.Append("</table>");

            sb.Append("<div class='w3-container w3-margin-top'><ul class='w3-ul 3-card w3-border'>");

            sb.Append($" <li><span class='material-icons-outlined'>call</span>  Sprachanrufe werden an diesen Empf&auml;nger weitergeleitet. </li>");
            sb.Append($" <li><span class='material-icons-outlined'>loyalty</span>  Dieser Empf&auml;nger wird bei allen eingehenden Nachrichten per Email benachrichtigt. </li>");

            if (Sql.PermanentEmailRecievers > 0)
                sb.Append($" <li>Zurzeit gibt es >{Sql.PermanentEmailRecievers}< abonenten.</li>");
            
            sb.Append(" <li><span class='material-icons-outlined'>edit</span> &Ouml;ffnet die Maske zum &Auml;ndern des Benutzerkontos.</li>");

            sb.Append("</ul></div>");

            return sb.ToString();
        }

        internal static string InfoSent()
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("<table class='w3-table'>");
            sb.Append("<tr>");
            sb.Append("  <th>Symbol</th>");            
            sb.Append("  <th>Bedeutung</th>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>smartphone</span></td>");            
            sb.Append("  <td>per SMS</td>");
            sb.Append("</tr>");
            sb.Append("<tr class='w3-border-bottom'>");
            sb.Append("  <td><span class='material-icons-outlined'>email</span></td>");            
            sb.Append("  <td>per Email</td>");
            sb.Append("</tr>");
            sb.Append("<tr><hr/></tr>");
            
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>check_circle_outline</span></td>");            
            sb.Append("  <td>Senden erfolgreich</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>timer</span></td>");            
            sb.Append("  <td>Tempor&auml;rer Fehler - versucht weiter zu senden</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>highlight_off</span></td>");            
            sb.Append("  <td>Dauerhafter Fehler - Senden fehlgeschlagen</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>help_outline</span></td>");            
            sb.Append("  <td>-Sendestatus unbekannt-</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");          
            sb.Append("</table>");

            sb.Append("<div class='w3-container'><ul class='w3-ul 3-card w3-border'>");
            sb.Append($" <li>Es werden max. {Html.MaxTableRowsShow} gesendete Nachrichten angezeigt.</li>");
            sb.Append(" <li>&Uuml;ber die Schaltfl&auml;che &quot;Datum w&auml;hlen&quot; lassen sich die an einem bestimmten Tag versendeten Nachrichten anzeigen.</li>");
            sb.Append("</ul></div>");

            return sb.ToString();
        }

        internal static string InfoShift()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<table class='w3-table w3-bordered'>");
            sb.Append("<tr>");
            sb.Append("  <th>Farbe</th>");
            sb.Append("  <th>Bedeutung</th>");
            sb.Append("  <th colspan='2'>Normalzeit Bereitschaft</th>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td class='w3-light-gray'>13.</td>");
            sb.Append("  <td>Wochentag</td>");
            sb.Append("  <td>Mo-Do<br/>Fr</td>");
            sb.Append("  <td>17 Uhr bis Folgetag 08 Uhr<br/>15 Uhr bis Folgetag 08 Uhr</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td class='w3-sand'>13.</td>");
            sb.Append("  <td>Wochenende</td>");
            sb.Append("  <td>Sa-So</td>");
            sb.Append("  <td>08 Uhr bis Folgetag 08 Uhr</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td class='w3-pale-red'>13.</td>");
            sb.Append("  <td>Feiertag</td>");
            sb.Append("  <td>&nbsp;</td>");
            sb.Append("  <td>08 Uhr bis Folgetag 08 Uhr</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("<tr>");
            sb.Append("  <td class='w3-green w3-text-black'>13.</td>");
            sb.Append("  <td>Heute</td>");
            sb.Append("  <td></td>");
            sb.Append("  <td></td>");
            sb.Append("</tr>");
            sb.Append("<tr>");  
            sb.Append("  <th>Farbe</th>");
            sb.Append("  <th>Bedeutung</th>");
            sb.Append("  <th colspan='2'>Zuweisung Empf&auml;nger</th>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='w3-tag w3-pale-green'>13.</span></td>");
            sb.Append("  <td>zugewiesen</td>");
            sb.Append("  <td colspan='2'>Nachrichten werden an den Empf&auml;nger aus Spalte &apos;Name&apos; weitergeleitet</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='w3-tag w3-tag stripe-1'>13.</span></td>");
            sb.Append("  <td>Wechsel</td>");
            sb.Append("  <td colspan='2'>An diesem Tag wechselt die Bereitschaft</td>");
            sb.Append("</tr>");
            sb.Append("  <td><span class='w3-tag w3-light-gray w3-opacity'>13.</span></td>");
            sb.Append("  <td>nicht zugewiesen</td>");
            sb.Append("  <td colspan='2'>Es ist kein Empf&auml;nger namentlich zugewiesen -<br/>Nachrichten werden an das Bereitschaftshandy weitergeleitet</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <th>Symbol</th>");
            sb.Append("  <th colspan='3'>Bedeutung</th>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append(" <td><span class='material-icons-outlined'>email</span></td>");
            sb.Append(" <td colspan='3'>Benachrichtigung per Email</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append(" <td><span class='material-icons-outlined'>smartphone</span></td>");
            sb.Append(" <td colspan='3'>Benachrichtigung per SMS</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append(" <td><span class='material-icons-outlined'>report_problem</span></td>");
            sb.Append("  <td colspan='3'>Kein Benachrichtigungsweg aktiv (keine Weiterleitung)</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append(" <td><span class='material-icons-outlined'>edit</span></td>");
            sb.Append("  <td colspan='3'>Öffnet eine Maske zur &Auml;nderung des Datensatzes</td>");
            sb.Append("</tr>");
            sb.Append("</table>");

            sb.Append("<div class='w3-container'><ul class='w3-ul 3-card w3-border w3-margin-top'>");
            sb.Append("<li><b>Organisation</b></li>");
            sb.Append("<li>Die Bereitschaft ist Kalenderwochenweise organisiert.<br/>Sie soll gew&ouml;hnlich von Montag 17 Uhr bis zum folgenden Montag 8 Uhr gehen.</li>");
            sb.Append("<li>Es lassen sich individuelle Zeiten einrichten. Zeitliche L&uuml;cken oder &Uuml;berschneidungen werden <b>nicht</b> gesondert hervorgehoben. Alle &Auml;nderungen liegen in der Verantwortung des jeweiligen Benutzers.</li>");
            sb.Append("<li>Benutzer k&ouml;nnen eine neue Kalenderwoche einrichten oder eigene Zeiten bearbeiten.<br/>Administratoren k&ouml;nnen auch Zeiten anderer Benutzer &auml;ndern.</li>");
            sb.Append("<li>Hinweis zu &Auml;nderungen: Eintr&auml;ge mit einer Dauer von weniger als 1 Std. sind ung&uuml;ltig und werden abgelehnt.</li>");
            sb.Append("<li>Hinweis zum L&ouml;schen: Eintr&auml;ge k&ouml;nnen nur von Administratoren gel&ouml;scht werden.</li>");
            

          sb.Append("</ul></div>");

            return sb.ToString();
        }
 
        internal static string InfoRecieved(bool isAdmin)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class='w3-container'><ul class='w3-ul 3-card w3-border'>");
            sb.Append($" <li>Hier werden die zuletzt empfangenen {Html.MaxTableRowsShow} Nachrichten angezeigt.</li>");
            sb.Append(" <li>&Uuml;ber die Schaltfl&auml;che &quot;Datum w&auml;hlen&quot; lassen sich die an einem bestimmten Tag eingegangenen Nachrichten anzeigen.</li>");

            if (isAdmin) sb.Append(" <li>Mit dem Button <span class='material-icons-outlined'>edit</span> &ouml;ffnet sich die Maske zum Sperren der nebenstehenden Nachricht.</li>");

            sb.Append("</ul></div>");
            return sb.ToString();
        }

        internal static string InfoBlocked(bool isAdmin)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class='w3-container'><ul class='w3-ul 3-card w3-border'>");
            sb.Append(" <li>Die hier angezeigten Nachrichten werden zu den anhehakten Wochentagen <b>nicht</b> an die Bereitschaft weitergeleitet.</li>");
            sb.Append(" <li>Liegt die Uhrzeit &apos;Beginn&apos; nach der Uhrzeit &apos;Ende&apos;, ist diese Nachricht bis zum n&auml;chsten Tag zur Uhrzeit &apos;Ende&apos; gesperrt.</li>");
            sb.Append(" <li>Sind die Uhrzeit &apos;Beginn&apos; und &apos;Ende&apos; gleich, ist diese Nachricht 24 Stunden gesperrt.</li>");
            sb.Append(" <li>Die Sperrzeiten k&ouml;nnen nur von Administratoren ge&auml;ndert werden.</li>");
            if (isAdmin) sb.Append(" <li>Ist kein Wochentag angehehakt, wird die Sperre aufgehoben.</li>");            
            
            sb.Append("</ul></div>");
            return sb.ToString();
        }

        internal static string InfoOverdue()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<table class='w3-table w3-bordered'>");
            sb.Append("<tr>");
            sb.Append("  <td></td>");
            sb.Append("  <td>Hier werden Sender (Anlagen) aufgelistet, die in regelm&auml;ßigen Abst&auml;nden eine Nachricht senden m&uuml;ssen (&uuml;berwachte Sender).</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td></td>");
            sb.Append("  <td>Liegt der letzte Eingang einer Nachricht eines dieser &uuml;berwachten Sender l&auml;nger zur&uuml;ck als in Spalte &apos;Max&nbsp;Inaktiv&apos; angegeben, ist davon auszugehen, dass der Meldeweg gest&ouml;rt ist. (&uuml;berf&auml;llige Sender)</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td></td>");
            sb.Append("  <td>Gibt es &uuml;berf&auml;lligen Sender, werden sie hier gesondert angezeigt. Bei den &uumlberf&auml;lligen Sendern muss die Meldekette &uuml;berpr&uuml;ft werden." +
                        "<ol> " +
                        "<li>Waren im Zeitraum &apos;Max&nbsp;Inaktiv&apos; Meldungen vorhanden?</li>" +
                        "<li>St&ouml;rmeldungsweiterleitung vor Ort eingeschaltet?</li>" +
                        "<li>GSM-Modem von Visu erreichbar? Fehlermeldungen von GSM-Modem?</li>" +                        
                        "<li>Empfangsqualität ausreichend?</li>" +
                        "<li>Bei EMail-Versand: Kunden-IT informieren</li>" +
                        "</td>");
            sb.Append("</tr>");

            sb.Append("</table>");

            return sb.ToString();
        }

        internal static string InfoLogin()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("<div class='w3-container'><ul class='w3-ul 3-card w3-border'>");
            sb.Append(" <li>Einige Funktionen und &Auml;nderungen sind nur durch eingeloggte Benutzer m&ouml;glich.</li>");
            sb.Append(" <li>Einloggen k&ouml;nnen sich nur registrierte und freigeschaltete Benutzer.<br/>Die Freischaltung muss durch einen Administrator erfolgen.</li>");
            sb.Append(" <li>Bei der Registrierung sind mindestens anzugeben:<br/>- ein noch ungenutzter Benutzername<br/>- ein pers&ouml;nliches Passwort");
            sb.Append("</ul></div>");
            return sb.ToString();
        }

        internal static string InfoLog()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("<div class='w3-container'><ul class='w3-ul 3-card w3-border'>");
            sb.Append(" <li>Hier werden Änderungen und Ereignisse protokolliert.</li>");
            sb.Append($" <li>Es werden maximal {Html.MaxTableRowsShow} Eintr&auml;ge angezeigt.</li>");
            sb.Append("</ul></div>");
            return sb.ToString();
        }

        internal static string InfoGsm(bool isAdmin)
        {
            return Html.Modal("GSM-Modem",
                "<div class='w3-margin'>Hier werden wichtige Parameter zum GSM-Modem angezeigt.<br/>Das GSM-Modem empf&auml;ngt und versendet SMS und sorgt f&uuml;r die Rufweiterleitung.</div>" +
                Html.Alert(4, "Reinitialisieren", "Wenn das GSM-Modem nicht richtig funktioniert, kann eine Reinitialisierung helfen.<br/>Nur Administratoren können das Modem reinitialisieren.")
                + (isAdmin ?
                "<form class='w3-margin'>" +
                Html.ButtonNew("gsm", "Reinitialisieren") +
                "<span class='w3-margin w3-opacity'>Die Reinitialisierung dauert ca. 20 Sekunden.</span></form>" : string.Empty) +
                "<br/><div class='w3-margin'>Sprachanrufe können an eine fest vergebene Nummer oder an die aktuelle Rufbereitschaft weitergelietet werden. " +
                "Die Umschaltung bei Wechsel der Rufbereitschaft erfolgt immer zur vollen Stunde.</div>"
                );
        }

        #endregion
  
    }
}
