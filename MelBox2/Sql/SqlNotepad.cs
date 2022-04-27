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
        /// <summary>
        /// Fügt eine Notiz in die Datenbank ein. Entfernt HTML-Markups.
        /// </summary>
        /// <param name="authorId"></param>
        /// <param name="customerId"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        internal static bool InsertNote(int authorId, int customerId, string content)
        {

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@AuthorId", authorId },
                { "@CustomerId", customerId },
                { "@Content", content
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")} //HTML-Markups entfernen
            };

            return NonQuery($"INSERT INTO Notepad (AuthorId, CustomerId, Content) VALUES (@AuthorId, @CustomerId, @Content);", args);
        }

        /// <summary>
        /// Ändert eine Notiz. Entfernt html-Markups
        /// </summary>
        /// <param name="id"></param>
        /// <param name="authorId"></param>
        /// <param name="customerId"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        internal static bool UpdateNote(int id, int authorId, int customerId, string content)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@ID", id},
                { "@Time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")},
                { "@AuthorId", authorId},
                { "@CustomerId", customerId},
                { "@Content", content
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")} //HTML-Markups entfernen                
            };

            const string query = "UPDATE Notepad SET Time = @Time, AuthorId = @AuthorId, CustomerId = @CustomerId, Content = @Content WHERE ID = @ID; ";

           return NonQuery(query, args);
        }


        public static DataTable SelectNote(int noteId)
        {
            string query = "SELECT ID, datetime(Bearbeitet, 'localtime') AS Bearbeitet, VonId, Von, KundeId, Kunde, Notiz FROM ViewNotepad WHERE ID = @ID;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@ID", noteId }
            };

            return SelectDataTable(query, args);
        }

        public static DataTable SelectLastNotes(int maxCount = 100)
        {
            string query = "SELECT ID, datetime(Bearbeitet, 'localtime') AS Bearbeitet, VonId, Von, KundeId, Kunde, Notiz FROM ViewNotepad ORDER BY Bearbeitet DESC LIMIT @LIMIT; "; 

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@LIMIT", maxCount }
            };

            return SelectDataTable(query, args);
        }

        /// <summary>
        /// Liste aller Personen, die nicht Firma "Kreutzt%" sind als HTML-Option für ein html-Select-Input
        /// </summary>
        /// <param name="selectedCustomerId">Id des selektierten Eintrags</param>
        /// <returns></returns>
        internal static string HtmlOptionCustomers(int selectedCustomerId)
        {            
            const string query = "SELECT ID, Name, Company FROM Person WHERE lower(Company) NOT LIKE 'kreutzt%' ORDER BY Name;";
            DataTable dt = SelectDataTable(query, null);
            string options = string.Empty;
          
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                int id = int.Parse(dt.Rows[i][0].ToString());
                string name = dt.Rows[i][1].ToString();
                string company = dt.Rows[i][2].ToString();

                if (company?.Length > 0 && name != company)
                    name += " (" + company + ")";

                string selected = (id == selectedCustomerId) ? "selected" : string.Empty;

                options += $"<option value='{id}' {selected}>{name}</option>" + Environment.NewLine;
            }

            return options;
        }

    }
}
