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

        internal static string GetKeyWord(string message)
        {
            char[] split = new char[] { ' ', ',', '-', '.', ':', ';' };
            string[] words = message.Split(split);

            string KeyWords = words[0].Trim();

            if (words.Length > 1)
            {
                KeyWords += " " + words[1].Trim();
            }

            return KeyWords.ToLower();
        }

        internal static bool IsMessageBlockedNow(Message msg)
        {
           
            if ((msg.BlockDays >> (int)DateTime.Now.DayOfWeek) == 0) return false;
            //heute potenziell gesperrt

            if (msg.BlockStart > msg.BlockEnd)
            {
                // Sperre wirkt über Tagsprung
                if ((DateTime.Now.Hour >= msg.BlockStart && DateTime.Now.Hour <= 23) || DateTime.Now.Hour >= 0 && DateTime.Now.Hour < msg.BlockEnd)
                    return true;
            }
            else if (msg.BlockStart < msg.BlockEnd)
            {
                // Sperre innerhalb eines Tages
                if (DateTime.Now.Hour >= msg.BlockStart && DateTime.Now.Hour < msg.BlockEnd)
                    return true;
            }
            else
            {
                // Sperre 24h
                return true;
            }

            return false;
        }

        internal static bool IsMessageBlockedNow(string msg)
        {
            return IsMessageBlockedNow(SelectOrCreateMessage(msg));
        }

        private static Message SelectOrCreateMessage(string message) //ungetestet
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Message", message }
            };

            const string query = "INSERT OR IGNORE INTO Message (Content) VALUES (@Message); SELECT ID, Time, Content, BlockDays, BlockStart, BlockEnd FROM Message WHERE Content = @Message; ";

            DataTable dt = Program.SelectDataTable(query, args);

            if (dt.Rows.Count == 0) return new Message();

            _ = int.TryParse(dt.Rows[0]["ID"].ToString(), out int id);
            _ = int.TryParse(dt.Rows[0]["BlockDays"].ToString(), out int blockDays);
            _ = int.TryParse(dt.Rows[0]["BlockStart"].ToString(), out int blockStart);
            _ = int.TryParse(dt.Rows[0]["BlockEnd"].ToString(), out int blockEnd);
            _ = DateTime.TryParse(dt.Rows[0]["Time"].ToString(), out DateTime time);

            Message msg = new Message
            {
                Id = id,
                Time = time,
                Content = dt.Rows[0]["Content"].ToString(),
                BlockDays = blockDays,
                BlockStart = blockStart,
                BlockEnd = blockEnd
            };

            return msg;
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

        public static DataTable Blocked_View(string content = "")
        {
            string query = "SELECT * FROM View_Blocked ";

            if (content.Length > 2) query += " WHERE Inhalt LIKE '%" + content + "%'"; //eleganter machen!

            query += " ORDER BY Nachricht ASC;";

            return SelectDataTable(query, null);
        }

        internal static bool UpdateMessage(int id, int blockDays, int blockStart, int blockEnd) //ungetestet
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@ID", id},
                { "@BlockDays", blockDays},
                { "@BlockStart", blockStart},
                { "@BlockEnd", blockEnd},
            };

            const string query = "UPDATE Message SET BlockDays = @BlockDays, BlockStart = @BlockStart, BlockEnd = @BlockEnd WHERE ID = @ID; ";

            return NonQuery(query, args);
        }

    }

    internal class Message
    {
        public int Id { get; set; }

        public DateTime Time { get; set; }

        public string Content { get; set; }

        public int BlockDays { get; set; }

        public int BlockStart { get; set; }

        public int BlockEnd { get; set; }
    }

}
