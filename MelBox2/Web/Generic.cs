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
        /// <summary>
        /// Liest Benuter anhand von Cookie aus
        /// </summary>
        /// <param name="context"></param>
        /// <param name="blockUser">true = Unbekannten Nutzern wird der weitere Zugriff verweigert</param>
        /// <returns></returns>
        internal static async Task<Person> GetLogedInUserAsync(IHttpContext context, bool blockUser = true)
        {
            Html.ReadCookies(context).TryGetValue("MelBoxId", out string guid);

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
            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Titel", titel },
                { "@Quality", Gsm.SignalQuality.ToString() },
                { "@GsmConnect", Gsm.NetworkRegistration == Gsm.Registration.Registerd ? Gsm.SignalQuality < 20 ? "signal_cellular_0_bar" : "signal_cellular_4_bar" : "signal_cellular_off"},
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

            if (dt.Rows.Count > 370) // Große Tabellen nicht sortierbar machen, da zu rechenintensiv!  
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                    html += $"<th>" +
                            $"{dt.Columns[i].ColumnName.Replace('_', ' ')}" +
                            $"</th>";
            }
            else
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                    html += $"<th class='w3-hover-sand' onclick=\"w3.sortHTML('#table1', '.item', 'td:nth-child({ i + 1 })')\">" +
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
                        "<a href='/" + root + "/" + dt.Rows[i][0].ToString() + "'><i class='material-icons-outlined'>build</i></a>" +
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
                        if (dt.Rows[i][j].ToString().Length > 0)
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

        internal static string FromShiftTable(Person user)
        {
            if (user == null) user = new Person() { Id = 0, Level = 0 };

            System.Data.DataTable dt = Sql.SelectShiftsCalendar();

            string html = Modal("Bereitschaft", InfoShift()); // "<p><input oninput=\"w3.filterHTML('#table1', '.item', this.value)\" class='w3-input' placeholder='Suche nach..'></p>\r\n";

            html += "<div class='w3-container'>";
            html += "<table class='w3-table-all'>\n";
           
            //add header row
            html += "<tr class='item'>";

            if (user.Id > 0)
            {
                html += "<th>Edit</th>";
            }

            html += "<th>Nr</th><th>Name</th><th>Via</th><th>Beginn</th><th>Ende</th><th>KW</th><th>Mo</th><th>Di</th><th>Mi</th><th>Do</th><th>Fr</th><th>Sa</th><th>So</th><th>mehr</th>";
            html += "</tr>\n";

            List<DateTime> holydays = Sql.Holydays(DateTime.Now);
            holydays.AddRange(Sql.Holydays(DateTime.Now.AddYears(1))); // Feiertage auch im kommenden Jahr anzeigen

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
                        "<a href='/shift/" + route + "'><i class='material-icons-outlined'>build</i></a>" +
                        "</td>";
                }
                else
                {
                    html += "<td>&nbsp;</td>";
                }
                #endregion

                #region Bereitschafts-Id
                html += "<td>" + shiftId + "</td>";
                #endregion

                #region Name
                html += "<td>" + contactName + "</td>";
                #endregion

                #region Sendeweg                
                bool phone = ((Sql.Via)via & Sql.Via.Sms) > 0; //= IsBitSet(via, 0); //int 1= SMS - Siehe Tabelle SendWay
                bool email = ((Sql.Via)via & Sql.Via.Email) > 0 || ((Sql.Via)via & Sql.Via.PermanentEmail) > 0; //= IsBitSet(via, 1) || IsBitSet(via, 2);  // int 2 = Email, int 4 = immerEmail - Siehe Tabelle SendWay

                html += "<td>";
                if (phone) html += "<span class='material-icons-outlined'>smartphone</span>";
                if (email) html += "<span class='material-icons-outlined'>email</span>";
                html += "</td>";
                #endregion

                #region Beginn
                //html += "<td><input Type='Date' name='Start' value='" + start.ToLocalTime().ToString("yyyy-MM-dd") +"'></td>";
                html += $"<td>{(start == DateTime.MinValue ? "&nbsp;" : start.ToLocalTime().ToShortDateString())}</td>";
                #endregion

                #region Ende
                //html += $"<td><input Type='Date' name='End' value='" + end.ToLocalTime().ToString("yyyy-MM-dd") + "'></td>";
                html += $"<td>{(end == DateTime.MinValue ? "&nbsp;" : end.ToLocalTime().ToShortDateString())}</td>";
                #endregion

                #region Kalenderwoche
                html += "<td>" + kw.ToString("00") + "</td>";
                #endregion

                #region Wochentage
                for (int j = 7; j < 14; j++)
                {
                    var dateStr = dt.Rows[i][j].ToString();
                    bool isMarked = dateStr.EndsWith("x");
                    _ = DateTime.TryParse(dateStr.TrimEnd('x') ?? "", out DateTime date);

                    html += WeekDayColor(holydays, date, isMarked);
                }               
                #endregion

                html += "<td class='w3-border-left'>" + dt.Rows[i][14] + "</td>"; // Spalte 'mehr'

                html += "</tr>\n";
            }

            html += "</table>\n";
            html += "</div>\n";

            return html;
        }

        private static string WeekDayColor(List<DateTime> holydays, DateTime date, bool isMarked)
        {
            string html = string.Empty;

            if (date == DateTime.Now.Date) //heute
                html += "<td class='w3-border-left w3-pale-green'>";
            else if (holydays.Contains(date)) //Feiertag?
                html += "<td class='w3-border-left w3-pale-red'>";
            else if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) //Wochenende ?              
                html += "<td class='w3-border-left w3-sand'>";
            else
                html += "<td class='w3-border-left'>";

            if (date == DateTime.MinValue)
                html += "&nbsp;";
            else if (isMarked)
                html += $"<span class='w3-tag w3-pale-green'>{date:dd}.</span>";
            // html += $"<text>{date.ToString("dd")}.</text>";
            else
                //html += $"<i class='w3-opacity'>{date.ToString("dd")}.</i>";
                html += $"<span class='w3-tag w3-light-gray w3-opacity'>{date:dd}.</span>";

            html += "</td>";

            return html;
        }

        internal static string Modal(string title, string body)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class='w3-container'>");
            sb.Append($"   <button onclick = \"document.getElementById('id02').style.display='block'\" class='w3-button w3-cyan w3-display-position w3-badge material-icons-outlined' title='{title}' style='top:140px;right:20px;';>info</button>");
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

            sb.Append("<div class='w3-container'><ul class='w3-ul 3-card'>");
            
            if (Sql.PermanentEmailRecievers > 0)
                sb.Append($" <li>&#9711; >{Sql.PermanentEmailRecievers}< Emailempf&auml;nger werden immer stumm benachrichtigt.</li>");
            
            sb.Append("</ul></div>");

            return sb.ToString();
        }

        internal static string InfoSent()
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("<table class='w3-table'>");
            sb.Append("<tr>");
            sb.Append("  <th>Symbol</th>");
            sb.Append("  <th>Wert</th>");
            sb.Append("  <th>Status</th>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>smartphone</span></td>");
            sb.Append("  <td>&nbsp;</td>");
            sb.Append("  <td>SMS</td>");
            sb.Append("</tr>");
            sb.Append("<tr class='w3-border-bottom'>");
            sb.Append("  <td><span class='material-icons-outlined'>email</span></td>");
            sb.Append("  <td>&nbsp;</td>");
            sb.Append("  <td>Email</td>");
            sb.Append("</tr>");
            sb.Append("<tr><hr/></tr>");
            
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>check_circle_outline</span></td>");
            sb.Append("  <td>&lt;3</td>");
            sb.Append("  <td>Senden erfolgreich</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>check_circle_outline</span></td>");
            sb.Append("  <td>1,3</td>");
            sb.Append("  <td>SMS vom ServiceCenter an Empf&auml;nger weitergeleitet, aber Empfang konnte nicht best&auml;tigt werden</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>timer</span></td>");
            sb.Append("  <td>&lt;64</td>");
            sb.Append("  <td>Temporärer Fehler - versucht weiter zu senden</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>highlight_off</span></td>");
            sb.Append("  <td>&lt;128</td>");
            sb.Append("  <td>Dauerhafter Fehler - Senden fehlgeschlagen</td>");
            sb.Append("</tr>");

            sb.Append("<tr>");
            sb.Append("  <td><span class='material-icons-outlined'>help_outline</span></td>");
            sb.Append("  <td>&gt;255</td>");
            sb.Append("  <td>-unbekannt-</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
          


            sb.Append("</table>");

            return sb.ToString();
        }

        internal static string InfoShift()
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("<table class='w3-table w3-bordered w3-cente'>");
            sb.Append("<tr>");
            sb.Append("  <th>Farbe</th>");
            sb.Append("  <th>Bedeutung</th>");
            sb.Append("  <th colspan='2'>Bereitschaft</th>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='w3-badge w3-light-gray'>13.</span></td>");
            sb.Append("  <td>Wochentag</td>");
            sb.Append("  <td>Mo-Do<br/>Fr</td>");
            sb.Append("  <td>17 Uhr bis Folgetag 08 Uhr<br/>15 Uhr bis Folgetag 08 Uhr</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='w3-badge w3-sand'>13.</span></td>");
            sb.Append("  <td>Wochenende</td>");
            sb.Append("  <td>Sa-So</td>");
            sb.Append("  <td>08 Uhr bis Folgetag 08 Uhr</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='w3-badge w3-pale-red'>13.</span></td>");
            sb.Append("  <td>Feiertag</td>");
            sb.Append("  <td>&nbsp;</td>");
            sb.Append("  <td>08 Uhr bis Folgetag 08 Uhr</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='w3-tag w3-pale-green'>13.</span></td>");
            sb.Append("  <td>belegt</td>");
            sb.Append("  <td colspan='2'>Empfänger ist zugewiesen (siehe Spalte &apos;Name&apos;)</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("  <td><span class='w3-tag w3-light-gray w3-opacity'>13.</span></td>");
            sb.Append("  <td>nicht belegt</td>");
            sb.Append("  <td colspan='2'>Empfänger ist nicht zugewiesen; geht an Bereitschaftshandy</td>");
            sb.Append("</tr>");

            sb.Append("</table>");

            return sb.ToString();
        }
 
        internal static string InfoBlocked(bool isAdmin)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class='w3-container'><ul class='w3-ul 3-card'>");
            sb.Append(" <li>&#9711; Die hier aufgelisteten Nachrichten werden zu den anhehakten Wochentagen <b>nicht</b> an die Bereitschaft weitergeleitet.</li>");
            sb.Append(" <li>&#9711; Liegt die Uhrzeit &apos;Beginn&apos; nach der Uhrzeit &apos;Ende&apos;, ist diese Nachricht bis zum Folgetag zur Uhrzeit &apos;Ende&apos; gesperrt.</li>");
            sb.Append(" <li>&#9711; Sind die Uhrzeit &apos;Beginn&apos; und &apos;Ende&apos; gleich, ist diese Nachricht 24 Stunden gesperrt.</li>");       
            
            if (isAdmin) sb.Append(" <li>&#9711; Ist kein Wochentag angehehakt, wird die Sperre aufgehoben.</li>");            
            
            sb.Append("</ul></div>");
            return sb.ToString();
        }

        internal static string InfoOverdue()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class='w3-container'><ul class='w3-ul 3-card'>");
            sb.Append(" <li>&#9711; Hier werden die &Uuml;berwachten Sender aufgelistet.</li>");
            sb.Append(" <li>&#9711; Liegt der letzte Eingang einer Nachricht dieser Sender länger als &apos;Max&nbsp;Inaktiv&apos; Stunden zurück, werden die &uumlberf&auml;lligen Sender hier aufgelistet.</li>");
            sb.Append(" <li>&#9711; Bei den &uumlberf&auml;lligen Sendern muss die Meldekette &uuml;berpr&uuml;ft werden.</li>");

            sb.Append("</ul></div>");
            return sb.ToString();
        }

    }
}
