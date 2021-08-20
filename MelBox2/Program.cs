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
            Log.Info(System.Reflection.Assembly.GetEntryAssembly().GetName().Name + " gestartet.", 100);
            Console.WriteLine("'Exit' eingeben zum beenden.");
            Server.Start();
            Sql.CheckDbFile();
            
            Gsm.NewErrorEvent += Gsm_NewErrorEvent;
            //Gsm.NetworkStatusEvent += Gsm_NetworkStatusEvent;
            Gsm.SmsRecievedEvent += Gsm_SmsRecievedEvent;
            Gsm.SmsSentEvent += Gsm_SmsSentEvent;
            Gsm.FailedSmsSendEvent += Gsm_FailedSmsSendEvent;
            Gsm.SmsReportEvent += Gsm_SmsReportEvent;

            Gsm.AdminPhone = "+4916095285304";            
            Gsm.SetupModem("+4916095285304");

            SetHourTimer();

            bool run = true;
            while(run)
            {
                //if (Console.KeyAvailable)
                {
                    string input = Console.ReadLine();

                    switch (input.ToLower())
                    {
                        case "exit":
                            run = false;
                            break;
                        case "sms sim":
                            Sms_Sim();
                            break;
                        case "sms read all":
                            List<SmsIn> list = Gsm.SmsRead("ALL");
                            foreach(SmsIn sms in list)                            
                                Console.WriteLine($"Lese [{sms.Index}] {sms.TimeUtc.ToLocalTime()} Tel. >{sms.Phone}< >{sms.Message}<");                            
                            break;
                        case "debug":
                            Console.WriteLine($"Aktueller Debug: {ReliableSerialPort.Debug}. Neuer Debug?");
                            string x = Console.ReadLine();
                            if (byte.TryParse(x, out byte d))
                                ReliableSerialPort.Debug = d;
                            break;
                    }
                }
            }

            Server.Stop();
            Log.Info(System.Reflection.Assembly.GetEntryAssembly().GetName().Name + " beendet.", 100);
            Console.WriteLine("Progammende. Beliebige Taste zum beenden..");
            Console.ReadKey();
        }

        private static void Gsm_NewErrorEvent(object sender, string e)
        {
            Log.Warning("GSM-Fehlermeldung - " + e, 1320);
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


        private static void Sms_Sim()
        {
            SmsIn sms = new SmsIn
            {
                Index = 0,
                Phone = "+4942122317123",
                Status = "REC UNREAD",
                TimeUtc = DateTime.UtcNow,
                Message = "MelBox2: Simulierter SMS-Empfang"
            };

            List<SmsIn> smsen = new List<SmsIn>
            {
                sms
            };

            ParseNewSms(smsen);
        }

    }
}
