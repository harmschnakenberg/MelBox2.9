using MelBoxGsm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MelBox2
{
    partial class Program
    {
        public static readonly string AppName = Process.GetCurrentProcess().ProcessName;

        static void Main()
        {
            #region nur eine Instanz des Progamms zulassen
            if (Process.GetProcessesByName(AppName).Length > 1)
            {
                //if (Args.Length == 0)
#if DEBUG
                    Log.Warning($"Debug: Es kann nur eine Instanz von {AppName} ausgeführt werden.", 739);
#endif
                return;
            }

            #endregion
            #region COM-Port vorhanden?
            if (System.IO.Ports.SerialPort.GetPortNames()?.Length < 1)
            {
                string txt = "Es ist kein Modem angeschlossen (kein COM-Port registriert). Programm beendet.";
                Console.WriteLine(txt);
                Log.Error(txt, 5000);
                //Sql.InsertLog(1, txt);
                System.Threading.Thread.Sleep(5000);
                return;
            }
            #endregion

            Console.Title = "MelBox2";

            #region Aufräumen beim Beenden sicherstellen
            MelBoxGsm.CleanClose.CloseConsoleHandler += new CleanClose.EventHandler(CleanClose.Handler); //erzwungenes Beenden (X am Konsolenfenster)
            CleanClose.SetConsoleCtrlHandler(CleanClose.CloseConsoleHandler, true);
            Console.CancelKeyPress += Console_CancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit; //Normales Beenden 
            #endregion

            GetIniValues();

            #region Programmstart protokollieren
#if DEBUG
            string startUpMsg = System.Reflection.Assembly.GetExecutingAssembly().Location + " [Debug-Kompilat] gestartet.";
#else
            string startUpMsg = System.Reflection.Assembly.GetExecutingAssembly().Location + " gestartet.";
#endif
            Log.Info(startUpMsg, 121);
            Sql.InsertLog(3, AppName + " gestartet.");
            Console.WriteLine(DateTime.Now.ToShortTimeString() + " - " + startUpMsg);
            #endregion

            #region Startsequenz
            startUpTimer.Start();
            startUpTimer.Elapsed += StartUpTimer_Elapsed;

            Server.Start();
            Sql.CheckDbFile();
            Sql.DbBackup();
            #endregion

            ShowHelp();

            #region Ereignisse abonieren
            ReliableSerialPort.SerialPortErrorEvent += ReliableSerialPort_SerialPortErrorEvent;
            ReliableSerialPort.SerialPortUnavailableEvent += ReliableSerialPort_SerialPortUnavailableEvent;
            Gsm.NewErrorEvent += Gsm_NewErrorEvent;
            Gsm.NetworkStatusEvent += Gsm_NetworkStatusEvent;
            Gsm.SmsRecievedEvent += Gsm_SmsRecievedEvent;
            Gsm.SmsSentEvent += Gsm_SmsSentEvent;
            Gsm.FailedSmsSendEvent += Gsm_FailedSmsSendEvent;
            Gsm.FailedSmsCommission += Gsm_FailedSmsCommission;
            Gsm.SmsReportEvent += Gsm_SmsReportEvent;
            Gsm.NewCallRecieved += Gsm_NewCallRecieved;
            #endregion

            CheckCallForwardingNumber(null, null);

            Gsm.SetupModem();

            SetHourTimer(null, null);

            Scheduler.CeckOrCreateWatchDog();

            EmailListener emailListener = new EmailListener(); // "imap.gmx.net", 993, "harmschnakenberg@gmx.de", "Oyterdamm64!", true);
            Console.WriteLine("Automatische E-Mail Empfangsbenachrichtigung " + (emailListener.IsIdleEmailSupported() ? "aktiviert." : "wird nicht unterstützt."));
            emailListener.EmailInEvent += EmailListener_EmailInEvent;
            emailListener.ReadUnseen();

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
                        {
                            GetIniValues();
                            Gsm.SetupModem();
                            Console.WriteLine($"Initialisierungswerte wurden aus der Datenbank neu eingelesen.");
                            Log.Info("Einlesen der Initialisierungswerte aus der Datenbank wurde manuell aus der Konsole angestoßen.", 4112);
                        }
                        break;
                    case "cls":
                        Console.Clear();
                        break;
                    case "modem reinit":
                        Gsm.SetupModem();
                        break;
                    case "modem status":
                        GetModemParameters();
                        break;
                    case "call forward":
                        Console.WriteLine("Weiterleitung Sprachanrufe an Telefonnummer: (+49...)");
                        string phone = Console.ReadLine();
                        Console.WriteLine($"Sprachanrufe an die Telefonnummer {phone} weiterleiten (j/n)?");
                        if (Console.ReadKey().Key == ConsoleKey.J)
                        {
                            Gsm.SetCallForewarding(phone);
                            Console.WriteLine($"Sprachanrufe werden an die Telefonnummer {Gsm.CallForwardingNumber} weitergeleitet.");
                        }
                        break;
                    case "sms read sim":
                        SmsRead_Sim();
                        break;
                    case "sms read all":
                        List<SmsIn> list = Gsm.SmsRead("ALL");
                        Console.WriteLine($"Es konnten {list.Count} Nachrichten aus dem Modemspeicher gelesen werden.");
                        foreach (SmsIn sms in list)
                            Console.WriteLine($"[{sms.Index}] {sms.TimeUtc.ToLocalTime()} Tel. >{sms.Phone}< >{sms.Message}<");
                        break;
                    case "email read":
                        EmailListener listener = new EmailListener();
                        listener.EmailInEvent += EmailListener_EmailInEvent;
                        listener.ReadUnseen();
                        System.Threading.Thread.Sleep(2000);
                        listener.EmailInEvent -= EmailListener_EmailInEvent;
                        listener.Dispose();
                        break;
                    case "debug":
                        Console.WriteLine($"Aktuelles Debug-Byte: {(int)ReliableSerialPort.Debug}. Neuer Debug?");
                        Console.WriteLine($"{(int)ReliableSerialPort.GsmDebug.AnswerGsm}\tAntwort von Modem");
                        Console.WriteLine($"{(int)ReliableSerialPort.GsmDebug.RequestGsm}\tAnfrage an Modem");
                        Console.WriteLine($"{(int)ReliableSerialPort.GsmDebug.UnsolicatedResult}\tEreignisse von Modem");
                        string x = Console.ReadLine();
                        if (byte.TryParse(x, out byte d))
                        {
                            ReliableSerialPort.Debug = (ReliableSerialPort.GsmDebug)d;
                            Console.WriteLine("Neuer Debug-Level: " + d);
                        }
                        break;
                    case "restart":
                        {
                            Log.Info("Die Anwendung wurde mit dem Befehl 'Restart' manuell neugestartet.", 4445);
                            ReliableSerialPort_SerialPortUnavailableEvent(null, 4445);
                        }
                        break;
                    case "import contact":
                        Console.WriteLine("Kontakte als CSV-Datei der Form 'AbsName;AbsInakt;AbsNr;AbsKey;' laden. Siehe alte Tabelle 'Tbl_Absender'. Optionale Spalte 'AbsEmail;AbsLevel'. Dateipfad angeben:");
                        string path = Console.ReadLine();
                        Sql.LoadPersonsFromCsv(path);
                        break;
                }

            }

            emailListener.Dispose();
#if DEBUG
            Console.WriteLine("Progammende. Beliebige Taste zum beenden..");
            Console.ReadKey();
#endif
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

            if (Console.ReadKey().Key != ConsoleKey.J)
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
            sb.AppendLine("Modem Status".PadRight(32) + "Zeigt die wichtigsten aktuellen Verbindungsdaten zum Modem an.");
            sb.AppendLine("Restart".PadRight(32) + "beendet das Programm und startet es nach 5 Sek. neu.");
            sb.AppendLine("Sms Read All".PadRight(32) + "Liest alle im Modemspeicher vorhandenen SMSen aus und zeigt sie in der Console an.");
            sb.AppendLine("Sms Read Sim".PadRight(32) + "Simuliert den Empfang einer SMS (wird ggf. an Bereitschaft weitergeleitet).");
            sb.AppendLine("Email Read".PadRight(32) + "Liest auf dem Mailserver vorhandenen neue (ungelesen) E-Mails.");

            sb.AppendLine("Import Contact".PadRight(32) + "Kontaktdaten aus altem MelBox per CSV-Datei importieren.");
            sb.AppendLine("### HILFE ENDE ###");

            Console.WriteLine(sb.ToString());
        }

        private static void GetModemParameters()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"\r\n### GSM MODEM STATUS {DateTime.Now}  ###");
            sb.AppendLine("Modemtype".PadRight(32) + Gsm.ModemType);
            sb.AppendLine("Angeschlossen an".PadRight(32) + Gsm.SerialPortName);
            sb.AppendLine("Kommunikationsmodus".PadRight(32) + (Gsm.IsGsmTextMode ? "Text" : "PDU"));
            sb.AppendLine("Zeichensatz".PadRight(32) + Gsm.GsmCharacterSet);
            sb.AppendLine("SIM Status".PadRight(32) + Gsm.SimPinStatus);
            sb.AppendLine("SIM Hinterlegter PIN".PadRight(32) + Gsm.SimPin);
            sb.AppendLine("SIM Telefonnummer".PadRight(32) + $"{Gsm.OwnNumber} {Gsm.OwnName}");
            sb.AppendLine("Mobilfunknetzempfang".PadRight(32) + $"{Gsm.SignalQuality}%");
            sb.AppendLine("Mobilfunknetzstatus überwachen".PadRight(32) + (Gsm.IsNetworkRegistrationNotificationActive ? "ja" : "nein"));
            sb.AppendLine("Mobilfunknetzstatus".PadRight(32) + Gsm.NetworkRegistration);
            sb.AppendLine("Mobilfunknetzbetreiber".PadRight(32) + Gsm.ProviderName);
            sb.AppendLine("SMS Servicecenter".PadRight(32) + Gsm.SmsServiceCenterAddress);
            sb.AppendLine("SMS Speicherbelegung".PadRight(32) + $"{Gsm.SmsStorageCapacityUsed} / {Gsm.SmsStorageCapacity}");
            sb.AppendLine("SMS Empfangsbestätigung".PadRight(32) + $"warte max. {Gsm.TrackingTimeoutMinutes} Minuten");
            sb.AppendLine("SMS max. Sendeversuche".PadRight(32) + Gsm.MaxSendTrysPerSms);
            sb.AppendLine("Rufweiterleitung".PadRight(32) + (Gsm.CallForwardingActive ? $"nach {Gsm.RingSecondsBeforeCallForwarding} Sek. an {Gsm.CallForwardingNumber}" : "deaktiviert"));
            sb.AppendLine("Debug-Telefonnummer".PadRight(32) + Gsm.AdminPhone);
            sb.AppendLine($"Letzter Fehler".PadRight(32) + $"{Gsm.LastError.Item1} >{Gsm.LastError.Item2}<");
            sb.AppendLine("### GSM MODEM STATUS ENDE ###");

            Console.WriteLine(sb.ToString());
        }

    }
}
