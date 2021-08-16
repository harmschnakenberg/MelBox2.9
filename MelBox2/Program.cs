using MelBoxGsm;
using System;
using System.Collections.Generic;

namespace MelBox2
{
    partial class Program
    {
        static void Main()
        {
            Console.WriteLine("Progammstart.");
            Server.Start();

            //Gsm.NewErrorEvent += Gsm_NewErrorEvent;
            //Gsm.NetworkStatusEvent += Gsm_NetworkStatusEvent;
            Gsm.SmsRecievedEvent += Gsm_SmsRecievedEvent;
            Gsm.SmsSentEvent += Gsm_SmsSentEvent;
            Gsm.FailedSmsSendEvent += Gsm_FailedSmsSendEvent;
            Gsm.SmsReportEvent += Gsm_SmsReportEvent;


            Console.WriteLine("Drücke ESC zum beenden.");
            do
            {                
                //while (!Console.KeyAvailable)
                //{
                //    // Do something
                //}
            } while (Console.ReadKey().Key != ConsoleKey.Escape);

            Server.Stop();
            Console.WriteLine("Progammende.");
        }

        private static void Gsm_SmsRecievedEvent(object sender, List<Gsm.SmsIn> e)
        {
            ParseNewSms(e);
        }

        private static void Gsm_SmsReportEvent(object sender, Gsm.Report e)
        {
            UpdateSent(e);
        }

        private static void Gsm_FailedSmsSendEvent(object sender, Gsm.SmsOut e)
        {
            InsertLog(Gsm.MaxSendTrysPerSms - e.SendTryCounter, $"Senden von SMS an >{e.Phone}< fehlgeschlagen. Noch {Gsm.MaxSendTrysPerSms - e.SendTryCounter} Versuche. Nachricht >{e.Message}<");
        }

        private static void Gsm_SmsSentEvent(object sender, Gsm.SmsOut e)
        {
            InsertSent(e);
        }



        
    }
}
