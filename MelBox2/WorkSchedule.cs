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

            TimeSpan span = new TimeSpan(0, min, sec);
#if DEBUG
            Log.Info($"Nächste Senderüberprüfung in {min} min. {sec} sek.", 65053);
#endif
            Timer execute = new Timer(span.TotalMilliseconds);
            
            execute.Elapsed += new ElapsedEventHandler(SenderTimeoutCheck);            
            execute.Elapsed += new ElapsedEventHandler(DailyBackup);
            execute.Elapsed += new ElapsedEventHandler(GetUsedMemory);
            execute.Elapsed += new ElapsedEventHandler(SetHourTimer);
            execute.Elapsed += new ElapsedEventHandler(DailyNotification); //Stundensprung beachten!

            execute.AutoReset = false;
            execute.Start();
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
                Console.WriteLine($"{DateTime.Now.ToLongTimeString()}: {msg}");
#if !DEBUG
                if (memory > 100 || cpu > 50)
#endif
                    Log.Info(msg, 88);
            }
        }

        /// <summary>
        /// Prüft, ob die Nummer der aktuellen Bereitschaft mit der Nummer für Rufweiterleitung übereinstimmt.
        /// Ändert ggf. die Weiterleitung.
        /// Prüft auch, ob es in der DB einen Eintrag für das Berietschaftshandy gibt und erzeugt diesen ggf. (z.B. ausversehen geändert).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CheckCallForwardingNumber(object sender, ElapsedEventArgs e)
        {
            string backup = Sql.GetPhone_Bereitschaftshandy();

            if (backup?.Length == 0) //'Bereitschaftshandy' nicht in DB vorhanden.
            {
                _ = Sql.NonQuery(@"INSERT INTO Person (Name, Password, Level, Company, Phone, Email, Via) VALUES ('Bereitschaftshandy', '�u�q�_��)vIh�ҷ\z�(yC[B���^|�', 2000, 'Kreutzträger Kältetechnik, Bremen', '+491729441694', 'Bereitschaftshandy@kreutztraeger.de', 1); ", null);
                backup = Sql.GetPhone_Bereitschaftshandy();
            }

            string phone = Sql.GetCurrentShiftPhoneNumbers()?[0] ?? backup;

            if (Gsm.CallForwardingNumber != phone)
            {
                Gsm.CallForwardingNumber = phone;
                Gsm.SetCallForewarding(Gsm.CallForwardingNumber);
            }

        }

    }
}
