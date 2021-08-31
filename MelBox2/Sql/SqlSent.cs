using System;
using System.Collections.Generic;
using System.Data;
using static MelBoxGsm.Gsm;
using MelBoxGsm;

namespace MelBox2
{
    partial class Sql
    {
  

        public static DataTable SelectLastSent(int count = 1000)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@LIMIT", count}
            };

            const string query = "SELECT Gesendet, An, Inhalt, Ref, Via, Sendestatus AS Status FROM View_Sent ORDER BY Gesendet DESC LIMIT @LIMIT;";

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
                { "@Confirmation", Gsm.DeliveryStatus.QueuedToSend}
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
                { "@Confirmation", Gsm.DeliveryStatus.EmailNoStatus} //Da keine Email-Empfangsbestätigung implementiert: Gehe erstmal davon aus, dass Email ankommt.
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

        internal static void UpdateSent(int emailId, Gsm.DeliveryStatus confirmation) //ungetestet
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
