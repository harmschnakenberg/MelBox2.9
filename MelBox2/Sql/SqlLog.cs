using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

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

        public static DataTable SelectLastLogs(int maxRows = 300, int maxPrio = 3)
        {           
            const string query = "SELECT Id, datetime(Time, 'localtime') AS Zeit, 'P'|| Prio AS Prio, Content AS Eintrag FROM Log WHERE Prio <= @Prio ORDER BY Time DESC LIMIT @LIMIT;";
             
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@LIMIT", maxRows },
                { "@Prio", maxPrio }
            };

            return SelectDataTable(query, args);
        }

        public static DataTable SelectLogRange(int startId, int count = 300, int maxPrio = 3)
        {          
            const string query = "SELECT Id, datetime(Time, 'localtime') AS Zeit, 'P'|| Prio AS Prio, Content AS Eintrag FROM Log WHERE Id >= @StartId AND Prio <= @Prio ORDER BY Time DESC LIMIT @LIMIT;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@StartId", startId },
                { "@Prio", maxPrio },
                { "@LIMIT", count }                
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

        #region GSM-Signal

        internal static void InsertGsmSignal(int quality)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Quality", quality }
            };

            _ = NonQuery($"INSERT INTO GsmSignal (SignalQuality) VALUES (@Quality);", args);
        }

        internal static void ConsolidateGsmSignal()
        {
            string query = "SELECT ROUND(AVG(SignalQuality)) AS AVG, MIN(Time) AS MIN, MAX(Time) AS MAX FROM GsmSignal;";
            DataTable dt = SelectDataTable(query, null);

            int.TryParse(dt.Rows[0][0].ToString(), out int avgSignalQuality);
            DateTime.TryParse(dt.Rows[0][1].ToString(), out DateTime begin);
            DateTime.TryParse(dt.Rows[0][2].ToString(), out DateTime end);

            if (begin != DateTime.MinValue)
                InsertLog(4, $"Mobilfunknetzsignal &Oslash; {avgSignalQuality}% von {begin.ToLocalTime()} bis {end.ToLocalTime()}");

            string query2 = "DELETE FROM GsmSignal";
            _ = NonQuery(query2, null);
        }
        #endregion

    }
}
