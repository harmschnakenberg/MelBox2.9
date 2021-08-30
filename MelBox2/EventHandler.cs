using System;
using System.Collections.Generic;
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
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Log.Info(AppName + " beendet.", 99);
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
            SmsIn dummy = new SmsIn
            {
                Phone = e,
                Message = $"Sprachanruf von >{e}< nach >{RingSecondsBeforeCallForwarding}< Sek. weitergeleitet an >{CallForwardingNumber}<",
                TimeUtc = DateTime.UtcNow
            };

            Sql.InsertRecieved(dummy);

            Email.Send(Email.Admin, $"Sprachanruf {dummy.TimeUtc.ToLocalTime()} weitergeleitet an >{CallForwardingNumber}<.", $"Sprachanruf >{e}<", true);
        }

        /// <summary>
        /// Das GSM-Modem hat einen Fehler gemeldet
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Gsm_NewErrorEvent(object sender, string e)
        {
            Log.Warning("GSM-Fehlermeldung - " + e, 1320);
            Sql.InsertLog(1, "Fehlermeldung Modem: " + e);
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
                DeliveryStatus = (int)(MaxSendTrysPerSms < e.SendTryCounter ? Sql.MsgConfirmation.SmsSendRetry : Sql.MsgConfirmation.SmsAborted),
                Reference = e.Reference,
                DischargeTimeUtc = e.SendTimeUtc
            };

            Sql.UpdateSent(fake);
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


    }
}
