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
            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Quality" , Gsm.SignalQuality.ToString()},
                { "@Registered" , Gsm.NetworkRegistration.RegToString()},
                { "@ModemType", Gsm.ModemType},
                { "@OwnName", Gsm.OwnName},
                { "@OwnNumber", Gsm.OwnNumber},
                { "@ServiceCenter", Gsm.SmsServiceCenterAddress},
                { "@ProviderName" , Gsm.ProviderName},
                { "@ForewardingNumber" ,  Gsm.CallForwardingNumber.Length > 0 ? Gsm.CallForwardingNumber : "-unbekannt-" },
                { "@ForewardingActive", $"<i class='material-icons-outlined'>{(Gsm.CallForwardingActive ? "phone_forwarded" : "phone_disabled")}</i>" },
                { "@PinStatus" , Gsm.SimPinStatus},
                { "@ModemError", Gsm.LastError == null ? "-kein Fehler-" : $"{Gsm.LastError.Item1}: {Gsm.LastError.Item2}" },
                { "@AdminPhone", Gsm.AdminPhone},
                { "@AdminEmail", Email.Admin.Address }
            };

            string html = Html.Page(Server.Html_FormGsm, pairs);

            await Html.PageAsync(context, "GSM-Modem", html);
        }
        #endregion


        #region Nachrichten
        [RestRoute("Get", "/in")]
        [RestRoute("Get", "/in/{Date}")] //noch kein Link implementiert
        public static async Task InBox(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context, false);
            
            bool isAdmin = user != null && user.Level >= Server.Level_Admin;
            #endregion

            System.Data.DataTable rec;
            if (context.Request.PathParameters.ContainsKey("Date") && DateTime.TryParse(context.Request.PathParameters["Date"].ToString(), out DateTime oneDate))
                rec = Sql.SelectLastRecieved(oneDate);
            else
                rec = Sql.SelectLastRecieved(300);

            string table = Html.FromTable(rec, isAdmin, "blocked");

            await Html.PageAsync(context, "Eingang", table, user);
        }

        [RestRoute("Get", "/out")]
        public static async Task OutBox(IHttpContext context)
        {
            System.Data.DataTable sent = Sql.SelectLastSent(100);

            // string table = Html.Modal("Sendestatus", Html.InfoSent());
            string table = Html.FromTable(sent, false);

            await Html.PageAsync(context, "Ausgang", table);
        }

        [RestRoute("Get", "/overdue")]
        public static async Task OverdueShow(IHttpContext context)
        {
            System.Data.DataTable overdue = Sql.SelectOverdueSenders();

            string html;
            if (overdue.Rows.Count == 0)
            {
                html = Html.Alert(3, "Keine Zeitüberschreitung", "Kein überwachter Sender ist überfällig.");
                html += Html.FromTable(Sql.SelectWatchedSenders(), false);
            }
            else
            {
                html = Html.FromTable(overdue, false);
            }

            string info = Html.Modal("Überwachte Sender", Html.InfoOverdue());

            await Html.PageAsync(context, "Überfällige Rückmeldungen", info + html);
        }
        #endregion


        #region Gesperrte Nachrichten
        [RestRoute("Get", "/blocked/{recId:num}")]
        public static async Task InBoxBlock(IHttpContext context)
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

            await Html.PageAsync(context, "Eingang", form, user);        
        }


        [RestRoute("Get", "/blocked")]
        public static async Task BlockedMessage(IHttpContext context)
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
                alert = Html.Alert(1, "Nachricht aktualisieren fehlgeschlagen", $"Die Nachricht {msg.Id}<p><i>{msg.Content}</i></p> konnte nicht geändert werden.");
            else
                alert = Html.Alert(2, "Nachricht aktualisiert", $"Änderungen für die Nachricht {msg.Id}<p><i>{msg.Content}</i></p> gespeichert.");

            System.Data.DataTable sent = Sql.Blocked_View();
            string table = Html.FromTable(sent, isAdmin, "blocked");

            await Html.PageAsync(context, "Nachricht aktualisiert", alert + table, user);
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

            if (isAdmin) form = Html.Modal("Benutzerkategorien", Html.InfoAccount()) + form ;

            await Html.PageAsync(context, "Benutzerkonto", table + form, user);            
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
            Person nP = Sql.NewPerson(payload);
            #endregion

            bool success = Sql.InsertPerson(nP.Name, nP.Password, nP.Level, nP.Company, nP.Phone, nP.Email, nP.Via, nP.MaxInactive);
            string alert;

            if (success)
            {
                alert = Html.Alert(3, "Neuen Kontakt gespeichert", "Der Kontakt " + nP.Name + " wurde erfolgreich neu erstellt.");
                Sql.InsertLog(2, "Der Kontakt >" + nP.Name + "< wurde neu erstellt durch >" + user.Name + "< [" + user.Level + "]");
            }
            else
                alert = Html.Alert(1, "Fehler beim speichern des Kontakts", "Der Kontakt " + nP.Name + " konnte nicht in der Datenbank gespeichert werden.");

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
            Person uP = Sql.NewPerson(payload);

            //kann maximal eigenen Access-Level vergeben.
            if (uP.Level > user.Level)
                uP.Level = user.Level; 
            #endregion

            bool success = uP.Id > 0 && Sql.UpdatePerson(uP.Id, uP.Name, uP.Password, uP.Level, uP.Company, uP.Phone, uP.Email, (int)uP.Via, uP.KeyWord, uP.MaxInactive);

            string alert;

            if (success)
            {
                alert = Html.Alert(3, "Kontakt gespeichert", "Der Kontakt [" + uP.Id + "] " + uP.Name + " wurde erfolgreich geändert.");
                Sql.InsertLog(2, "Der Kontakt [" + uP.Id + "] >" + uP.Name + "< wurde geändert durch >" + user.Name + "< [" + user.Level + "]");
            }
            else
                alert = Html.Alert(1, "Fehler beim speichern des Kontakts", "Der Kontakt [" + uP.Id + "] " + uP.Name + " konnte in der Datenbank nicht geändert werden.");

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
            //payload.TryGetValue("password", out string password); //Sicherheit!

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Name", name },
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

            Person p = Sql.NewPerson(payload);          
                                
            if (p.Id > 0)
            {
                string error = Html.Alert(1, "Registrierung fehlgeschlagen", $"Der Benutzername {p.Name} ist bereits vergeben." + @"<a href='/' class='w3-bar-item w3-button w3-teal w3-margin'>Nochmal</a>");
                await Html.PageAsync(context, "Benutzerregistrierung fehlgeschlagen", error);
                return;
            }

            #endregion

            bool success = Sql.InsertPerson(p.Name, p.Password, p.Level, p.Company, p.Phone, p.Email, p.Via, p.MaxInactive);

            string alert;

            if (success)
            {
                alert = Html.Alert(3, $"Erfolgreich registriert", $"Willkommen {p.Name}!<br/> Die Registrierung muss noch durch einen Administrator bestätigt werden, bevor Sie sich einloggen können. Informieren Sie einen Administrator.");
                Sql.InsertLog(2, $"Neuer Benutzer >{p.Name}< im Web-Portal registriert.");
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

            await Html.PageAsync(context, "Bereitschaftsdienste", table, user);
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
                { "@MinDate",  DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd") },
                { "@StartDate", shift.StartUtc.ToLocalTime().ToString("yyyy-MM-dd") },
                { "@EndDate", shift.EndUtc.ToLocalTime().ToString("yyyy-MM-dd") },             
                { "@Route", "update" }
            };

            string form = Html.Page(Server.Html_FormShift, pairs);

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Bereitschaftsdienst", table + form, user);
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
                { "@Route", "new" }
            };

            string form = Html.Page(Server.Html_FormShift, pairs);

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Bereitschaftsdienst", table + form, user);
        }

        [RestRoute("Post", "/shift/new")]
        public static async Task ShiftCreate(IHttpContext context)
        {
            Person user = await Html.GetLogedInUserAsync(context);
            if (user == null) return;

            Dictionary<string, string> payload = Html.Payload(context);
            Shift shift = Sql.GetShift(payload);
            if (shift.PersonId == 0) shift.PersonId = user.Id;
            shift.StartUtc = Sql.ShiftStartTimeUtc(shift.StartUtc);
            shift.EndUtc = Sql.ShiftEndTimeUtc(shift.EndUtc);

            bool success = true;
            List <Shift> shifts = Sql.SplitShift(shift);
            foreach (Shift splitShift in shifts)
            {
                if (!Sql.InsertShift(splitShift)) success = false;
            }

            string alert;

            if (success)
                alert = Html.Alert(3, "Neue Bereitschaft gespeichert", $"Neue Bereitschaft vom {shift.StartUtc.ToLocalTime().ToShortDateString()} bis {shift.EndUtc.ToLocalTime().ToShortDateString()} wurde erfolgreich erstellt.");
            else
                alert = Html.Alert(1, "Fehler beim speichern der Bereitschaft", "Die Bereitschaft konnte nicht in der Datenbank gespeichert werden.");

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

            shift.StartUtc = Sql.ShiftStartTimeUtc(shift.StartUtc);
            shift.EndUtc = Sql.ShiftEndTimeUtc(shift.EndUtc);

            bool success = true;
            List<Shift> shifts = Sql.SplitShift(shift);
            for (int i = 0; i < shifts.Count; i++)
            {
                if (i == 0)                
                    if (!Sql.UpdateShift(shifts[i])) success = false;
                else
                    if (!Sql.InsertShift(shifts[i])) success = false;
            }

            double shiftHours = shift.EndUtc.Subtract(shift.StartUtc).TotalHours;

            string alert;
            if (shiftHours > 0 && user.Level >= Server.Level_Reciever && success)
                alert = Html.Alert(3, "Bereitschaft geändert", $"Die Bereitschaft Nr. {shift.Id} von {shift.StartUtc.ToLocalTime().ToShortDateString()} bis {shift.EndUtc.ToLocalTime().ToShortDateString()} ({shiftHours} Std.) wurde erfolgreich geändert.");
            else if (user.Level >= Server.Level_Admin && Sql.DeleteShift(shift.Id))
                alert = Html.Alert(1, "Bereitschaft gelöscht", $"Die Bereitschaft Nr. {shift.Id} von {shift.StartUtc.ToLocalTime().ToShortDateString()} bis {shift.EndUtc.ToLocalTime().ToShortDateString()} wurde gelöscht.");
            else
                alert = Html.Alert(2, "Fehler beim Ändern der Bereitschaft", "Es wurden ungültige Parameter übergeben.");

            string table = Html.FromShiftTable(user);

            await Html.PageAsync(context, "Bereitschaftszeit geändert", alert + table, user);
        }
        #endregion


        #region Log
        [RestRoute("Get", "/log")]
        public static async Task LoggingShow(IHttpContext context)
        {
            #region Anfragenden Benutzer identifizieren
            Person user = await Html.GetLogedInUserAsync(context, false);
            bool isAdmin = user != null && user.Level >= Server.Level_Admin;
            #endregion

            System.Data.DataTable log = Sql.SelectLastLogs(500);
            string table = Html.FromTable(log, false, "");
            int del = 400;
            string html = !isAdmin ? string.Empty : $"<p><a href='/log/delete/{del}' class='w3-button w3-block w3-red w3-padding'>Bis auf letzten {del} Eintr&auml;ge alle l&ouml;schen</a></p>\r\n";

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

            System.Data.DataTable log = Sql.SelectLastLogs(500);
            string table = Html.FromTable(log, false, "");

            await Html.PageAsync(context, "Log", html + table, user);
        }
        #endregion


        [RestRoute]
        public static async Task Home(IHttpContext context)
        {
            string form = Html.Page(Server.Html_FormLogin, null);

            await Html.PageAsync(context, "Login", form);
        }

    }
}
