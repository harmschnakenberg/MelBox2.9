using System;
using System.Collections.Generic;
using MelBoxGsm;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    partial class Program
    {
        static void Main()
        {
            Console.WriteLine("Progammstart.");
            Server.Start();
            Sql.CheckDbFile();

            //Gsm.NewErrorEvent += Gsm_NewErrorEvent;
            //Gsm.NetworkStatusEvent += Gsm_NetworkStatusEvent;
            Gsm.SmsRecievedEvent += Gsm_SmsRecievedEvent;
            Gsm.SmsSentEvent += Gsm_SmsSentEvent;
            Gsm.FailedSmsSendEvent += Gsm_FailedSmsSendEvent;
            Gsm.SmsReportEvent += Gsm_SmsReportEvent;

            Gsm.AdminPhone = "+4916095285304";            
            Gsm.SetupModem("+4916095285304");

            Console.WriteLine("Drücke ESC zum beenden.");
            //do
            //{
            //    //while (!Console.KeyAvailable)
            //    //{
            //    //    // Do something
            //    //}
            //} while (Console.ReadKey().Key != ConsoleKey.Escape);

            ConsoleKeyInfo key;
            do {
                key = Console.ReadKey();
            } while (key.Key != ConsoleKey.Escape);

            Server.Stop();
            Console.WriteLine("Progammende.");
        }

        private static void Gsm_SmsRecievedEvent(object sender, List<SmsIn> e)
        {
            ParseNewSms(e);
        }

        private static void Gsm_SmsReportEvent(object sender, Report e)
        {
            Sql.UpdateSent(e);
        }

        private static void Gsm_FailedSmsSendEvent(object sender, SmsOut e)
        {
            Sql.InsertLog(Gsm.MaxSendTrysPerSms - e.SendTryCounter, $"Senden von SMS an >{e.Phone}< fehlgeschlagen. Noch {Gsm.MaxSendTrysPerSms - e.SendTryCounter} Versuche. Nachricht >{e.Message}<");
        }

        private static void Gsm_SmsSentEvent(object sender, SmsOut e)
        {
            Sql.InsertSent(e);
        }



        
    }
}
