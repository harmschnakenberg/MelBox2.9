using MelBoxGsm;
using System;
using System.Data;
using System.Timers;

namespace MelBox2
{
    static partial class Program
    {
        public static int HourOfDailyTasks { get; set; } = 8;

        /// <summary>
        /// Starte zu jeder vollen Stunde
        /// </summary>
        public static void SetHourTimer(object sender, ElapsedEventArgs e)
        {
            //Zeit bis zur nächsten vollen Stunde 
            int min = 59 - DateTime.Now.Minute;
            int sec = 59 - DateTime.Now.Second;

            TimeSpan span = new TimeSpan(0, min, sec + 3); //In jedem Fall erst nach dem Stundensprung ausführen
#if DEBUG
            Log.Info($"Nächste Senderüberprüfung in {min} min. {sec} sek.", 65053);
#endif
            Timer execute = new Timer(span.TotalMilliseconds);

            execute.Elapsed += new ElapsedEventHandler(CheckEmailInBox); //Gürtel
            execute.Elapsed += new ElapsedEventHandler(RenewEmailInBox); //Hosenträger   
            execute.Elapsed += new ElapsedEventHandler(CheckCallForwardingNumber);
            execute.Elapsed += new ElapsedEventHandler(SenderTimeoutCheck);
            execute.Elapsed += ConsolidateGsmSignal;
            execute.Elapsed += new ElapsedEventHandler(DailyBackup);
            execute.Elapsed += new ElapsedEventHandler(GetUsedMemory);
            execute.Elapsed += new ElapsedEventHandler(DailyNotification); //Stundensprung beachten!
            execute.Elapsed += new ElapsedEventHandler(SetHourTimer);

            execute.AutoReset = false;
            execute.Start();

            //Tagessprung anzeigen
            if (DateTime.Now.Hour == 0)
                Console.WriteLine(DateTime.Now.ToLongDateString());
        }

        /// <summary>
        /// Beendet die Verbindung zum Eingngs-Postfach und Stellt die Verbindung erneut wieder her. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void RenewEmailInBox(object sender, ElapsedEventArgs e)
        {
            //if (EmailListener.ImapConnectionRenewInterval == 0 || DateTime.Now.Hour % EmailListener.ImapConnectionRenewInterval > 0) return; // 3,6,9,12.. Uhr % 3 = 0 -> Alle 3 Stunden

#if DEBUG
            Console.WriteLine(DateTime.Now.ToShortTimeString() + ": Die Verbindung zum E-Mail-Server wird planmäßig erneuert.");
#endif
            Program.emailListener.EmailInEvent -= EmailListener_EmailInEvent;
            Program.emailListener.Dispose();

            Program.emailListener = new EmailListener();
            Program.emailListener.EmailInEvent += EmailListener_EmailInEvent;
        }

        private static void ConsolidateGsmSignal(object sender, ElapsedEventArgs e)
        {
            Sql.ConsolidateGsmSignal();
        }

        private static void CheckEmailInBox(object sender, ElapsedEventArgs e)
        {
#if DEBUG
                Console.WriteLine(DateTime.Now.ToShortTimeString() + " E-Mail-Abruf.");
#endif
            EmailListener emailListener = new EmailListener();
            emailListener.ReadUnseen();
            emailListener.Dispose();
        }

        private static void SenderTimeoutCheck(object sender, ElapsedEventArgs e)
        {
            if (Sql.IsWatchTime()) return; //Inaktivität nur zur Geschäftszeit prüfen.

            DataTable dt = Sql.SelectOverdueSenders();

            System.Net.Mail.MailAddressCollection mailAddresses = Sql.GetCurrentEmailRecievers(true); //E-Mail-Verteiler

            if (!mailAddresses.Contains(Email.Admin))
                mailAddresses.Add(Email.Admin);
            
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                string name = dt.Rows[i]["Name"].ToString();
                string company = dt.Rows[i]["Firma"].ToString();
                string due = dt.Rows[i]["Fällig_seit"].ToString();

                string text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + $" Inaktivität >{name}<{(company.Length > 0 && name != company ? $", >{company}<" : string.Empty)}. Meldung fällig seit >{due}<. \r\nMelsys bzw. Segno vor Ort prüfen.";
                
                Log.Info(text, 60723);

                Email.Send(mailAddresses, text, $"Inaktivität >{name}<", DateTime.Now.Millisecond);
            }
        }

