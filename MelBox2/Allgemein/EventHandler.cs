using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;
using MelBoxGsm;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    partial class Program
    {
        /// <summary>
        /// Verhindert das häufige Senden von Statusinformationen zum Mobilfunkempfang
        /// </summary>
        static bool Gsm_NetworkStatusNotify = true;

        /// <summary>
        /// Verhindert das häufige Senden von Statusinformationen zum Mobilfunkempfang
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void NotificationTimeout_Elapsed(object sender, ElapsedEventArgs e) { Gsm_NetworkStatusNotify = true; }

        /// <summary>
        /// Benachrichtigt, wenn ein Problem mit dem Mobilfunk besteht.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="quality"></param>
        private static void Gsm_NetworkStatusEvent(object sender, int quality)
        {
            //Ist die Mobilfunkverbindung zu schlecht, Nachricht an Admin
            if(quality < 20 && Gsm_NetworkStatusNotify) // bei MelBox1 Grenze 20%
            {
                Gsm_NetworkStatusNotify = false;
                Timer NotificationTimeout = new Timer(600000); //10 min
                NotificationTimeout.Elapsed += NotificationTimeout_Elapsed;
                NotificationTimeout.AutoReset = false;
                NotificationTimeout.Start(); //Verhindert zu häufige Benachrichtigung

                string txt = $"Das GSM-Modem ist nicht mit dem Mobilfunknetz verbunden bzw. der Empfangslevel ist zu niedrig \r\n Empfangslevel {quality}% < Grenzwert 20%";
                Log.Warning(txt, 1515);
                Email.Send(Email.Admin,txt, "MelBox2: Kein Mobilfunkempfang");
            }
        }

        /// <summary>
        /// Wird ausgeführt bevor das Programm-Fenster beendet wird. 
        /// Wird nicht bei 'brutalem beenden' durch X am Konsolengfenster odder Shutdown getriggert!
        /// SerialPort daher nochmal möglichst separat abfangen!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Log.Info(AppName + " ordnungsgemäß beendet.", 99);
            Server.Stop();
            Gsm.ModemShutdown();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Log.Info($"{AppName} durch Tastenkombination Strg + {e.SpecialKey} beendet.", 98);
            Server.Stop();
            Gsm.ModemShutdown();
        }


        /// <summary>
        /// Ein neuer Sprachanruf wurde erkannt.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Gsm_NewCallRecieved(object sender, string e)
        {
            //Gsm.SetCallForewarding( Sql.GetCurrentCallForwardingNumber(Program.OverideCallForwardingNumber) ); //Rufumleitung während des eingehenden Anrufs umstellen? Besser nicht..

            SmsIn dummy = new SmsIn
            {
                Phone = e,
                Message = $"Sprachanruf von >{e}< nach {RingSecondsBeforeCallForwarding} Sek. weitergeleitet an >{CallForwardingNumber}<",
                TimeUtc = DateTime.UtcNow
            };

            Console.WriteLine(dummy.Message);
            Sql.InsertRecieved(dummy);

            Email.Send(Email.Admin, $"Sprachanruf {dummy.TimeUtc.ToLocalTime()} weitergeleitet an >{CallForwardingNumber}<.", $"Sprachanruf >{e}<", true);
        }

        static readonly Timer emailTimer = new Timer();

        /// <summary>
        /// Das GSM-Modem hat einen Fehler gemeldet
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Gsm_NewErrorEvent(object sender, string e)
        {
#if !DEBUG
            if (!emailTimer.Enabled) // Sende Emails mit GSM-Fehlermeldung nicht häufiger als im Abstand von 30 sec.
            {
                emailTimer.Interval = 30000;
                emailTimer.AutoReset = false;
                emailTimer.Enabled = true;
                emailTimer.Elapsed += EmailTimer_Elapsed;
                emailTimer.Start();
            }            
#endif
            Log.Warning("GSM-Fehlermeldung - " + e, 1320);
            Sql.InsertLog(1, "Fehlermeldung Modem: " + e);            
        }

        private static void EmailTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Email.Send(Email.Admin, Gsm.LastError.Item1 + " MelBox2 GSM-Fehlermeldung - " + Gsm.LastError.Item2);
            emailTimer.Enabled = false;
        }

        /// <summary>
        /// Neue SMS(en) empfangen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Gsm_SmsRecievedEvent(object sender, List<SmsIn> e)
        {
            ParseNewSms(e);
        }

        /// <summary>
        /// Neue Empfangsbestätigung(en) empfangen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Gsm_SmsReportEvent(object sender, Report e)
        {
            Sql.InsertReport(e); //Später auskommentieren?
            Sql.UpdateSent(e);
        }

        /// <summary>
        /// Das Versenden einer SMS ist fehlgeschlagen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Gsm_FailedSmsSendEvent(object sender, SmsOut e)
        {
            string txt = $"Senden von SMS an >{e.Phone}< fehlgeschlagen. Noch {Gsm.MaxSendTrysPerSms - e.SendTryCounter} Versuche. Nachricht >{e.Message}<";

            Sql.InsertLog(Gsm.MaxSendTrysPerSms - e.SendTryCounter, txt);

            Report fake = new Report
            {
                DeliveryStatus = (int)(MaxSendTrysPerSms < e.SendTryCounter ? Gsm.DeliveryStatus.SendRetry : Gsm.DeliveryStatus.ServiceDenied),
                Reference = e.Reference,
                DischargeTimeUtc = e.SendTimeUtc
            };

            Sql.UpdateSent(fake);
        }

        /// <summary>
        /// Eine SMS zum Senden wurde nicht vom GSM-Modem angenommen (SIM-Karte nicht bereit, Speicherfehler, ungültige Telefonnummer, ...)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">SMS, die hätte gesendet werden sollen</param>
        private static void Gsm_FailedSmsCommission(object sender, SmsOut e)
        {
            Sql.InsertLog(1, $"Sendebefehl von Modem nicht quittiert: SMS an >{e.Phone}< >{e.Message}< wurde nicht versandt.");
        }

        /// <summary>
        /// Eine SMS ist erfolgreich versendet worden
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Gsm_SmsSentEvent(object sender, SmsOut e)
        {
            Sql.InsertSent(e);
        }

        private static void ReliableSerialPort_SerialPortUnavailableEvent(object sender, int e)
        {
            //Neustart 
            ProcessStartInfo Info = new ProcessStartInfo
            {
                Arguments = "/C ping 127.0.0.1 -n 10 && \"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"",
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = true,
                FileName = "cmd.exe"
            };
            Process.Start(Info);
            Environment.Exit(e);
        }

        private static void ReliableSerialPort_SerialPortErrorEvent(object sender, System.IO.Ports.SerialErrorReceivedEventArgs e)
        {
            Console.WriteLine("Fehler COM-Port: " + e);
            Log.Error("Fehler COM-Port: " + e, 1122);
        }

        private static void EmailListener_EmailInEvent(object sender, System.Net.Mail.MailMessage e)
        {
            e.Body = Email.ChangeEncoding(e.Body, e.BodyEncoding, System.Text.Encoding.UTF8); //email-Inhalt in UTF8 konvertieren wg. Umlaute
            ParseNewEmail(e);
        }

    }
}
