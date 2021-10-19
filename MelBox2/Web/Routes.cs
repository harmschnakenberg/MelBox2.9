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
        #region Modem
        [RestRoute("Get", "/gsm")]
        public static async Task ModemShow(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context, false);
            bool isAdmin = (user != null && user.Level >= Server.Level_Admin);

            string info = Html.Modal("GSM-Modem",
                "<div class='w3-margin'>Hier werden wichtige Parameter zum GSM-Modem angezeigt.<br/>Das GSM-Modem empf&auml;ngt und versendet SMS und sorgt f&uuml;r die Rufweiterleitung.</div>"+
                Html.Alert(4, "Reinitialisieren", "Wenn das GSM-Modem nicht richtig funktioniert, kann eine Reinitialisierung helfen.<br/>Nur Administratoren können das Modem reinitialisieren.")
                + (isAdmin ? 
                "<form class='w3-margin'>" + 
                Html.ButtonNew("gsm", "Reinitialisieren") + 
                "<span class='w3-margin w3-opacity'>Die Reinitialisierung dauert ca. 20 Sekunden.</span></form>" : string.Empty)
                );

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
                { "@ForewardingNumber" ,  Gsm.CallForwardingNumber.Length > 0 ? Gsm.CallForwardingNumber : "-unbekannt-" },
                { "@ForewardingActive", $"<i class='material-icons-outlined' title={(Gsm.CallForwardingActive ? "'Rufweiterleitung aktiv'> phone_forwarded" : "'keine Rufweiterleitung'>phone_disabled")}</i>" },
                { "@PinStatus" , Gsm.SimPinStatus},
                { "@ModemError", Gsm.LastError == null ? "-kein Fehler-" : $"{Gsm.LastError.Item1}: {Gsm.LastError.Item2}" },
                { "@AdminPhone", Gsm.AdminPhone},
                { "@AdminEmail", Email.Admin.Address }                
            };

            string html = Html.Page(Server.Html_FormGsm, pairs);

            await Html.PageAsync(context, "GSM-Modem", html);
        }

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
        #endregion


        #region Nachrichten


        [RestRoute("Get", "/in")]
        public static async Task InBox(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context, false);
            
            bool isAdmin = user != null && user.Level >= Server.Level_Admin;
            #endregion

            System.Data.DataTable rec;
            if (context.Request.QueryString.HasKeys() && DateTime.TryParse(context.Request.QueryString.Get("datum"), out DateTime oneDate))            
                rec = Sql.SelectLastRecieved(oneDate);    
            else if (context.Request.QueryString.HasKeys() && context.Request.QueryString.Get("datum") == "heute") //nicht dokumentiert! http://elektro5:1234/in?datum=heute als bookmark
                rec = Sql.SelectLastRecieved(DateTime.Now.Date);
            else
                rec = Sql.SelectLastRecieved(Html.MaxTableRowsShow);

            string table = Html.Modal("Empfangene Nachrichten", Html.InfoRecieved(isAdmin));
            table += Html.ChooseDate("in");
            table += Html.FromTable(rec, isAdmin, "in");
           

            await Html.PageAsync(context, "Empfangene Nachrichten", table, user);
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
            System.Data.DataTable sent;
            if (context.Request.QueryString.HasKeys() && DateTime.TryParse(context.Request.QueryString.Get("datum"), out DateTime oneDate))
               // if (context.Request.PathParameters.ContainsKey("Date") && DateTime.TryParse(context.Request.PathParameters["Date"].ToString(), out DateTime oneDate))
                sent = Sql.SelectLastSent(oneDate);
            else
                sent = Sql.SelectLastSent(Html.MaxTableRowsShow);

            string table = Html.Modal("Sendestatus", Html.InfoSent());
            table += Html.ChooseDate("out");
            table += Html.FromTable(sent, false);

            await Html.PageAsync(context, "Gesendete Nachrichten", table);
        }

        [RestRoute("Get", "/overdue")]
        public static async Task OverdueShow(IHttpContext context)
        {
            System.Data.DataTable overdue = Sql.SelectOverdueSenders();

            string html;
            if (overdue.Rows.Count == 0)
            {
                html = Html.Alert(3, "Keine Zeit&uuml;berschreitung", "Kein &uuml;berwachter Sender ist &uuml;berf&auml;llig: Kein Handlungsbedarf.");                
            }
            else
            {
                html = Html.Alert(1, "Zeit&uuml;berschreitung", "Diese Absender haben l&auml;nger keine Nachricht geschickt. Bitte Meldeweg &Uuml;berpr&uuml;fen.");
                html += Html.FromTable(overdue, false);
                html += "<hr/>";
            }

            html += Html.FromTable(Sql.SelectWatchedSenders(), false);

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

            if (context.Request.PathParameters.TryGetValue("id", out string idStr))
            {
                _ = int.TryParse(idStr, out showId);
            }

            Person account = Sql.SelectPerson(showId);
            #endregion

            bool viaSms = (account.Via & Sql.Via.Sms) > 0;
            bool viaEmail = (account.Via & Sql.Via.Email) > 0;
            bool viaAlwaysEmail = (account.Via & Sql.Via.PermanentEmail) > 0;

            string userRole = "Aspirant";
            if (account.Level >= Server.Level_Admin) userRole = "Admin";
            else if (account.Level >= Server.Level_Reciever) userRole = "Benutzer";
            else if (account.Level > 0) userRole = "Beobachter";

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@readonly", isAdmin ? string.Empty : "readonly" },
                { "@disabled", isAdmin ? string.Empty : "disabled" },
                { "@Id", account.Id.ToString() },
                { "@Name", account.Name },
                { "@Accesslevel", account.Level.ToString() },
                { "@UserRole", userRole },
                { "@UserAccesslevel", user.Level.ToString() },
                { "@Company", account.Company },                
                { "@viaEmail", viaEmail ? "checked" : string.Empty },
                { "@viaAlwaysEmail", viaAlwaysEmail ? "checked" : string.Empty },
                { "@Email", account.Email },
                { "@viaPhone", viaSms ? "checked" : string.Empty },
                { "@Phone", account.Phone },
                { "@MaxInactiveHours", account.MaxInactive.ToString() },
                { "@KeyWord", account.KeyWord },

                { "@NewContact", isAdmin ? Html.ButtonNew("account") : string.Empty },
                { "@DeleteContact", isAdmin ? Html.ButtonDelete("account", account.Id) : string.Empty}
            };

            string form = Html.Page(Server.Html_FormAccount, pairs);
            string table = Html.FromTable(Sql.SelectViewablePersons(user), true, "account");
            string info = Html.Modal("Benutzerkategorien", Html.InfoAccount());

            await Html.PageAsync(context, "Benutzerkonto", info + table + form, user);            
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
                Sql.InsertLog(2, "Der Kontakt >" + p.Name + "< wurde neu erstellt durch >" + user.Name + "< [" + user.Level + "]");
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
                Sql.InsertLog(2, "Der Kontakt [" + p.Id + "] >" + p.Name + "< wurde geändert durch >" + user.Name + "< [" + user.Level + "]");
            }
            else
                alert = Html.Alert(1, "Fehler beim speichern des Kontakts", "Der Kontakt [" + p.Id + "] " + p.Name + " konnte in der Datenbank nicht geändert werden.");

            string table = Html.FromTable(Sql.SelectViewablePersons(user), true, "account");

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
                        string text = $"Der Benutzer [{deleteId}] >{dP.Name}< >{dP.Company}< wurde durch [{user.Id}] >{user.Name}< mit Berechtigung >{user.Level}< aus der Datenbank gelöscht.";
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
                Sql.InsertLog(2, $"Neuer Benutzer >{p_new.Name}< im Web-Portal registriert.");
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
                Sql.InsertLog(3, $"Login {level} [{user.Id}] >{user.Name}<");
            }

            string alert = Html.Alert(prio, titel, text);

            await Html.PageAsync(context, titel, alert, user);
        }
        #endregion


        #region Bereitschaftszeiten
        [RestRoute("Get", "/shift")]
        public static async Task ShiftShow(IHttpContext context)
        {            
            Person user = await Html.GetLogedInUserAsync(context, false);           
            string table = Html.FromShiftTable(user);
            string shiftActive = $"<span class='material-icons-outlined w3-display-topmiddle w3-text-blue w3-xxlarge' style='Top:90px;' " + (Sql.IsWatchTime() ? "title='Benachrichtigungen an Rufannahme aktiv'>notifications" : "title='zur Zeit werden keine Benachrichtungen weitergeleitet'>notifications_paused") + "</span>";

            await Html.PageAsync(context, "Bereitschaft Rufannahme", shiftActive + table, user);
        }

        [RestRoute("Get", "/shift/{shiftId:num}")]
        public static async Task ShiftFromId(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;

            string contactOptions = Sql.HtmlOptionContacts(user);

            var shiftIdStr = context.Request.PathParameters["shiftId"];
            _ = int.TryParse(shiftIdStr, out int shiftId);

            Shift shift = Sql.GetShift(Sql.SelectShift(shiftId));

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Id", shift.Id.ToString() },
                { "@ContactOptions", contactOptions },
                { "@MinDate",  DateTime.UtcNow.Date.ToString("yyyy-MM-dd") },
                { "@StartDate", shift.StartUtc.ToLocalTime().ToString("yyyy-MM-dd") },
                { "@EndDate", shift.EndUtc.ToLocalTime().ToString("yyyy-MM-dd") },
                { "@StartTime", shift.StartUtc.ToLocalTime().ToShortTimeString() },
                { "@EndTime", shift.EndUtc.ToLocalTime().ToShortTimeString() },
                { "@Route", "update" }
            };

            string form = Html.Page(Server.Html_FormShift, pairs);

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Bereitschaft Rufannahme", table + form, user);
        }

        [RestRoute("Get", "/shift/{shiftDate}")]
        public static async Task ShiftFromDate(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;

            var shiftDateStr = context.Request.PathParameters["shiftDate"];
            _ = DateTime.TryParse(shiftDateStr, out DateTime startDate);

            string contactOptions = Sql.HtmlOptionContacts(user);

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
                { "@Route", "new" }
            };

            string form = Html.Page(Server.Html_FormShift, pairs);

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Bereitschaft Rufannahme", table + form, user);
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
                    Sql.InsertLog(3, $"Bereitschaft [{shift.Id}] von {shift.StartUtc.ToLocalTime():g} bis {shift.EndUtc.ToLocalTime():g} ({shiftHours} Std.) für >{shiftName}< wurde geändert durch >{user.Name}<");
                }
                else                
                    alert = Html.Alert(2, "Fehler beim Ändern der Bereitschaft", "Es wurden ungültige Parameter übergeben.");                
            } 
            else if (user.Level >= Server.Level_Admin )
            {
                if (Sql.DeleteShift(shift.Id))
                {
                    alert = Html.Alert(1, "Bereitschaft gelöscht", $"Die Bereitschaft Nr. {shift.Id} von {shift.StartUtc.ToLocalTime():g} bis {shift.EndUtc.ToLocalTime():g} wurde gelöscht.");
                    Sql.InsertLog(3, $"Bereitschaft [{shift.Id}] für >{shiftName}< wurde gelöscht durch >{user.Name}<");
                }
                else
                    alert = Html.Alert(2, "Fehler beim Löschen der Bereitschaft", "Es wurden ungültige Parameter übergeben.");
            }
            else
                alert = Html.Alert(2, "Fehler beim Ändern der Bereitschaft", "Sie haben keine Berechtigung für die Änderung der Bereitschaft " +
                    $"Nr. {shift.Id} von {shift.StartUtc.ToLocalTime():g} bis {shift.EndUtc.ToLocalTime():g} ({shiftHours} Std.) für {shiftName}. " +
                    $"{(shiftHours > 0 ? string.Empty : "Nur Administratoren können Einträge löschen.")}");

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Bereitschaftszeit geändert", alert + table, user);
        }

        [RestRoute("Get", "/shift/delete/{shiftId:num}")]
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
            Person user = await Html.GetLogedInUserAsync(context, false);              
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