        private static void DailyNotification(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Now.Hour != HourOfDailyTasks) return;

            Console.WriteLine($"{DateTime.Now.ToShortTimeString()}: Versende tägliche Kontroll-SMS an " + Gsm.AdminPhone);
            Gsm.SmsSend(Gsm.AdminPhone, $"SMS-Zentrale Routinemeldung.");

            Console.WriteLine($"{DateTime.Now.ToShortTimeString()}: Versende tägliche Kontroll-E-Mail an " + Email.Admin);
           
            System.Net.Mail.MailAddressCollection mailAddresses = new System.Net.Mail.MailAddressCollection
            {
                Email.Admin
            };

            //diese Überladung von Send() dokumentiert die gesendete Routinemeldung in der Tabelle Sent:
            Email.Send(mailAddresses, "Routinemeldung. E-Mail-Versand aus MelBox2 ok.", "SMS-Zentrale Routinemeldung.", DateTime.UtcNow.Millisecond); 
        }

        private static void DailyBackup(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Now.Hour != HourOfDailyTasks) return;

            Console.WriteLine($"{DateTime.Now.ToShortTimeString()}: Prüfe / erstelle Backup der Datenbank.");
            Sql.DbBackup();
        }

        private static void GetUsedMemory(object sender, ElapsedEventArgs e)
        {
            System.Diagnostics.PerformanceCounter perfCpuCount = new System.Diagnostics.PerformanceCounter("Processor Information", "% Processor Time", "_Total");

            using (System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess())
            {
                // The proc.PrivateMemorySize64 will returns the private memory usage in byte.
                // Would like to Convert it to Megabyte? divide it by 2^20
                long memory = proc.PrivateMemorySize64 / (1 << 20); // (1024 * 1024);
                int cpu = (int)perfCpuCount.NextValue();

                string msg = $"Vom Programm zurzeit belegter Arbeitsspeicher: {memory} MB, CPU bei {cpu}%";
#if DEBUG
                Console.WriteLine($"{DateTime.Now.ToLongTimeString()}: {msg}");
#else
                if (memory > 100 || cpu > 50)
#endif
                    Log.Info(msg, 88);
            }
        }

        /// <summary>
        /// Prüft, ob die Nummer der aktuellen Bereitschaft mit der Nummer für Rufweiterleitung übereinstimmt.
        /// Ändert ggf. die Weiterleitung.
        /// Prüft auch, ob es in der DB einen Eintrag für das Bereitschaftshandy gibt und erzeugt diesen ggf. (z.B. aus Versehen geändert).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal static void CheckCallForwardingNumber(object sender, ElapsedEventArgs e)
        {
            string phone = Sql.GetCurrentCallForwardingNumber(OverideCallForwardingNumber);

            if (Gsm.CallForwardingNumber != phone)
            {
                Gsm.SetCallForewarding(phone);

                if (Gsm.CallForwardingActive)
                    Sql.InsertLog(3, $"Sprachanrufe werden weitergeleitet an '{phone}'");
                else
                    Sql.InsertLog(1, $"Sprachanrufe werden zur Zeit nicht weitergeleitet.");
            }

            if (phone != Gsm.CallForwardingNumber)
                Console.WriteLine($"{DateTime.Now.ToShortTimeString()}: Die Rufumleitung ist {(Gsm.CallForwardingActive ? "aktiv" : "inaktiv")}, soll an {phone}, geht aber an {Gsm.CallForwardingNumber}.");
        }


    }
}
