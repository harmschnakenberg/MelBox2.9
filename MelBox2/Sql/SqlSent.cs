using System;
using System.Collections.Generic;
using System.Data;
using static MelBoxGsm.Gsm;
using MelBoxGsm;

namespace MelBox2
{
    partial class Sql
    {
        //Delivery-Status<st> - Quelle: https://en.wikipedia.org/wiki/GSM_03.40#Discharge_Time

        /// <summary>
        /// 0-31:     delivered or, more generally, other transaction completed.
        /// 32-63:    still trying to deliver the message.
        /// 64-127:   not making any more delivery attempts.
        /// Weitere Werte sind selbst vergeben.
        /// </summary>
        public enum MsgConfirmation
        {
            NoDeliveryYet = 3, //Wenn Empfangshandy aus
            NoDelivery = 6,//
            Success = 31,
            ReportPending = 63,
            SmsAborted = 127,
            Unknown = 256,
            NoReference = 257,
            SmsSendRetry = 259,
            EmailSendRetry = 260,
            EmailSendAborted = 261
        }
        //Console.WriteLine($"Empfangsbestätigung erhalten für ID[{reference}]: " + (delviveryStatus > 63 ? "Senden fehlgeschlagen" : delviveryStatus > 31 ? "senden im Gange" : "erfolgreich versendet"));


        public static DataTable SelectLastSent(int count = 1000)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@LIMIT", count}
            };

            const string query = "SELECT Gesendet, An, Inhalt, Ref, Via, Sendestatus FROM View_Sent ORDER BY Gesendet DESC LIMIT @LIMIT;";

            return SelectDataTable(query, args);
        }

        internal static void InsertSent(SmsOut sms) //ungetestet
        {
            Person sender = SelectPerson(sms);
            Message msg = SelectOrCreateMessage(sms.Message);

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", sms.SendTimeUtc.ToString("yyyy-MM-dd HH:mm:ss")},
                { "@ToId", sender.Id},
                { "@ContentId", msg.Id},
                { "@Reference", sms.Reference},
                { "@Confirmation", sms.Reference == 0 ? MsgConfirmation.NoReference : MsgConfirmation.ReportPending}
            };

            const string query = "INSERT INTO Sent(Time, ToId, Via, ContentId, Reference, Confirmation) VALUES(@Time, @ToId, 1, @ContentId, @Reference, @Confirmation); ";

            _ = NonQuery(query, args);
        }

        internal static void InsertSent(System.Net.Mail.MailAddress email, string message, int reference) //ungetestet
        {
            Person sender = SelectPerson(email);
            Message msg = SelectOrCreateMessage(message);

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")},
                { "@ToId", sender.Id},
                { "@ContentId", msg.Id},
                { "@Reference", reference},
                { "@Confirmation", MsgConfirmation.Success} //Da keine Email-Empfangsbestätigung implementiert: Gehe erstmal davon aus, dass Email ankommt.
            };

            const string query = "INSERT INTO Sent(Time, ToId, Via, ContentId, Reference, Confirmation) VALUES(@Time, @ToId, 2, @ContentId, @Reference, @Confirmation); ";

            _ = NonQuery(query, args);
        }

        internal static void UpdateSent(Report report) //ungetestet
        {
            //Nur den letzten Eintrag mit passender Referenz ändern. Wenn nicht genau genug, SmSOut-Objekt von Tracking hier mit auswerten.

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", report.DischargeTimeUtc.ToString("yyyy-MM-dd HH:mm:ss")},
                { "@Confirmation", report.DeliveryStatus},
                { "@Reference", report.Reference}
            };

            const string query = "UPDATE Sent SET Time = @Time, Confirmation = @Confirmation WHERE Reference IN ( SELECT Reference FROM Sent WHERE Reference = @Reference ORDER BY Id DESC LIMIT 1);";

           _ = NonQuery(query, args);
        }

        internal static void UpdateSent(int emailId, MsgConfirmation confirmation) //ungetestet
        {
            //Nur den letzten Eintrag mit passender Referenz ändern. Wenn nicht genau genug, SmSOut-Objekt von Tracking hier mit auswerten.

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")},
                { "@Confirmation", confirmation},
                { "@Reference", emailId}
            };

            const string query = "UPDATE Sent SET Time = @Time, Confirmation = @Confirmation WHERE Reference IN (SELECT Reference FROM Sent WHERE Reference = @Reference ORDER BY Id DESC LIMIT 1);";

            _ = NonQuery(query, args);
        }


        internal static void InsertReport(Report report) //ungetestet
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", report.DischargeTimeUtc.ToString("yyyy-MM-dd HH:mm:ss")},
                { "@Reference", report.Reference},
                { "@DeliveryCode", report.DeliveryStatus}
            };

            const string query = "INSERT INTO Report (Time, Reference, DeliveryCode) VALUES (@Time, @Reference, @DeliveryCode); ";

            _ = NonQuery(query, args);
        }
    }
}
