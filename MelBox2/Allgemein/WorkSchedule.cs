using System;
using System.Data;
using System.Timers;
using MelBoxGsm;

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

            execute.Elapsed += new ElapsedEventHandler(CheckEmailInBox);
            execute.Elapsed += new ElapsedEventHandler(CheckCallForwardingNumber);
            execute.Elapsed += new ElapsedEventHandler(SenderTimeoutCheck);            
            execute.Elapsed += new ElapsedEventHandler(DailyBackup);
            execute.Elapsed += new ElapsedEventHandler(GetUsedMemory);
            execute.Elapsed += new ElapsedEventHandler(DailyNotification); //Stundensprung beachten!
            execute.Elapsed += new ElapsedEventHandler(SetHourTimer);
            
            execute.AutoReset = false;
            execute.Start();
        }

        private static void CheckEmailInBox(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("> E-Mail-Abruf zur vollen Stunde.");
#endif
            EmailListener emailListener = new EmailListener();
            emailListener.ReadUnseen();
            emailListener.Dispose();
        }

        private static void SenderTimeoutCheck(object sender, ElapsedEventArgs e)
        {            
            if (Sql.IsWatchTime()) return; //Inaktivität nur zur Geschäftszeit prüfen.

            DataTable dt = Sql.SelectOverdueSenders();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                string name = dt.Rows[i]["Name"].ToString();
                string company = dt.Rows[i]["Firma"].ToString();
                string due = dt.Rows[i]["Fällig_seit"].ToString();

                string text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + $" Inaktivität >{name}<{(company.Length > 0 ? $", >{company}<" : string.Empty)}. Meldung fällig seit >{due}<. \r\nMelsys bzw. Segno vor Ort prüfen.";

                Log.Info(text, 60723);
                Email.Send(Email.Admin, text, $"Inaktivität >{name}<{(company.Length > 0 ? $", >{company}< " : string.Empty)}", true);
            }
        }

        private static void DailyNotification(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Now.Hour != HourOfDailyTasks) return;

            Console.WriteLine($"{DateTime.Now.ToLongTimeString()}: Versende tägliche Kontroll-SMS an " + Gsm.AdminPhone);
            Gsm.SmsSend(Gsm.AdminPhone, $"SMS-Zentrale Routinemeldung.");        
        }

        private static void DailyBackup(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Now.Hour != HourOfDailyTasks) return;

            Console.WriteLine($"{DateTime.Now.ToLongTimeString()}: Prüfe / erstelle Backup der Datenbank.");
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
                Console.WriteLine($"Die aktuelle Rufumleitung soll an {phone}, geht aber an {Gsm.CallForwardingNumber}.");

                Console.WriteLine($"Rufumleitung an {Gsm.CallForwardingNumber} ist {(Gsm.CallForwardingActive ? "aktiv" : "inaktiv")}.");           
        }



    }
}
