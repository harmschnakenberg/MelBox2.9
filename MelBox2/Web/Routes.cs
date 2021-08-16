using Grapevine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelBoxGsm;

namespace MelBox2
{
    [RestResource]
    class Routes
    {


        #region Benutzerverwaltung
        [RestRoute("Post", "/register")]
        public static async Task Register(IHttpContext context)
        {
            Dictionary<string, string> payload = Html.Payload(context);
            payload.TryGetValue("name", out string name);
            //payload.TryGetValue("password", out string password); //Sicherheit!

            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Name", name }
            };

            string form = Html.Page(Server.Html_FormRegister, pairs);

            await Html.PageAsync(context, "Benutzerregistrierung", form);
        }


        [RestRoute("Post", "/register/thanks")]
        public static async Task RegisterProcessing(IHttpContext context)
        {
            #region Form auslesen
            Dictionary<string, string> payload = Html.Payload(context);

            Person p = Program.NewPerson(payload);          
            #endregion

            #region Kontakt erstellen            
            if (p.Id > 0)
            {
                string error = Html.Alert(1, "Registrierung fehlgeschlagen", $"Der Benutzername {p.Name} ist bereits vergeben." + @"<a href='/' class='w3-bar-item w3-button w3-teal w3-margin'>Nochmal</a>");
                await Html.PageAsync(context, "Benutzerregistrierung fehlgeschlagen", error);
                return;
            }

            #endregion

            bool success = Program.InsertPerson(p.Name, p.Password, p.Level, p.Company, p.Phone, p.Email, p.Via, p.MaxInactive);

            string alert;

            if (success)
            {
                alert = Html.Alert(3, $"Erfolgreich registriert", $"Willkommen {p.Name}!<br/> Die Registrierung muss noch durch einen Administrator bestätigt werden, bevor Sie sich einloggen können. Informieren Sie einen Administrator.");
                Program.InsertLog(2, $"Neuer Benutzer >{p.Name}< im Web-Portal registriert.");
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
            string guid = Program.CheckCredentials(name, password);

            int prio = 1;
            string titel = "Login fehlgeschlagen";
            string text = "Benutzername und Passwort prüfen.<br/>Neue Benutzer müssen freigeschaltet sein.<br/>" + @"<a href='/' class='w3-bar-item w3-button w3-teal w3-margin'>Nochmal</a>";

            if (guid.Length > 0)
            {
                prio = 3;
                titel = "Login ";
                string level = "Beobachter";

                System.Net.Cookie cookie = new System.Net.Cookie("MelBoxId", guid, "/");
                context.Response.Cookies.Add(cookie);

                if (Server.LogedInHash.TryGetValue(guid, out Person user))
                {
                    if (user.Level >= Server.Level_Admin)
                        level = "Admin";
                    else if (user.Level >= Server.Level_Reciever)
                        level = "Benutzer";
                }

                text = $"Willkommen {level} {name}";
                Program.InsertLog(3, $"Login {level} [{user.Id}] >{user.Name}<");
            }

            string alert = Html.Alert(prio, titel, text);

            await Html.PageAsync(context, titel, alert);
        }
        #endregion

        [RestRoute("Get", "/gsm")]
        public static async Task ModemShow(IHttpContext context)
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "@Registered" , Gsm.NetworkRegistration},
                { "@OwnName", Gsm.OwnName},
                { "@OwnNumber", Gsm.OwnNumber},
                { "@ServiceCenter", Gsm.SmsServiceCenterAddress},
                { "@ProviderName" , Gsm.ProviderName},
                { "@RelayNumber" ,  Gsm.CallForwardingNumber.Length > 0 ? "+" + Gsm.CallForwardingNumber : "-deaktiviert-" },
                { "@PinStatus" , Gsm.SimPinStatus},
                { "@ModemError",  Gsm.LastError.Item2.Length > 0 ? $"{Gsm.LastError.Item1}: {Gsm.LastError.Item2}" : "-kein Fehler-"}
            };

            string html = Html.Page(Server.Html_FormGsm, pairs);

            await Html.PageAsync(context, "GSM-Modem", html);
        }

        [RestRoute]
        public static async Task Home(IHttpContext context)
        {
            string form = Html.Page(Server.Html_FormLogin, null);

            await Html.PageAsync(context, "Login", form);
        }

    }
}
