﻿using System.Collections.Generic;
using System.Data;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    partial class Program
    {

        internal static DataTable SelectLastRecieved(int count = 1000)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@LIMIT", count}
            };

            const string query = "SELECT Nr, datetime(Empfangen, 'localtime') AS Empfangen, Von, Inhalt FROM View_Recieved ORDER BY Empfangen DESC LIMIT @LIMIT;";

            return SelectDataTable(query, args);
        }

        public static DataTable SelectRecieved(int recId)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@recId", recId}
            };

            const string query = "SELECT r.ContentId AS Nr, m.Content AS Inhalt, m.BlockDays, m.BlockStart, m.BlockEnd FROM Recieved r JOIN Message AS m ON m.ID = r.ContentId  WHERE r.ID = @recId";

            return SelectDataTable(query, args);
        }


        internal static void InsertRecieved(List<SmsIn> smsen) //ungetestet
        {
            foreach (SmsIn sms in smsen)
            {
                Person sender = SelectOrCreatePerson(sms);
                Message msg = SelectOrCreateMessage(sms.Message);

                Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@Time", sms.TimeUtc},
                    { "@SenderId", sender.Id},
                    { "@ContentId", msg.Id }
                };

                const string query = "INSERT INTO Recieved (Time, SenderId, ContentId) VALUES (@Time, @SenderId, @ContentId);";

                _ = NonQuery(query, args);
            }
        }

     

    }
}
