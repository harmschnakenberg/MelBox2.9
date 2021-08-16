using System;
using System.Collections.Generic;
using System.Data;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    partial class Program
    {
        public static DataTable SelectLastSent(int count = 1000)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@LIMIT", count}
            };

            const string query = "SELECT datetime(Gesendet, 'localtime') AS Gesendet, An, Inhalt, Ref, Via, Sendestatus FROM View_Sent ORDER BY Gesendet DESC LIMIT @LIMIT;";

            return SelectDataTable(query, args);
        }

        internal static void InsertSent(SmsOut sms) //ungetestet
        {
            Person sender = SelectPerson(sms);
            Message msg = SelectOrCreateMessage(sms.Message);

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", sms.SendTimeUtc},
                { "@ToId", sender.Id},
                { "@ContentId", msg.Id},
                { "@Reference", sms.Reference}
            };

            const string query = "INSERT INTO Sent(Time, ToId, Via, ContentId, Reference) VALUES(@Time, @ToId, 1, @ContentId, @Reference); ";

            _ = NonQuery(query, args);
        }

        internal static void InsertSent(System.Net.Mail.MailAddress email, string message, int reference) //ungetestet
        {
            Person sender = SelectPerson(email);
            Message msg = SelectOrCreateMessage(message);

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", DateTime.UtcNow},
                { "@ToId", sender.Id},
                { "@ContentId", msg.Id},
                { "@Reference", reference}
            };

            const string query = "INSERT INTO Sent(Time, ToId, Via, ContentId, Reference) VALUES(@Time, @ToId, 2, @ContentId, @Reference); ";

            _ = NonQuery(query, args);
        }

        internal static void UpdateSent(Report report) //ungetestet
        {
            //Nur den letzten Eintrag mit passender Referenz ändern. Wenn nicht genau genug, SmSOut-Objekt von Tracking hier mit auswerten.

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", report.DischargeTimeUtc},
                { "@Confirmation", report.DeliveryStatus},
                { "@Reference", report.Reference}
            };

            const string query = "UPDATE Sent(Time, Confirmation) VALUES (@Time, @Confirmation) WHERE Reference IN (SELECT Reference FROM Sent WHERE Reference = @Reference ORDER BY Id DESC LIMIT 1);";

           _ = NonQuery(query, args);
        }

        internal static void UpdateSent(int emailId, int deliveryStatus) //ungetestet
        {
            //Nur den letzten Eintrag mit passender Referenz ändern. Wenn nicht genau genug, SmSOut-Objekt von Tracking hier mit auswerten.

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", DateTime.UtcNow},
                { "@Confirmation", deliveryStatus},
                { "@Reference", emailId}
            };

            const string query = "UPDATE Sent(Time, Confirmation) VALUES (@Time, @Confirmation) WHERE Reference IN (SELECT Reference FROM Sent WHERE Reference = @Reference ORDER BY Id DESC LIMIT 1);";

            _ = NonQuery(query, args);
        }

    }
}
