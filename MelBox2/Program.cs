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
                #if DEBUG
                    Log.Warning($"Debug: Es kann nur eine Instanz von {AppName} ausgeführt werden.", 739);
                #endif
                return;
            }

            Console.Title = "MelBox2";
   
            MelBoxGsm.CleanClose.CloseConsoleHandler += new CleanClose.EventHandler(CleanClose.Handler); //erzwungenes Beenden (X am Konsolenfenster)
            CleanClose.SetConsoleCtrlHandler(CleanClose.CloseConsoleHandler, true);
            Console.CancelKeyPress += Console_CancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit; //Normales Beenden 
      
#if DEBUG
            string startUpMsg = System.Reflection.Assembly.GetExecutingAssembly().Location + " [Debug-Kompilat] gestartet.";
#else
            string startUpMsg = System.Reflection.Assembly.GetExecutingAssembly().Location + " gestartet.";
#endif
            GetIniValues();

            Log.Info(startUpMsg, 121);
            Sql.InsertLog(3, startUpMsg);
            Console.WriteLine(startUpMsg);

            Server.Start();
            Sql.CheckDbFile();
            Sql.DbBackup();

            ShowHelp();

            ReliableSerialPort.SerialPortErrorEvent += ReliableSerialPort_SerialPortErrorEvent;
            ReliableSerialPort.SerialPortUnavailableEvent += ReliableSerialPort_SerialPortUnavailableEvent;
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
                if (input == null) continue;
                
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
                    case "restart":
                        Restart();
                        break;
                }

            }    
#if DEBUG
            Console.WriteLine("Progammende. Beliebige Taste zum beenden..");
            Console.ReadKey();
#endif
        }



        private static void ReliableSerialPort_SerialPortUnavailableEvent(object sender, int e)
        {
            //Neustart 
            ProcessStartInfo Info = new ProcessStartInfo();
            Info.Arguments = "/C ping 127.0.0.1 -n 5 && \"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"";
            Info.WindowStyle = ProcessWindowStyle.Normal;
            Info.CreateNoWindow = true;
            Info.FileName = "cmd.exe";
            Process.Start(Info);
            Environment.Exit(e);
        }

        private static void ReliableSerialPort_SerialPortErrorEvent(object sender, System.IO.Ports.SerialErrorReceivedEventArgs e)
        {
            Console.WriteLine("Fehler COM-Port: " + e);
            Log.Error("Fehler COM-Port: " + e, 1122);
        }

        private static void SmsRead_Sim()
        {
            Console.WriteLine("Simuliere den Empfang einer SMS.\r\nVon Telefonnummer (mind. 10 Zeichen):");
            string phone = Console.ReadLine();
            Console.WriteLine("Text der SMS (mind. 3 Zeichen):");
            string text = Console.ReadLine();

            SmsIn sms = new SmsIn
            {
                Index = 0,
                Phone = phone.Length > 9 ? phone : "+49123456789",
                Status = "REC UNREAD",
                TimeUtc = DateTime.UtcNow,
                Message = text.Length > 2 ? text : "MelBox2: Simulierter SMS-Empfang. Sonderzeichen: 'Ä' 'Ü' 'Ö' 'ä' 'ü' 'ö' 'ß' Ende"
            };

            Console.WriteLine($"Folgendes simulieren:\r\nSMS von >{sms.Phone}< empfangen mit Inhalt >{sms.Message}< ? (j/n)");
            
            if ( Console.ReadKey().Key != ConsoleKey.J)
            {
                Console.WriteLine("\r\nSimulation SMS-Empfang abgebrochen.");
                return;
            }

            List<SmsIn> smsen = new List<SmsIn> { sms };

            ParseNewSms(smsen);
            Console.WriteLine("SMS-Empfang wurde simuliert.");
        }

        private static void ShowHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\r\n### HILFE MELBOX CONSOLE ###");
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
            sb.AppendLine("Sms Read Sim".PadRight(32) + "Simuliert den Empfang einer SMS.");
            sb.AppendLine("### HILFE ENDE ###");

            Console.WriteLine(sb.ToString());
        }


        static void Restart()
        {
            ProcessStartInfo Info = new ProcessStartInfo();
            Info.Arguments = "/C ping 127.0.0.1 -n 2 && \"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"";
            Info.WindowStyle = ProcessWindowStyle.Normal;
            Info.CreateNoWindow = true;
            Info.FileName = "cmd.exe";
            Process.Start(Info);
            Environment.Exit(4444);
        }

    }
}
