using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelBox2
{
    partial class Program
    {
        internal static void InsertLog(int prio, string content)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Prio", prio },
                { "@Content", content }
            };

            _ = NonQuery($"INSERT INTO Log (Prio, Content) VALUES (@Prio, @Content);", args);
        }

        public static DataTable SelectLastLogs(int maxPrio = 3)
        {
            string query = "SELECT Id, datetime(Time, 'localtime') AS Zeit, Prio, Content AS Eintrag FROM Log WHERE Prio <= @Prio ORDER BY Time DESC LIMIT 1000;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Prio", maxPrio }
            };

            return SelectDataTable(query, args);
        }

        public static bool DeleteLogUntil(System.DateTime deleteUntil)
        {
            string query = $"DELETE FROM Log WHERE Time < @Time";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Time", deleteUntil.ToString("yyyy-MM-dd HH:mm:ss") }
            };

            return NonQuery(query, args);
        }

    }
}
