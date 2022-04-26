using System;
using System.Collections.Generic;
using System.Data;
using MelBoxGsm;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    partial class Sql
    {
        
        internal static DataTable SelectLastRecieved(string sender)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Sender", sender}
            };

            const string query = "SELECT Nr, Empfangen, Von, Inhalt FROM View_Recieved WHERE Von LIKE '%' || @Sender || '%' ORDER BY Empfangen DESC LIMIT 1000;";

            return SelectDataTable(query, args);
        }


        internal static DataTable SelectRecieved(System.DateTime date)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Date", date }
            };

            const string query = "SELECT Nr, Empfangen, Von, Inhalt FROM View_Recieved WHERE date(Empfangen) = date(@Date) ORDER BY Empfangen DESC;";

            return SelectDataTable(query, args);
        }


        public static Message SelectRecieved(int recId)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@recId", recId}
            };

            const string query = "SELECT r.ContentId AS ID, m.Content, m.BlockDays, m.BlockStart, m.BlockEnd FROM Recieved r JOIN Message AS m ON m.ID = r.ContentId  WHERE r.ID = @recId";

            DataTable dt = SelectDataTable(query, args);
            
            return GetMessage(dt);
        }


        internal static bool InsertRecieved(SmsIn sms) 
        {
            Person sender = SelectOrCreatePerson(sms);
            Message msg = SelectOrCreateMessage(sms.Message);

            Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@Time", sms.TimeUtc.ToString("yyyy-MM-dd HH:mm:ss")},
                    { "@SenderId", sender.Id},
                    { "@ContentId", msg.Id }
                };

            const string query = "INSERT INTO Recieved (Time, SenderId, ContentId) VALUES (@Time, @SenderId, @ContentId);";

           return NonQuery(query, args);
        }

        internal static bool InsertRecieved(System.Net.Mail.MailMessage email) 
        {
            
            Person sender = SelectOrCreatePerson(email.From);
            Message msg = SelectOrCreateMessage( RemoveHTMLTags(email.Body) ); //Emails ohne HTML-Tags speichern

            //Sendezeit aus E-Mail-Header lesen. Bei unplausiblen Werten aktuelle Zeit nehmen
            if (!DateTime.TryParse(email.Headers["Date"], out DateTime emailDate) || emailDate.CompareTo(DateTime.Now) > 0)
                emailDate = DateTime.Now;
#if DEBUG
            Console.WriteLine($"E-Mail empfangen von {email.From.Address}:\r\n\t" +
                $"Übermittelte Zeit: {email.Headers["Date"]}\r\n\t" +
                $"Ermittelte Sendezeit {emailDate}\r\n\t" +
                $"Datenbankeintrag {emailDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")}");
#else
            Console.WriteLine($"{emailDate.ToShortTimeString()}: E-Mail empfangen von {email.From.Address}");
#endif
            Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@Time", emailDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")},
                    { "@SenderId", sender.Id},
                    { "@ContentId", msg.Id }
                };

            const string query = "INSERT INTO Recieved (Time, SenderId, ContentId) VALUES (@Time, @SenderId, @ContentId);";

            return NonQuery(query, args);
        }



        internal static DataTable SelectOverdueSenders()
        {
            const string query = "SELECT * FROM View_Overdue ORDER BY Fällig_seit DESC; ";

            return SelectDataTable(query, null);
        }


        internal static DataTable SelectWatchedSenders()
        {
            const string query = "SELECT * FROM View_WatchedSenders; ";

            return SelectDataTable(query, null);
        }

       
    }


}
