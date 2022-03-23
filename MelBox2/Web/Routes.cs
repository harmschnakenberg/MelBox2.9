using Grapevine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MelBoxGsm;

namespace MelBox2
{
    [RestResource]
    class Routes
    {



        #region Nachrichten


        [RestRoute("Get", "/in")]
        public static async Task InBox(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context, false);
            
            bool isAdmin = user != null && user.Level >= Server.Level_Admin;
            #endregion

            DateTime date = DateTime.Now.Date;
            string filter = string.Empty;

            if (context.Request.QueryString.HasKeys())
            {
                DateTime.TryParse(context.Request.QueryString.Get("datum"), out date);
                if (date.CompareTo(DateTime.Now.AddYears(-10)) < 0) date = DateTime.Now.Date; //Älter als 10 Jahre = ungültig

                filter = context.Request.QueryString.Get("filter");
            }

            System.Data.DataTable rec = Sql.SelectRecieved(date);
            
            string table = Html.Modal("Empfangene Nachrichten", Html.InfoRecieved(isAdmin));
            table += rec.Rows.Count > 0 ? string.Empty : Html.Alert(4, "Keine Eintr&auml;ge", $"F&uuml;r den {date.ToShortDateString()} sind keine empfangenen Nachrichten protokolliert.");
            table += Html.ChooseDate("in", date);
            table += Html.FromTable(rec, isAdmin, "in");

            string refreshScript =  "<p><div class='w3-light-grey w3-padding'><i>Die Seite wird alle 5 Minuten automatisch aktualisiert:</i>&nbsp;<span>aus</span>" +
                                    "<button class='w3-button' onclick=\"this.firstChild.innerHTML='toggle_off';\" this.firstChild.classList.add('w3-disabled'); clearTimeout(myVar);\" title='Die Seite wird alle 5 Min. neu geladen.'>" +
                                    "<span class='material-icons-outlined'>toggle_on</span></button><span>ein</span></div></p>" +
                                    "<script>setTimeout(myFunction, 300000); function myFunction() { location.reload(); }";

            if (filter?.Length > 2)  //Filter beim blättern beibehalten
                refreshScript += $"w3.filterHTML('#table1', '.item', '{filter}'); document.getElementById('tablefilter').value='{filter}';";

            refreshScript += "</script>\r\n";

            await Html.PageAsync(context, "Empfangene Nachrichten", table + refreshScript, user);
        }

