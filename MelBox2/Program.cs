using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MelBoxGsm;

namespace MelBox2
{
    partial class Program
    {
        public static readonly string AppName = Process.GetCurrentProcess().ProcessName;
      
        static void Main()
        {
            if (Process.GetProcessesByName(AppName).Length > 1)
            {
                //if (Args.Length == 0)                
                    Log.Warning($"Es kann nur eine Instanz von {AppName} ausgeführt werden.", 739);                
                return;
            }

            Console.Title = "MelBox2";

            MelBoxGsm.CleanClose.CloseConsoleHandler += new CleanClose.EventHandler(CleanClose.Handler); //erzwungenes Beenden (X am Konsolenfenster)
            CleanClose.SetConsoleCtrlHandler(CleanClose.CloseConsoleHandler, true);

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit; //Normales Beenden 

            Console.WriteLine("Progammstart.");
            ShowHelp();
            Log.Info(AppName + " gestartet.", 100);
            Sql.InsertLog(3, AppName + " gestartet.");

            Server.Start();
            Gsm.AdminPhone = "+4916095285xxx";
            Gsm.CallForwardingNumber = Gsm.AdminPhone;

            Sql.CheckDbFile();
            Sql.DbBackup();
            GetIniValues();

            ReliableSerialPort.SerialPortErrorEvent += ReliableSerialPort_SerialPortErrorEvent;
            Gsm.NewErrorEvent += Gsm_NewErrorEvent;
            Gsm.NetworkStatusEvent += Gsm_NetworkStatusEvent;
            Gsm.SmsRecievedEvent += Gsm_SmsRecievedEvent;
            Gsm.SmsSentEvent += Gsm_SmsSentEvent;
            Gsm.FailedSmsSendEvent += Gsm_FailedSmsSendEvent;
            Gsm.SmsReportEvent += Gsm_SmsReportEvent;
            Gsm.NewCallRecieved += Gsm_NewCallRecieved;

            Gsm.SetupModem();

            SetHourTimer(null, null);
            Scheduler.CeckOrCreateWatchDog();
                        
            //Neustart melden
            Email.Send(Email.Admin, $"MelBox2 Neustart um {DateTime.Now.ToLongTimeString()}\r\n\r\n" +
                $"Mobilfunkverbindung: {Gsm.NetworkRegistration.RegToString()},\r\n" +
                $"Signalstärke: {(Gsm.SignalQuality > 100 ? 0 : Gsm.SignalQuality)}%,\r\n" +
                $"Rufweiterleitung auf >{Gsm.CallForwardingNumber}< " +
                $"ist{(Gsm.CallForwardingActive ? " " : " nicht")} aktiv.", "MelBox2 Neustart");

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
                        GetIniValues(); Gsm.SetupModem(); Console.WriteLine($"Initialisierungswerte wurden aus der Datenbank neu eingelesen."); 
                        break;
                    case "cls":
                        Console.Clear();
                        break;
                    case "modem reinit":
                        Gsm.SetupModem();
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

        private static void ReliableSerialPort_SerialPortErrorEvent(object sender, System.IO.Ports.SerialErrorReceivedEventArgs e)
        {
            Console.WriteLine("Fehler COM-Port: " + e);
            Log.Error("Fehler COM-Port: " + e, 1122);
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
                Message = "MelBox2: Simulierter SMS-Empfang. Test: 'Ä' 'Ü' 'Ö' 'ä' 'ü' 'ö' 'ß' Ende"
            };

            List<SmsIn> smsen = new List<SmsIn> { sms };

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
            sb.AppendLine("Modem Reinit".PadRight(32) + "Initialisiert das Modem neu.");             
            sb.AppendLine("Sms Read All".PadRight(32) + "Liest alle im Modemspeicher vorhandenen SMSen aus und zeigt sie in der Console an.");
            sb.AppendLine("Sms Read Sim".PadRight(32) + "Simuliert den Empfang einer SMS mit >MelBox2: Simulierter SMS-Empfang<.");
            sb.AppendLine("### HILFE ENDE ###");

            Console.WriteLine(sb.ToString());
        }


        //private static void Restart(int errorNum)
        //{
        //    ProcessStartInfo Info = new ProcessStartInfo();
        //    Info.Arguments = "/C ping 127.0.0.1 -n 2 && \"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"";
        //    Info.WindowStyle = ProcessWindowStyle.Hidden;
        //    Info.CreateNoWindow = true;
        //    Info.FileName = "cmd.exe";
        //    Process.Start(Info);
        //    Environment.Exit(errorNum);
        //}
    }
}
