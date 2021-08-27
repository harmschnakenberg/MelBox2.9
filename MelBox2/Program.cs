using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            Console.WriteLine("Progammstart.");
            ShowHelp();
            Log.Info(AppName + " gestartet.", 100);

            Server.Start();
            Gsm.AdminPhone = "+4916095285xxx";
            Gsm.CallForwardingNumber = Gsm.AdminPhone;

            Sql.CheckDbFile();
            Sql.DbBackup();
            GetIniValues();

            Gsm.NewErrorEvent += Gsm_NewErrorEvent;
            Gsm.NetworkStatusEvent += Gsm_NetworkStatusEvent;
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
                    case "help":
                        ShowHelp();
                        break;
                    case "ini":
                        GetIniValues(); Console.WriteLine($"Initialisierungswerte wurden aus der Datenbank neu eingelesen."); 
                        break;
                    case "cls":
                        Console.Clear();
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
                        Console.WriteLine($"Aktuelles Debug-Byte: {(int)ReliableSerialPort.Debug}. Neuer Debug?");
                        Console.WriteLine($"{(int)ReliableSerialPort.GsmDebug.AnswerGsm}\tAntwort von Modem");
                        Console.WriteLine($"{(int)ReliableSerialPort.GsmDebug.RequestGsm}\tAnfrage an Modem");
                        Console.WriteLine($"{(int)ReliableSerialPort.GsmDebug.UnsolicatedResult}\tEreignisse von Modem");
                        string x = Console.ReadLine();
                        if (byte.TryParse(x, out byte d))
                        {
                            ReliableSerialPort.Debug = (ReliableSerialPort.GsmDebug) d;
                            Console.WriteLine("Neuer Debug-Level: " + d);
                        }
                        break;
                }

            }    
#if DEBUG
            Console.WriteLine("Progammende. Beliebige Taste zum beenden..");
            Console.ReadKey();
#endif
        }

        private static void Gsm_NetworkStatusEvent(object sender, int quality)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Log.Info(AppName + " beendet.", 99);
            Server.Stop();
            Gsm.ModemShutdown();
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
            Sql.InsertLog(1, "Fehlermeldung Modem: " + e);
        }

        private static void Gsm_SmsRecievedEvent(object sender, List<SmsIn> e)
        {
            ParseNewSms(e);
        }

        private static void Gsm_SmsReportEvent(object sender, Report e)
        {
            Sql.InsertReport(e);
            Sql.UpdateSent(e);
        }

        private static void Gsm_FailedSmsSendEvent(object sender, SmsOut e)
        {
            string txt = $"Senden von SMS an >{e.Phone}< fehlgeschlagen. Noch {Gsm.MaxSendTrysPerSms - e.SendTryCounter} Versuche. Nachricht >{e.Message}<";

            Sql.InsertLog(Gsm.MaxSendTrysPerSms - e.SendTryCounter, txt);

            Report fake = new Report
            {
                DeliveryStatus = (int)(MaxSendTrysPerSms < e.SendTryCounter ? Sql.MsgConfirmation.SmsSendRetry : Sql.MsgConfirmation.SmsAborted),
                Reference = e.Reference,
                DischargeTimeUtc = e.SendTimeUtc
            };

            Sql.UpdateSent(fake); 
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

        private static void ShowHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("### HILFE MELBOX CONSOLE ###");
            sb.AppendLine("Exit".PadRight(32) + "Beendet das Programm. Fährt den Webserver herunter. Schließt den Seriellen Port.");
            sb.AppendLine("Help".PadRight(32) + "Ruft diese Hilfe auf.");
            sb.AppendLine("CLS".PadRight(32) + "Löscht den Fensterinhalt.");
            sb.AppendLine("Debug".PadRight(32) + "Zeigt das Debug-Byte an. Aktuell " + (byte)ReliableSerialPort.Debug);
            sb.AppendLine("".PadRight(32) + $"{(int)ReliableSerialPort.GsmDebug.AnswerGsm} = von Modem empfangen");
            sb.AppendLine("".PadRight(32) + $"{(int)ReliableSerialPort.GsmDebug.RequestGsm} = an Modem gesendet");
            sb.AppendLine("".PadRight(32) + $"{(int)ReliableSerialPort.GsmDebug.UnsolicatedResult} = Ereignisse von Modem.");
            sb.AppendLine("Ini".PadRight(32) + "Liest die Initialisierungswerte aus der Datenbank neu ein.");
            sb.AppendLine("Sms Read All".PadRight(32) + "Liest alle im Modemspeicher vorhandenen SMSen aus und zeigt sie in der Console an.");
            sb.AppendLine("Sms Read Sim".PadRight(32) + "Simuliert den Empfang einer SMS mit >MelBox2: Simulierter SMS-Empfang<.");
            sb.AppendLine("### HILFE ENDE ###");

            Console.WriteLine(sb.ToString());
        }
    }
}
