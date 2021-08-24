using System;
using System.Collections.Generic;
using System.Diagnostics;
using MelBoxGsm;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    partial class Program
    {
        public static readonly string AppName = Process.GetCurrentProcess().ProcessName;
        static void Main()
        {
            if (Process.GetProcessesByName(AppName).Length > 1)
            {
                Log.Warning($"Es kann nur eine Instanz von {AppName} ausgeführt werden.", 739);                
                return;
            }

            Console.WriteLine("Progammstart.");
            Console.WriteLine("'Exit' eingeben zum beenden.");
            Log.Info(AppName + " gestartet.", 100);

            Server.Start();
            Gsm.AdminPhone = "+4916095285304";
            Gsm.CallForwardingNumber = Gsm.AdminPhone;

            Sql.CheckDbFile();
            Sql.DbBackup();
            GetIniValues();

            Gsm.NewErrorEvent += Gsm_NewErrorEvent;
            //Gsm.NetworkStatusEvent += Gsm_NetworkStatusEvent;
            Gsm.SmsRecievedEvent += Gsm_SmsRecievedEvent;
            Gsm.SmsSentEvent += Gsm_SmsSentEvent;
            Gsm.FailedSmsSendEvent += Gsm_FailedSmsSendEvent;
            Gsm.SmsReportEvent += Gsm_SmsReportEvent;
            Gsm.NewCallRecieved += Gsm_NewCallRecieved;

            Gsm.SetupModem();

            SetHourTimer();

            bool run = true;
            while (run)
            {
                string input = Console.ReadLine();

                switch (input.ToLower())
                {
                    case "exit":
                        run = false;
                        break;
                    case "ini":                        
                        GetIniValues();
                        break;
                    case "sms read sim":
                        SmsRead_Sim();
                        break;
                    case "sms read all":
                        List<SmsIn> list = Gsm.SmsRead("ALL");
                        foreach (SmsIn sms in list)
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

            Server.Stop();
            Log.Info(AppName + " beendet.", 101);
#if DEBUG
            Console.WriteLine("Progammende. Beliebige Taste zum beenden..");
            Console.ReadKey();
#endif
        }

        private static void Gsm_NewCallRecieved(object sender, string e)
        {
            SmsIn dummy = new SmsIn
            {
                Phone = e,
                Message = $"Sprachanruf von >{e}< weitergeleitet an >{CallForwardingNumber}<",
                TimeUtc = DateTime.UtcNow
            };

            Sql.InsertRecieved(dummy);

            Email.Send(Email.Admin, $"Sprachanruf {dummy.TimeUtc.ToLocalTime()} weitergeleitet an >{CallForwardingNumber}<.", $"Sprachanruf >{e}<", true);
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


        private static void SmsRead_Sim()
        {
            Console.WriteLine("Simuliere den Empfang einer SMS.");

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
