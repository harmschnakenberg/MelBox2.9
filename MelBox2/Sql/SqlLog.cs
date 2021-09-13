using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelBox2
{
    partial class Sql
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

        public static DataTable SelectLastLogs(int maxRows = 300,  int maxPrio = 3)
        {
            string query = "SELECT Id, datetime(Time, 'localtime') AS Zeit, Prio, Content AS Eintrag FROM Log WHERE Prio <= @Prio ORDER BY Time DESC LIMIT @LIMIT;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@LIMIT", maxRows },
                { "@Prio", maxPrio }
            };

            return SelectDataTable(query, args);
        }

        public static bool DeleteLogExeptLast(int deleteUntil)
        {
            const string query = "DELETE FROM Log WHERE ID NOT IN ( SELECT ID FROM Log ORDER BY Time DESC LIMIT @Limit)";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Limit", deleteUntil }
            };

            return NonQuery(query, args);
        }


    }
}