        [RestRoute("Get", "/in/{recId:num}")]
        public static async Task BlockedMessage(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            var recIdStr = context.Request.PathParameters["recId"];
            _ = int.TryParse(recIdStr, out int recId);

            Message msg = Sql.SelectRecieved(recId);

            bool mo = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Monday);
            bool tu = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Tuesday);
            bool we = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Wednesday);
            bool th = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Thursday);
            bool fr = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Friday);
            bool sa = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Saturday);
            bool su = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Sunday);

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@MsgId", msg.Id.ToString() },
                { "@Message", msg.Content },
                { "@Mo", mo ? "checked" : string.Empty },
                { "@Tu", tu ? "checked" : string.Empty },
                { "@We", we ? "checked" : string.Empty },
                { "@Th", th ? "checked" : string.Empty },
                { "@Fr", fr ? "checked" : string.Empty },
                { "@Sa", sa ? "checked" : string.Empty },
                { "@Su", su ? "checked" : string.Empty },
                { "@Start", msg.BlockStart.ToString() },
                { "@End", msg.BlockEnd.ToString() }
            };

            string form = Html.Page(Server.Html_FormMessage, pairs);
            string info = Html.Modal("Gesperrte Nachrichten", Html.InfoBlocked(user.Level >= Server.Level_Admin));

            await Html.PageAsync(context, "Eingang", info + form, user);
        }



        [RestRoute("Get", "/out")]
        public static async Task OutBox(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            DateTime date = DateTime.Now.Date;
            string filter = string.Empty;

            if (context.Request.QueryString.HasKeys())
            {
                DateTime.TryParse(context.Request.QueryString.Get("datum"), out date);
                filter = context.Request.QueryString.Get("filter");
            }

            System.Data.DataTable sent = Sql.SelectSent(date);
            
            string table = Html.Modal("Sendestatus", Html.InfoSent());
            table += sent.Rows.Count > 0 ? string.Empty : Html.Alert(4, "Keine Eintr&auml;ge", $"F&uuml;r den {date.ToShortDateString()} sind keine gesendeten Nachrichten protokolliert.");
            table += Html.ChooseDate("out", date);
            table += Html.FromTable(sent, false);

            if (filter?.Length > 2) //Filter beim blättern beibehalten
                table +=  $"<script>w3.filterHTML('#table1', '.item', '{filter}'); document.getElementById('tablefilter').value='{filter}';</script>";


            await Html.PageAsync(context, "Gesendete Nachrichten", table);
        }

        [RestRoute("Get", "/overdue")]
        public static async Task OverdueShow(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            System.Data.DataTable overdue = Sql.SelectOverdueSenders();

            string html;
            if (overdue.Rows.Count == 0)
            {
                html = Html.Alert(3, "Keine Zeit&uuml;berschreitung", "Kein &uuml;berwachter Sender ist &uuml;berf&auml;llig: Kein Handlungsbedarf.");
                html += "<h3>Liste &uuml;berwachter Sender</h3>";
                html += "<p>Diese Sender werden auf regelm&auml;&szlig;ige Nachrichteneing&auml;nge &uuml;berwacht:</p>";
                html += Html.FromTable(Sql.SelectWatchedSenders(), false);
            }
            else
            {
                html = Html.Alert(1, "Zeit&uuml;berschreitung", "Diese Absender haben l&auml;nger keine Nachricht geschickt. Bitte Meldeweg &Uuml;berpr&uuml;fen.");
                html += Html.FromTable(overdue, false);
            }

            string info = Html.Modal("Sender&uuml;berwachung", Html.InfoOverdue());

            await Html.PageAsync(context, "Sender&uuml;berwachung", info + html);
        }
        #endregion


        #region Gesperrte Nachrichten
     
        [RestRoute("Get", "/blocked/{msgId:num}")]
        public static async Task InBoxBlock(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            var msgIdStr = context.Request.PathParameters["msgId"];
            _ = int.TryParse(msgIdStr, out int msgId);

            Message msg = Sql.SelectMessage(msgId);

            bool mo = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Monday);
            bool tu = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Tuesday);
            bool we = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Wednesday);
            bool th = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Thursday);
            bool fr = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Friday);
            bool sa = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Saturday);
            bool su = Html.IsBitSet(msg.BlockDays, (int)DayOfWeek.Sunday);

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@MsgId", msg.Id.ToString() },
                { "@Message", msg.Content },
                { "@Mo", mo ? "checked" : string.Empty },
                { "@Tu", tu ? "checked" : string.Empty },
                { "@We", we ? "checked" : string.Empty },
                { "@Th", th ? "checked" : string.Empty },
                { "@Fr", fr ? "checked" : string.Empty },
                { "@Sa", sa ? "checked" : string.Empty },
                { "@Su", su ? "checked" : string.Empty },
                { "@Start", msg.BlockStart.ToString() },
                { "@End", msg.BlockEnd.ToString() }
            };

            string form = Html.Page(Server.Html_FormMessage, pairs);
            string info = Html.Modal("Gesperrte Nachrichten", Html.InfoBlocked(user.Level >= Server.Level_Admin));

            await Html.PageAsync(context, "Eingang", info + form, user);
        }

        [RestRoute("Get", "/blocked")]
        public static async Task BlockedMessages(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context, false);

            bool isAdmin = user != null && user.Level >= Server.Level_Admin;
            #endregion

            System.Data.DataTable blocked = Sql.Blocked_View();
            string table = Html.FromTable(blocked, isAdmin, "blocked");
            string info = Html.Modal("Gesperrte Nachrichten", Html.InfoBlocked(isAdmin));

            await Html.PageAsync(context, "Gesperrte Nachrichten", info + table, user);
        }

        [RestRoute("Post", "/blocked/update")]
        public static async Task BlockedMessageUpdate(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            bool isAdmin = user.Level >= Server.Level_Admin;
            #endregion

            Dictionary<string, string> payload = Html.Payload(context);

            Message msg = Sql.GetMessage(payload);
            
            string alert;
            if (!Sql.UpdateMessage(msg.Id, msg.BlockDays, msg.BlockStart, msg.BlockEnd))
                alert = Html.Alert(1, "Sperrzeiten aktualisieren fehlgeschlagen", $"Die Nachricht [{msg.Id}]<p><i>{msg.Content}</i></p> konnte nicht geändert werden.");
            else if (msg.BlockDays == 0)
                alert = Html.Alert(3, "Sperrzeiten entfernt", $"Die Weiterleitung der Nachricht [{msg.Id}]<p><i>{msg.Content}</i></p> wird nicht gesperrt.");
            else
                alert = Html.Alert(2, "Sperrzeiten aktualisiert", $"Änderungen für die Nachricht [{msg.Id}]<p><i>{msg.Content}</i></p> gespeichert.");

            System.Data.DataTable sent = Sql.Blocked_View();
            string table = Html.FromTable(sent, isAdmin, "blocked");

            await Html.PageAsync(context, "Sperrzeiten aktualisiert", alert + table, user);
        }

        #endregion


        #region Benutzerkonto
        [RestRoute("Get", "/account")]
        [RestRoute("Get", "/account/{id:num}")]
        public static async Task AccountShow(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            bool isAdmin = user.Level >= Server.Level_Admin;
            #endregion

            #region Anzuzeigenden Benutzer 
            int showId = user.Id;
            string companyFilter = string.Empty;

            if (context.Request.QueryString.HasKeys())                            
                companyFilter = context.Request.QueryString.Get("company");

            if (context.Request.PathParameters.TryGetValue("id", out string idStr))
            {
                _ = int.TryParse(idStr, out showId);
            }

            Person account = Sql.SelectPerson(showId);
            #endregion

            bool viaSms = (account.Via & Sql.Via.Sms) > 0;
            bool viaEmail = (account.Via & Sql.Via.Email) > 0;
            bool viaAlwaysEmail = (account.Via & Sql.Via.PermanentEmail) > 0;
            bool onEmailWhitelist = (account.Via & Sql.Via.EmailWhitelist) > 0;

            string userRole = "Aspirant";
            if (account.Level >= Server.Level_Admin) userRole = "Admin";
            else if (account.Level >= Server.Level_Reciever) userRole = "Benutzer";
            else if (account.Level > 0) userRole = "Beobachter";

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@readonly1", isAdmin ? string.Empty : "readonly" },
                { "@disabled1", isAdmin ? string.Empty : "disabled" },
                { "@disabled2", user.Level >= Server.Level_Reciever ? string.Empty : "disabled" },
                { "@Id", account.Id.ToString() },
                { "@Name", account.Name },
                { "@Accesslevel", account.Level.ToString() },
                { "@UserRole", userRole },
                { "@UserAccesslevel", user.Level.ToString() },
                { "@Company", account.Company },                
                { "@viaEmail", viaEmail ? "checked" : string.Empty },
                { "@viaAlwaysEmail", viaAlwaysEmail ? "checked" : string.Empty },
                { "@onEmailWhitelist", onEmailWhitelist ? "checked" : string.Empty },
                { "@Email", account.Email },
                { "@viaPhone", viaSms ? "checked" : string.Empty },
                { "@Phone", account.Phone },
                { "@MaxInactiveHours", account.MaxInactive.ToString() },
                { "@KeyWord", account.KeyWord },

                { "@NewContact", isAdmin ? Html.ButtonNew("account") : string.Empty },
                { "@DeleteContact", isAdmin ? Html.ButtonDelete("account", account.Id) : Html.ButtonDeleteDisabled("Kann nur durch einen Administrator gelöscht werden.")},
                { "@UpdateContact", user.Level < Server.Level_Reciever ? string.Empty : "<button class='w3-button w3-block w3-cyan w3-section w3-padding w3-col w3-quarter w3-margin-left w3-right' type='submit'>&Auml;ndern</button>"}
            };

            string filter = isAdmin ? Html.AccountFilter("kreu") : string.Empty;
            string form = Html.Page(Server.Html_FormAccount, pairs);            
            string table = Html.FromTable(Sql.SelectViewablePersons(user, companyFilter), true, "account");
            string info = Html.Modal("Benutzerkategorien", Html.InfoAccount());

            await Html.PageAsync(context, "Benutzerverwaltung", filter + form + info + table, user);            
        }
        
        [RestRoute("Post", "/account/new")]
        public static async Task AccountCreate(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            #region Form auslesen
            Dictionary<string, string> payload = Html.Payload(context);
            #endregion

            #region Kontakt erstellen
            Person p = Sql.NewPerson(payload);
            Person p_known = Sql.SelectPerson(p.Name); // Gibt es die Person schon?
            #endregion

            string alert;
            
            if (p_known.Id > 0)
            {
                alert = Html.Alert(1, "Fehler beim speichern des Kontakts", $"Der Kontakt [{ p_known.Id}] {p_known.Name} existiert bereits in der Datenbank.");
            }
            else if (Sql.InsertPerson(p.Name, p.Password, p.Level, p.Company, p.Phone, p.Email, p.Via, p.MaxInactive))
            {
                alert = Html.Alert(3, "Neuen Kontakt gespeichert", "Der Kontakt " + p.Name + " wurde erfolgreich neu erstellt.");
                Sql.InsertLog(2, "Der Kontakt " + p.Name + " wurde neu erstellt durch " + user.Name + " [" + user.Level + "]");
            }
            else
                alert = Html.Alert(1, "Fehler beim speichern des Kontakts", "Der Kontakt " + p.Name + " konnte nicht in der Datenbank gespeichert werden.");

            await Html.PageAsync(context, "Benutzerkonto erstellen", alert, user);
        }
               
        [RestRoute("Post", "/account/update")]
        public static async Task AccountUpdate(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            #region Kontakt erstellen
            Dictionary<string, string> payload = Html.Payload(context);
            Person p = Sql.NewPerson(payload);

            //kann maximal eigenen Access-Level vergeben.
            if (p.Level > user.Level)
                p.Level = user.Level; 
            #endregion

            bool success = p.Id > 0 && Sql.UpdatePerson(p.Id, p.Name, p.Password, p.Level, p.Company, p.Phone, p.Email, (int)p.Via, p.KeyWord, p.MaxInactive);

            string alert;

            if (success)
            {
                alert = Html.Alert(3, "Kontakt gespeichert", "Der Kontakt [" + p.Id + "] " + p.Name + " wurde erfolgreich geändert.");
                Sql.InsertLog(2, "Der Kontakt [" + p.Id + "] " + p.Name + " wurde geändert durch " + user.Name + " [" + user.Level + "]");
            }
            else
                alert = Html.Alert(1, "Fehler beim speichern des Kontakts", "Der Kontakt [" + p.Id + "] " + p.Name + " konnte in der Datenbank nicht geändert werden.");

            string companyFilter = p.Company.Substring(0,3); //Filter hier sinnvoll?
            string table = Html.FromTable(Sql.SelectViewablePersons(user, companyFilter), true, "account");

            await Html.PageAsync(context, "Benutzerkonto ändern", alert + table, user);
        }

        [RestRoute("Post", "/account/delete/{id:num}")]
        public static async Task AccountDelete(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;

            if (user.Level < Server.Level_Admin)
            {
                await Home(context);
                return;
            }
            #endregion

            string html = Html.Alert(1, "Fehlerhafter Parameter", "Aufruf mit fehlerhaftem Parameter.");

            if (context.Request.PathParameters.TryGetValue("id", out string idStr))
            {
                if (user.Level < Server.Level_Admin || !int.TryParse(idStr, out int deleteId))
                {
                    html = Html.Alert(2, "Keine Berechtigung", $"Keine Berechtigung zum Löschen von Benutzern durch >{user.Name}<.");
                }
                else
                {
                    Person dP = Sql.SelectPerson(deleteId);

                    if (!Sql.DeletePerson(deleteId))
                    {
                        html = Html.Alert(2, "Löschen fehlgeschlagen", $"Löschen des Benutzers [{deleteId}] >{dP.Name}< >{dP.Company}< fehlgeschlagen.");
                    }
                    else
                    {
                        string text = $"Der Benutzer [{deleteId}] {dP.Name} ({dP.Company}) wurde durch [{user.Id}] {user.Name} mit Berechtigung {user.Level} aus der Datenbank gelöscht.";
                        html = Html.Alert(1, "Benuter gelöscht", text);
                        Sql.InsertLog(2, text);
                    }
                }
            }

            await Html.PageAsync(context, "Benutzer löschen", html, user);
        }

        [RestRoute("Post", "/register")]
        public static async Task Register(IHttpContext context)
        {
            Dictionary<string, string> payload = Html.Payload(context);
            payload.TryGetValue("name", out string name);
            payload.TryGetValue("password", out string password); //Sicherheit!?

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Name", name },
                { "@Password", password },
                { "@Company", "Kreutzträger Kältetechnik, Bremen" }
            };

            string form = Html.Page(Server.Html_FormRegister, pairs);

            await Html.PageAsync(context, "Benutzerregistrierung", form);
        }

        [RestRoute("Post", "/register/thanks")]
        public static async Task RegisterProcessing(IHttpContext context)
        {
            #region Kontakt erstellen 
            Dictionary<string, string> payload = Html.Payload(context);

            Person p_new = Sql.NewPerson(payload);
            Person p_known = Sql.SelectPerson(p_new.Name);
            
            if (p_known.Id > 0)
            {
                string error = Html.Alert(1, "Registrierung fehlgeschlagen", $"Der Benutzername {p_new.Name} ist bereits vergeben." + @"<a href='/' class='w3-bar-item w3-button w3-light-blue w3-margin'>Nochmal</a>");
                await Html.PageAsync(context, "Benutzerregistrierung fehlgeschlagen", error);
                return;
            }

            #endregion

            bool success = Sql.InsertPerson(p_new.Name, p_new.Password, p_new.Level, p_new.Company, p_new.Phone, p_new.Email, p_new.Via, p_new.MaxInactive);

            string alert;

            if (success)
            {
                alert = Html.Alert(3, $"Erfolgreich registriert", $"Willkommen {p_new.Name}!<br/> Die Registrierung muss noch durch einen Administrator bestätigt werden, bevor Sie sich einloggen können. Informieren Sie einen Administrator.");
                Sql.InsertLog(2, $"Neuer Benutzer {p_new.Name} im Web-Portal registriert.");
            }
            else
                alert = Html.Alert(1, "Registrierung fehlgeschlagen", "Es ist ein Fehler bei der Registrierung aufgetreten. Wenden Sie sich an den Administrator.");


            await Html.PageAsync(context, "Benutzerregistrierung", alert);
        }

        [RestRoute("Post", "/login")]
        public static async Task Login(IHttpContext context)
        {
            Dictionary<string, string> payload = Html.Payload(context);
            string name = payload["name"];
            string password = payload["password"];
            string guid = Sql.CheckCredentials(name, password);

            Person user = new Person() { Name = name };            
            string titel = "Login fehlgeschlagen";
            string text = "Benutzername und Passwort prüfen.<br/>Neue Benutzer müssen freigeschaltet sein.<br/>" + @"<a href='/' class='w3-bar-item w3-button w3-teal w3-margin'>Nochmal</a>";
            int prio = 1;

            if (guid.Length > 0)
            {
                prio = 3;
                titel = "Login ";
                string level = "Beobachter";

                System.Net.Cookie cookie = new System.Net.Cookie("MelBoxId", guid, "/");
                context.Response.Cookies.Add(cookie);

                if (Server.LogedInHash.TryGetValue(guid, out user))
                {
                    if (user.Level >= Server.Level_Admin)
                        level = "Admin";
                    else if (user.Level >= Server.Level_Reciever)
                        level = "Benutzer";
                }

                text = $"Willkommen {level} {name}";
                Sql.InsertLog(3, $"Login {level} {user.Name} [{user.Id}]");
            }

            string alert = Html.Alert(prio, titel, text);

            await Html.PageAsync(context, titel, alert, user);
        }

        [RestRoute("Get", "/login/delete")]
        public static async Task DeleteCookie(IHttpContext context)
        {
            System.Net.Cookie cookie = new System.Net.Cookie("MelBoxId", string.Empty, "/");
            context.Response.Cookies.Add(cookie);
           
            string alert = Html.Alert(4, "Cookie geleert", "Der Cookie mit Ihren codierten Anmeldeinformationen wurde geleert.");

            await Html.PageAsync(context, "Cookie geleert", alert, null);
        }

        #endregion


        #region Bereitschaftszeiten
        [RestRoute("Get", "/shift")]
        public static async Task ShiftShow(IHttpContext context)
        {            
            Person user = await Html.GetLogedInUserAsync(context, false);           
            string table = Html.FromShiftTable(user);
            string shiftActive = $"<span class='material-icons-outlined w3-display-topmiddle w3-text-blue w3-xxlarge' style='Top:90px;' " + (Sql.IsWatchTime() ? "title='Benachrichtigungen an Rufannahme aktiv'>notifications" : "title='zur Zeit werden keine Benachrichtungen weitergeleitet'>notifications_paused") + "</span>";

            await Html.PageAsync(context, "Planer Bereitschaft", shiftActive + table, user);
        }

        [RestRoute("Get", "/shift/{shiftId:num}")]
        public static async Task ShiftFromId(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            bool isAdmin = user.Level >= Server.Level_Admin;
            

            var shiftIdStr = context.Request.PathParameters["shiftId"];
            _ = int.TryParse(shiftIdStr, out int shiftId);

            Shift shift = Sql.GetShift(Sql.SelectShift(shiftId));
            string contactOptions = Sql.HtmlOptionContacts(user, shift.PersonId);

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Id", shift.Id.ToString() },
                { "@ContactOptions", contactOptions },
                { "@MinDate",  DateTime.UtcNow.Date.AddDays(-7).ToString("yyyy-MM-dd") },
                { "@StartDate", shift.StartUtc.ToLocalTime().ToString("yyyy-MM-dd") },
                { "@EndDate", shift.EndUtc.ToLocalTime().ToString("yyyy-MM-dd") },
                { "@StartTime", shift.StartUtc.ToLocalTime().ToShortTimeString() },
                { "@EndTime", shift.EndUtc.ToLocalTime().ToShortTimeString() },
                { "@Route", "update" },
                { "@DeleteShift", isAdmin ? Html.ButtonDelete("shift", shift.Id) : Html.ButtonDeleteDisabled("Kann nur durch einen Administrator gelöscht werden.")}
            };

            string form = Html.Page(Server.Html_FormShift, pairs);

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Planer Bereitschaft", table + form, user);
        }

        [RestRoute("Get", "/shift/{shiftDate}")]
        public static async Task ShiftFromDate(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;

            var shiftDateStr = context.Request.PathParameters["shiftDate"];
            _ = DateTime.TryParse(shiftDateStr, out DateTime startDate);

            string contactOptions = Sql.HtmlOptionContacts(user, user.Id);

            DateTime endDate = startDate.DayOfWeek == DayOfWeek.Monday ? startDate.Date.AddDays(7) : startDate.Date.AddDays(1);

            startDate = Sql.ShiftStartTimeUtc(startDate);
            endDate = Sql.ShiftEndTimeUtc(endDate);

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Id", string.Empty },
                { "@ContactOptions", contactOptions },
                { "@MinDate", DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd") },
                { "@StartDate", startDate.ToLocalTime().ToString("yyyy-MM-dd") },
                { "@EndDate", endDate.ToLocalTime().ToString("yyyy-MM-dd") },
                { "@StartTime", startDate.ToLocalTime().ToShortTimeString() },
                { "@EndTime", endDate.ToLocalTime().ToShortTimeString() },
                { "@Route", "new" },
                { "@DeleteShift", string.Empty}
            };

            string form = Html.Page(Server.Html_FormShift, pairs);

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Planer Bereitschaft", table + form, user);
        }

        [RestRoute("Post", "/shift/new")]
        public static async Task ShiftCreate(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;

            Dictionary<string, string> payload = Html.Payload(context);
            Shift shift = Sql.GetShift(payload);
            if (shift.PersonId == 0) shift.PersonId = user.Id;

            string alert;
            bool success = true;            
            int shiftHours = (int)shift.EndUtc.Subtract(shift.StartUtc).TotalHours;

            if (user.Level < Server.Level_Reciever)            
                alert = Html.Alert(1, "Fehler beim speichern der Bereitschaft", "Sie haben keine Berechtigung zum Erstellen dieser Bereitschaft.");            
            else if (shiftHours < 1)            
                alert = Html.Alert(1, "Fehler beim speichern der Bereitschaft", $"Die Bereitschaft vom {shift.StartUtc.ToLocalTime():g} bis {shift.EndUtc.ToLocalTime():g} ist ungültig.");
            else
            {
                List<Shift> shifts = Sql.SplitShift(shift);
                foreach (Shift splitShift in shifts)
                    if (!Sql.InsertShift(splitShift)) success = false;

                if (success)
                    alert = Html.Alert(3, "Neue Bereitschaft gespeichert", $"Neue Bereitschaft vom {shift.StartUtc.ToLocalTime():g} bis {shift.EndUtc.ToLocalTime():g} wurde erfolgreich erstellt.");
                else
                    alert = Html.Alert(1, "Fehler beim speichern der Bereitschaft", "Die Bereitschaft konnte nicht in der Datenbank gespeichert werden.");
            }
             
            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Bereitschaftszeit erstellen", alert + table, user);
        }

        [RestRoute("Post", "/shift/update")]
        public static async Task ShiftUpdate(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;

            Dictionary<string, string> payload = Html.Payload(context);
            Shift shift = Sql.GetShift(payload);
            if (shift.PersonId == 0) shift.PersonId = user.Id;

            int shiftHours = (int)shift.EndUtc.Subtract(shift.StartUtc).TotalHours;

            bool success = true;            
            string alert;
            string shiftName = Sql.SelectPerson(shift.PersonId).Name;
            List<Shift> shifts = Sql.SplitShift(shift);

            if (shiftHours > 0 && user.Level >= Server.Level_Reciever)
            {
                for (int i = 0; i < shifts.Count; i++)
                {
                    if (i == 0)
                    { if (!Sql.UpdateShift(shifts[i])) success = false; }
                    else
                    { if (!Sql.InsertShift(shifts[i])) success = false; }
                }

                if (success)
                {
                    alert = Html.Alert(3, "Bereitschaft geändert", $"Die Bereitschaft Nr. {shift.Id} von {shift.StartUtc.ToLocalTime():g} bis {shift.EndUtc.ToLocalTime():g} ({shiftHours} Std.) für {shiftName} wurde erfolgreich geändert.");
                    Sql.InsertLog(3, $"Bereitschaft [{shift.Id}] von {shift.StartUtc.ToLocalTime():g} bis {shift.EndUtc.ToLocalTime():g} ({shiftHours} Std.) für {shiftName} wurde geändert durch {user.Name}");
                }
                else
                    alert = Html.Alert(2, "Fehler beim Ändern der Bereitschaft", "Es wurden ungültige Parameter übergeben.");
            }
            else
                alert = Html.Alert(2, "Fehler beim Ändern der Bereitschaft", "Sie haben keine Berechtigung für diese Änderung der Bereitschaft " +
                    $"Nr. {shift.Id} von {shift.StartUtc.ToLocalTime():g} bis {shift.EndUtc.ToLocalTime():g} ({shiftHours} Std.) für {shiftName} oder es wurden ungültige Parameter übergeben.");                    

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Bereitschaftszeit geändert", alert + table, user);
        }

        [RestRoute("Post", "/shift/delete/{shiftId:num}")]
        public static async Task DeleteShiftFromId(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null || user.Level < Server.Level_Admin) return;

            var shiftIdStr = context.Request.PathParameters["shiftId"];
            _ = int.TryParse(shiftIdStr, out int shiftId);
            Shift shift = Sql.GetShift(Sql.SelectShift(shiftId));

            string alert;
            if (shift.Id == 0)            
                alert = Html.Alert(2, "Fehler Bereitschaftszeit löschen", $"Die angeforderte Bereitschaft Nr. [{shiftIdStr}] wurde nicht gefunden.");            
            else
            {
                Person p = Sql.SelectPerson(shift.PersonId);

                if (Sql.DeleteShift(shift.Id))
                {
                    string msg = $"Die Bereitschaft [{shift.Id}] von {shift.StartUtc.ToLocalTime().ToShortDateString()} {shift.StartUtc.ToLocalTime().ToShortTimeString()} bis " +
                    $"{shift.EndUtc.ToLocalTime().ToShortDateString()} {shift.EndUtc.ToLocalTime().ToShortTimeString()} f&uuml;r {p.Name} wurde gelöscht durch {user.Name}";

                    Sql.InsertLog(1, msg);
                    alert = Html.Alert(1, "Bereitschaftszeit gelöscht", msg);
                }
                else
                {
                    string msg = $"Die Bereitschaft [{shift.Id}] von {shift.StartUtc.ToLocalTime().ToShortDateString()} {shift.StartUtc.ToLocalTime().ToShortTimeString()} bis " +
                    $"{shift.EndUtc.ToLocalTime().ToShortDateString()} {shift.EndUtc.ToLocalTime().ToShortTimeString()} f&uuml;r {p.Name} konnte nicht durch {user.Name} gelöscht werden.";
                    alert = Html.Alert(2, "Fehler Bereitschaftszeit löschen", msg);
                }
            }

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Bereitschaftszeit löschen", alert + table, user);
        }

        #endregion


        #region Log
        [RestRoute("Get", "/log")]
        [RestRoute("Get", "/log/{maxPrio:num}")]
        public static async Task LoggingShow(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            int maxPrio = 3;
            if (context.Request.PathParameters.ContainsKey("maxPrio"))
                _ = int.TryParse(context.Request.PathParameters["maxPrio"], out maxPrio);
            
            System.Data.DataTable log = Sql.SelectLastLogs(Html.MaxTableRowsShow, maxPrio);
            string table = Html.FromTable(log, false, "");
            int del = 100;
            string html = user?.Level < 9900 ? string.Empty : $"<p><a href='/log/delete/{del}' class='w3-button w3-red w3-display-position' style='top:140px;right:100px;'>Bis auf letzten {del} Eintr&auml;ge alle l&ouml;schen</a></p>\r\n";

            html += Html.Modal("Ereignisprotokoll", Html.InfoLog());

            await Html.PageAsync(context, "Log", table + html, user);
        }


        [RestRoute("Get", "/log/delete/{del}")]
        public static async Task LoggingDelete(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            bool isAdmin = user.Level >= Server.Level_Admin;
            #endregion

            string html = string.Empty;

            if (isAdmin)
            {
                if (int.TryParse(context.Request.PathParameters["del"].ToString(), out int del))
                {                   
                    if (Sql.DeleteLogExeptLast(del))
                    {
                        string text = $"Log-Einträge bis auf letzten {del} Einträge gelöscht durch [{user.Id}] >{user.Name}<.";
                        html = Html.Alert(2, "Log-Einträge gelöscht.", text);
                        Sql.InsertLog(2, text);
                    }
                    else
                    {
                        html = Html.Alert(2, "Keine Log-Einträge gelöscht.", "Keine passenden Einträge zum löschen gefunden.");
                    }
                }
            }

            html += Html.Modal("Ereignisprotokoll", Html.InfoLog());

            System.Data.DataTable log = Sql.SelectLastLogs(Html.MaxTableRowsShow);
            string table = Html.FromTable(log, false, "");

            await Html.PageAsync(context, "Log", html + table, user);
        }


        [RestRoute("Get", "/notepad")]
        [RestRoute("Get", "/notepad/{noteId:num}")]
        public static async Task SingleNote(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            string form = string.Empty;

            if (context.Request.PathParameters.ContainsKey("noteId") && int.TryParse(context.Request.PathParameters["noteId"], out int noteId))
            {
                string author = user.Name;
                string content = string.Empty;
                string updateButton = string.Empty;
                int selectedCustomerId = 0;

                System.Data.DataTable note = Sql.SelectNote(noteId);

                if (note.Rows.Count > 0)
                {
                    author = note.Rows[0]["Von"].ToString();
                    content = note.Rows[0]["Notiz"].ToString();
                    selectedCustomerId = int.Parse(note.Rows[0]["KundeId"]?.ToString());

                    if (note.Rows[0]["VonId"]?.ToString() == user.Id.ToString())
                        updateButton = "<button class='w3-button w3-cyan w3-padding w3-margin w3-quarter' type='submit' formaction='/notepad/update'>&Auml;ndern</button>";
                }
                    
                string customerOptions = Sql.HtmlOptionCustomers(selectedCustomerId);

                Dictionary<string, string> pairs = new Dictionary<string, string>
                {
                    { "@NoteId", noteId.ToString() },
                    { "@AuthorId", user.Id.ToString() },
                    { "@Author", author },
                    { "@CustomerOptions", customerOptions },
                    { "@Content", content },
                    { "@UpdateNote", updateButton }
                };

                form = Html.Page(Server.Html_FormNote, pairs);
            }

            string table = Html.FromNotesTable(user);
            string info = Html.Modal("Notizbuch", Html.InfoNotepad());

            await Html.PageAsync(context, "Notizbuch", info + table + form, user);
        }


        [RestRoute("Post", "/notepad/new")]
        public static async Task NoteCreate(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            #region Form auslesen
            bool success = false;
            Dictionary<string, string> payload = Html.Payload(context);
            
            if (payload.TryGetValue("authorId", out string authorIdStr)
                && int.TryParse(authorIdStr, out int authorId)
                && payload.TryGetValue("customerId", out string customerIdStr)
                && int.TryParse(customerIdStr, out int customerId)
                && payload.TryGetValue("content", out string content) //Es sind HTML-Markups möglich! Sicherheitsrisiko?
                )
                success = Sql.InsertNote(authorId, customerId, content);
            #endregion

            payload.TryGetValue("author", out string author);

            string alert = string.Empty;
            if (success)
                alert += Html.Alert(3, "Notiz erstellt", $"{author} hat eine neue Notiz erstellt.") ;
            else
                alert += Html.Alert(1, "Notiz erstellen fehlgeschlagen", $"Es konnte keine neue Notiz erstellt werden.");

            string table = Html.FromNotesTable(user);

            await Html.PageAsync(context, "Notiz erstellt", alert + table, user);
        }

        [RestRoute("Post", "/notepad/update")]
        public static async Task NoteUpdate(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            #endregion

            #region Form auslesen
            bool success = false;
            Dictionary<string, string> payload = Html.Payload(context);

            if (payload.TryGetValue("noteId", out string noteIdStr)
                && int.TryParse(noteIdStr, out int noteId)
                && payload.TryGetValue("authorId", out string authorIdStr)
                && int.TryParse(authorIdStr, out int authorId)
                && payload.TryGetValue("customerId", out string customerIdStr)
                && int.TryParse(customerIdStr, out int customerId)
                && payload.TryGetValue("content", out string content) //Es sind HTML-Markups möglich! Sicherheitsrisiko?
                )
                success = Sql.UpdateNote(noteId, authorId, customerId, content);
            #endregion

            payload.TryGetValue("author", out string author);

            string alert = string.Empty;
            if (success)
                alert += Html.Alert(3, "Notiz geändert", $"{author} hat eine neue Notiz ge&auml;ndert.");
            else
                alert += Html.Alert(1, "Notiz &auml;ndern fehlgeschlagen", $"Die Notiz konnte nicht ge&auml;ndert werden.");

            string table = Html.FromNotesTable(user);

            await Html.PageAsync(context, "Notiz ge&auml;ndert", alert + table, user);
        }

        #endregion


        #region Modem & Sendemedien

        [RestRoute("Get", "/gsm/new")]
        public static async Task ModemReinit(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null || user.Level < Server.Level_Admin) return;
            #endregion

            Gsm.SetupModem();

            string html = Html.Alert(2, "GSM-Modem reinitialisiert", "Die Startprozedur für das GSM-Modem wurde ausgeführt.");

            await Html.PageAsync(context, "GSM-Modem reinitialisiert", html);
        }

        [RestRoute("Get", "/gsm/callforward")]
        public static async Task ModemCallforward(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null || user.Level < Server.Level_Reciever) return;
            #endregion

            var phoneStr = context.Request.QueryString.Get("phone");
            phoneStr = Sql.NormalizePhone(phoneStr);

            string html = string.Empty;

            if (phoneStr == null || (user.Level < Server.Level_Admin && phoneStr != user.Phone)) //Benutzer dürfen nur ihre eigene Nummer einsetzen
                html += Html.Alert(1, "Rufweiterleitung ändern fehlgeschlagen", $"Die übergebene Nummer '{phoneStr}' ist nicht zulässig.");
            else
            {
                Program.OverideCallForwardingNumber = phoneStr;
                Gsm.SetCallForewarding(phoneStr);
                System.Threading.Thread.Sleep(3000);
                html += Html.Alert(4, "Rufweiterleitung ändern", $"Sprachanrufe werden bis auf weiteres an die Nummer '{phoneStr}' weitergeleitet . Die Umstellung kann einige Sekunden dauern. <a href='/gsm'>&uuml;berpr&uuml;fen</a>");
                string txt = $"Die Rufumleitung wurde von {user.Name} [{user.Level}] bis auf weiteres umgestellt auf die Nummer '{Gsm.CallForwardingNumber}'.";

                Sql.InsertLog(2, txt);
                Log.Warning(txt, 736);
            }

            await Html.PageAsync(context, "Rufweiterleitung ändern", html);
        }

        [RestRoute("Get", "/gsm/callforward/off")]
        public static async Task ModemCallforwardOff(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null || user.Level < Server.Level_Reciever) return;
            #endregion

            string oldPhone = Gsm.CallForwardingNumber;
            string html;

            if (user.Level < Server.Level_Admin && user.Phone != oldPhone)
                html = Html.Alert(2, "Zwangsweise Rufweiterleitung deaktivieren ist fehlgeschlagen", $"Die Rufweiterleitung an '{oldPhone}' konnte nicht deaktiviert werden. Sie haben keine Berechtigung diese Nummer zu &auml;ndern.");
            else
            {
                Program.OverideCallForwardingNumber = string.Empty;
                Program.CheckCallForwardingNumber(null, null);

                html = Html.Alert(4, "Zwangsweise Rufweiterleitung deaktiviert", $"Die erzwungene Rufweiterleitung an {oldPhone} wird deaktiviert. Sprachanrufe werden an die aktuelle Bereitschaft {Gsm.CallForwardingNumber} geleitet.");
            }

            await Html.PageAsync(context, "Zwangsweise Rufweiterleitung zurücksetzen", html);
        }

        [RestRoute("Get", "/gsm/callforward/check")]
        public static async Task ModemCallforwardCheck(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null || user.Level < Server.Level_Reciever) return;
            #endregion

            Program.CheckCallForwardingNumber(null, null);
            System.Threading.Thread.Sleep(2000);

            string html = Html.Alert(4, "Aktuelle Rufweiterleitung geprüft", $"Sprachanrufe werden aktuell an {(Gsm.CallForwardingNumber.Length > 8 ? Gsm.CallForwardingNumber : "-unbekannt-")} weitergeleitet.");
            
            await Html.PageAsync(context, "Rufweiterleitung geprüft", html);
        }


        [RestRoute("Post", "/gsm/sendsms")]
        public static async Task ModemSendTestSms(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            //bool isAdmin = user.Level >= Server.Level_Admin;
            #endregion

            Dictionary<string, string> payload = Html.Payload(context);
            if (payload.TryGetValue("phone", out string phoneStr))
                phoneStr = Sql.NormalizePhone(phoneStr);

            string html;
            if (phoneStr.Length < 8)
                html = Html.Alert(1, "Nummer ungültig", $"Die übergebene Telefonnummer >{phoneStr}< ist ungültig.");
            else
            {
                Gsm.SmsSend(phoneStr, "Dies ist ein Test von MelBox2.");
                html = Html.Alert(4, "SMS versendet", $"Eine Test-SMS wurde an Telefonnummer >{phoneStr}< gesendet.");
            }

            await Html.PageAsync(context, "Test-SMS versenden", html, user);
        }


        [RestRoute("Post", "/gsm/sendemail")]
        public static async Task ModemSendTestEmail(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            //bool isAdmin = user.Level >= Server.Level_Admin;
            #endregion

            Dictionary<string, string> payload = Html.Payload(context);
            _ = payload.TryGetValue("email", out string email);

            string html;
            if (!Email.IsValidEmail(email))
                html = Html.Alert(1, "E-Mail-Adresse ungültig", $"Die übergebene E-Mail-Adresse  >{email}< ist ungültig.");
            else
            {
                Email.Send(new System.Net.Mail.MailAddress(email), "Dies ist ein Test von MelBox2.");
                html = Html.Alert(4, "E-Mail versendet", $"Eine Test-E-Mail wurde an >{email}< gesendet.");
            }

            await Html.PageAsync(context, "Test-E-Mail versenden", html, user);
        }


        [RestRoute("Post", "/gsm/sysadmin")]
        public static async Task SetSysAdmin(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;
            bool isAdmin = user.Level >= Server.Level_Admin;
            #endregion

            string html;
            Dictionary<string, string> payload = Html.Payload(context);
            if (!isAdmin)
                html = Html.Alert(1, "Anfrage ung&uuml;ltig", $"Sie besitzen nicht die erforderliche Berechtigung.");
            else if (payload.TryGetValue("hour", out string hourStr)
                && int.TryParse(hourStr, out int hour)
                && hour >= 0 && hour < 24
                && ((int)user.Via & (int)Sql.Via.Email) > 0
                && user.Email.Length > 10
                && ((int)user.Via & (int)Sql.Via.Sms) > 0
                && user.Phone.Length > 10)
            {
                Program.HourOfDailyTasks = hour;
                Email.Admin = new System.Net.Mail.MailAddress(user.Email, user.Name + " (MelBox2-Admin)");
                Gsm.AdminPhone = user.Phone;

                string txt = $"{user.Name} ist nun verantwortlicher Systemadministrator von MelBox2.\r\n" +
                    $"Tägliche Routinemeldungen werden um {Program.HourOfDailyTasks} Uhr an {Gsm.AdminPhone} und {Email.Admin} versendet.";

                Email.Send(Email.Admin, txt);
                Log.Warning(txt, 42563);
                html = Html.Alert(4, "Systemadministrator erfolgreich aktualisiert", txt.Replace(Environment.NewLine, "<br/>"));
            }
            else
                html = Html.Alert(1, "Anfrage ung&uuml;ltig", $"Die übergebene Anfrage ist ungültig oder Sie sind nicht berechtigt.<br/>Um Systemadministrator werden zu können muss der Empfang von Telefon und Email freigeschaltet sein.");


            await Html.PageAsync(context, "Systemadministrator aktualisieren", html, user);
        }


        [RestRoute("Get", "/gsm")]
        public static async Task ModemShow(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context);
            bool isAdmin = (user != null && user.Level >= Server.Level_Admin);

            string info = Html.InfoGsm(isAdmin);

            string callforward1 = Gsm.CallForwardingActive ?
                "<i class='material-icons-outlined w3-text-green' title='Rufweiterleitung aktiv'>phone_forwarded</i>" :
                "<i class='material-icons-outlined w3-text-red' title='keine Rufweiterleitung'>phone_disabled</i>";

            string callforward2 = Gsm.CallForwardingNumber == Program.OverideCallForwardingNumber ?
                "<i class='material-icons-outlined w3-text-red' title='Nummer fest hinterlegt'>phone_locked</i>" :
                string.Empty;

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@ModemReinit", info },
                { "@Quality" , Gsm.SignalQuality.ToString()},
                { "@Registered" , Gsm.NetworkRegistration.RegToString()},
                { "@ModemType", Gsm.ModemType},
                { "@OwnName", Gsm.OwnName},
                { "@OwnNumber", Gsm.OwnNumber},
                { "@ServiceCenter", Gsm.SmsServiceCenterAddress},
                { "@ProviderName" , Gsm.ProviderName},
                { "@ForewardingNumber" ,  (Gsm.CallForwardingNumber.Length > 0 ? Gsm.CallForwardingNumber : "-unbekannt-") + callforward2 },
                { "@ForewardingActive", callforward1 },
                { "@NewForwardingNumber", Html.ManualUpdateCallworwardNumber(user)},
                { "@PinStatus" , Gsm.SimPinStatus},
                { "@ModemError", Gsm.LastError == null ? "-kein Fehler-" : $"{Gsm.LastError.Item1}: {Gsm.LastError.Item2}" },
                { "@SmtpServer", Email.SmtpHost + ":" + Email.SmtpPort },
                { "@ImapServer", EmailListener.ImapServer + ":" + EmailListener.ImapPort },
                { "@AdminPhone", Gsm.AdminPhone},
                { "@AdminEmail", Email.Admin.Address },
                { "@HourSelect", Html.SelectHourOfDay(Program.HourOfDailyTasks) },
                { "@SysAdminDisabled", (isAdmin && (user.Via == Sql.Via.SmsAndEmail || user.Via == Sql.Via.PermanentEmailAndSms) ? string.Empty : "disabled") }
            };

            string html = Html.Page(Server.Html_FormGsm, pairs);

            await Html.PageAsync(context, "Sendemedien", html, user);
        }

        #endregion


        [RestRoute("Get", "/help")]
        public static async Task HelpMeShow(IHttpContext context)
        {
            string html = Html.Page(Server.Html_Help, null);

            await Html.PageAsync(context, "Melbox2 Hilfe", html);
        }

        [RestRoute]
        public static async Task Home(IHttpContext context)
        {           
            string form = Html.Modal("Login und Registrierung", Html.InfoLogin());
            form += Html.Page(Server.Html_FormLogin, null);

            await Html.PageAsync(context, "MelBox2", form);
        }

    }
}

