using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public static void SetHourTimer()
        {
            //Zeit bis zur nächsten vollen Stunde 
            int min = 59 - DateTime.Now.Minute;
            int sec = 59 - DateTime.Now.Second;

            TimeSpan span = new TimeSpan(0, min, sec);
#if DEBUG
            Log.Info($"Nächste Senderüberprüfung in {min} min.", 65053);
#endif
            Timer execute = new Timer(span.TotalMilliseconds);
            
            execute.Elapsed += new ElapsedEventHandler(SenderTimeoutCheck);
            execute.Elapsed += new ElapsedEventHandler(DailyNotification);
            execute.Elapsed += new ElapsedEventHandler(DailyBackup);
            execute.Elapsed += new ElapsedEventHandler(GetUsedMemory);

            execute.AutoReset = true;
            execute.Start();
        }

        private static void SenderTimeoutCheck(object sender, ElapsedEventArgs e)
        {           
            if (
                DateTime.Now.DayOfWeek == DayOfWeek.Saturday ||
                DateTime.Now.DayOfWeek == DayOfWeek.Sunday ||
                Sql.IsHolyday(DateTime.Now)
                )
                return; //Nur an Werktagen

            DataTable dt = Sql.SelectOverdueSenders();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                string name = dt.Rows[i]["Name"].ToString();
                string company = dt.Rows[i]["Firma"].ToString();
                string due = dt.Rows[i]["Fällig_seit"].ToString();

                string text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + $" Inaktivität >{name}<, >{company}<. Meldung fällig seit >{due}<. Melsys bzw. Segno vor Ort prüfen.";

                Email.Send(Email.Admin, text, $"Inaktivität >{name}<, >{company}<");
            }

        }

        private static void DailyNotification(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Now.Hour != HourOfDailyTasks) return;

            Console.WriteLine($"Versende tägliche Kontroll-SMS an " + Gsm.AdminPhone);
            Gsm.SmsSend(Gsm.AdminPhone, $"SMS-Zentrale Routinemeldung.");        
        }

        private static void DailyBackup(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Now.Hour != HourOfDailyTasks) return;

            Console.WriteLine("Prüfe / erstelle Backup der Datenbank.");
            Sql.DbBackup();
        }

        private static void GetUsedMemory(object sender, ElapsedEventArgs e)
        {
            System.Diagnostics.PerformanceCounter perfCpuCount = new System.Diagnostics.PerformanceCounter("Processor Information", "% Processor Time", "_Total");

            using (System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess())           
            {
                // The proc.PrivateMemorySize64 will returns the private memory usage in byte.
                // Would like to Convert it to Megabyte? divide it by 2^20
                long memory = proc.PrivateMemorySize64 / (1024 * 1024);
                int cpu = (int)perfCpuCount.NextValue();

                string msg = $"Vom Programm zurzeit belegter Arbeitsspeicher: {memory} MB, CPU bei {cpu}%";                
                Console.WriteLine(msg);
#if !DEBUG
                if (memory > 100 || cpu > 50)
#endif
                    Log.Info(msg, 88);
            }
        }

    }
}
