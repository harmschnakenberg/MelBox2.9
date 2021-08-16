using Grapevine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelBox2
{
    class Server
    {

        private static IRestServer restServer;

        public static int Level_Admin { get; set; } = 9000; //Benutzerverwaltung u. -Einteilung
       
        public static int Level_Reciever { get; set; } = 2000; //Empfänger bzw. Bereitschaftsnehmer

        private static readonly string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public static string Html_Skeleton { get; } = Path.Combine(appPath, "Templates", "Skeleton.html");
        public static string Html_FormLogin { get; } = Path.Combine(appPath, "Templates", "FormLogin.html");
        public static string Html_FormMessage { get; } = Path.Combine(appPath, "Templates", "FormMessage.html");
        public static string Html_FormAccount { get; } = Path.Combine(appPath, "Templates", "FormAccount.html");
        public static string Html_FormCompany { get; } = Path.Combine(appPath, "Templates", "FormCompany.html");
        public static string Html_FormRegister { get; } = Path.Combine(appPath, "Templates", "FormRegister.html");
        public static string Html_FormShift { get; } = Path.Combine(appPath, "Templates", "FormShift.html");
        public static string Html_FormGsm { get; } = Path.Combine(appPath, "Templates", "FormGsm.html");

        /// <summary>
        /// GUID - User-Id
        /// </summary>
        internal static Dictionary<string, Person> LogedInHash = new Dictionary<string, Person>();

        public static void Start()
        {
            if (restServer != null && restServer.IsListening) return;

            try
            {
                restServer = RestServerBuilder.From<Startup>().Build();

                restServer.AfterStarting += (s) =>
                {
                    Process.Start("explorer", s.Prefixes.First());
                };

                restServer.AfterStopping += (s) =>
                {
                    Console.WriteLine("Web-Server beendet.");
                };

                restServer.Start();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static void Stop()
        {
            if (restServer != null)
                restServer.Stop();
        }


    }
}
