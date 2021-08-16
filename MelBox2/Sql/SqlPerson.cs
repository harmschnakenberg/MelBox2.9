using System;
using System.Collections.Generic;
using System.Data;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    partial class Program
    {
        public enum Via
        {
            Undefined,
            Sms,
            Email,
            SmsAndEmail,
            PermanentEmail
        }

        public static int Level_Admin { get; set; } = 9000; //Benutzerverwaltung u. -Einteilung
        public static int Level_Reciever { get; set; } = 2000; //Empfänger bzw. Bereitschaftsnehmer

        private static string Encrypt(string password)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(password);
            data = new System.Security.Cryptography.SHA256Managed().ComputeHash(data);
            return System.Text.Encoding.UTF8.GetString(data);
        }

        private static Person GetPerson(DataTable dt)
        {
            if (dt.Rows.Count == 0) return new Person();

            Person p = new Person
            {
                Id = (int)dt.Rows[0]["ID"],
                Name = dt.Rows[0]["Name"].ToString(),
                Level = (int)dt.Rows[0]["Level"],
                Company = dt.Rows[0]["Company"].ToString(),
                Phone = dt.Rows[0]["Phone"].ToString(),
                Email = dt.Rows[0]["Email"].ToString(),
                Via = (Via)dt.Rows[0]["Via"],
                KeyWord = dt.Rows[0]["KeyWord"].ToString(),
                MaxInactive = (int)dt.Rows[0]["MaxInactive"]
            };

            return p;
        }

        private static Person SelectPerson(int id)
        {

            const string query = "SELECT ID, Name, Level, Company, Phone, Email, Via, KeyWord, MaxInactive FROM Person WHERE ID = @ID;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@ID", id }
            };

            DataTable dt = SelectDataTable(query, args);

            return GetPerson(dt);
        }

        internal static Person NewPerson(Dictionary<string, string> payload)
        {
            Person p = new Person();

            if (payload.TryGetValue("Id", out string strId))
                 p.Id = int.Parse(strId);

            if (payload.TryGetValue("name", out string name))
                p.Name = name;

            if (payload.TryGetValue("password", out string password))
                p.Password = password;

            if (payload.TryGetValue("company", out string company))
                p.Company = company;

            if (payload.TryGetValue("viaEmail", out string viaEmail))
                p.Via += 2;

            if (payload.TryGetValue("email", out string email))
                p.Email = email;

            if (payload.TryGetValue("viaPhone", out string viaPhone))
                p.Via += 1;

            if (payload.TryGetValue("phone", out string phoneStr))
                p.Phone = phoneStr;

            if (payload.TryGetValue("keyWord", out string keyWord))
                p.KeyWord = keyWord;

            if (payload.TryGetValue("MaxInactiveHours", out string maxInactiveHoursStr))
                p.MaxInactive = int.Parse(maxInactiveHoursStr);

            if (payload.TryGetValue("Accesslevel", out string accesslevelStr))
                p.Level = int.Parse(accesslevelStr);

            return p;
        }

        private static Person SelectOrCreatePerson(SmsIn sms)
        {
            string keyWord = GetKeyWord(sms.Message);

            const string query = "SELECT ID, Name, Level, Company, Phone, Email, Via, KeyWord, MaxInactive FROM Person WHERE Phone = @Phone OR KeyWord = @KeyWord;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Phone", sms.Phone },
                { "@KeyWord", keyWord }
            };

            DataTable dt = SelectDataTable(query, args);

            if (dt.Rows.Count == 0 && NonQuery($"INSERT INTO Person (Name, Level, Phone, KeyWord) VALUES ('Neu_@Phone', 0, @Phone, @KeyWord); ", args))
                return SelectOrCreatePerson(sms);

            return GetPerson(dt);
        }

        private static Person SelectPerson(SmsOut smsOut)
        {
            SmsIn smsIn = new SmsIn()
            {
                Phone = smsOut.Phone,
                Message = smsOut.Message
            };

            return SelectOrCreatePerson(smsIn);
        }

        private static Person SelectPerson(System.Net.Mail.MailAddress email)
        {
            const string query = "SELECT ID, Name, Level, Company, Phone, Email, Via, KeyWord, MaxInactive FROM Person WHERE Email = @Email AND Name = @Name;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Email", email.Address },
                { "@Name", email.DisplayName }
            };

            DataTable dt = SelectDataTable(query, args);

            if (dt.Rows.Count == 0 && NonQuery($"INSERT INTO Person (Name, Level, Email) VALUES ('@Email', 0, @Email); ", args))
                return SelectPerson(email);

            return GetPerson(dt);
        }

        private static Person Authentification(string name, string password)
        {
            string encryped_pw = Encrypt(password);

            const string query = "SELECT ID, Name, Level, Company, Phone, Email, Via, KeyWord, MaxInactive FROM Person WHERE Name = @Name AND Password = @Password AND Level > 0;";

            Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@Name", name },
                    { "@Password", encryped_pw }
                };

            DataTable dt = SelectDataTable(query, args);

            return GetPerson(dt);
        }

        internal static string CheckCredentials(string name, string password)
        {
            Person p;

            try
            {
                p = Authentification(name, password);
            }
            catch (Exception)
            {
                throw;
                // Was tun?
            }

            if (p.Id > 0)
            {
                while (Server.LogedInHash.Count > 10) //Max. 10 Benutzer gleichzetig eingelogged
                {
                    Server.LogedInHash.Remove(Server.LogedInHash.Keys.GetEnumerator().Current);
                }

                string guid = Guid.NewGuid().ToString("N");

                Server.LogedInHash.Add(guid, p);

                return guid;
            }

            return string.Empty;
        }

        internal static bool InsertPerson(string name, string password, int level, string company, string phone, string email, Via via, int maxInactiveHours)
        {
            const string query = "INSERT INTO Person (Name, Password, Level, CompanyId, Phone, Email, Via, MaxInactive) VALUES (@Name,@Password, @Level, @Company, @Phone, @Email, @Via, @MaxInactive); ";

            Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@Name", name },
                    { "@Password", Encrypt(password) },
                    { "@Level", level },
                    { "@Company", company },
                    { "@Phone", phone },
                    { "@Email", email},
                    { "@Via", via},
                    { "@MaxInactive", maxInactiveHours}
                };

            return NonQuery(query, args);
        }

        private static bool UpdatePerson(int id, string name, string password, int accesslevel, string company, string phone, string email, int via, string keyWord, int maxInactive)
        {
            string query = "UPDATE Person SET Name = @Name, Level = @Level, Company = @Company, Phone = @Phone, Email = @Email, Via = @Via, KeyWord = @KeyWord, MaxInactive = @MaxInactive " +
                            (password.Length > 3 ? ", Password = @Password " : string.Empty) +
                            "WHERE ID = @ID; ";

            Dictionary<string, object> args = new Dictionary<string, object>()
            {
                { "@ID", id },
                { "@Name", name },
                { "@Level", accesslevel },
                { "@Company", company },
                { "@Phone", phone },
                { "@Email", email },
                { "@Via", via },
                { "@KeyWord", keyWord?? string.Empty },
                { "@MaxInactive", maxInactive }
            };

            if (password.Length > 3) args.Add("@Password", Encrypt(password));

            return NonQuery(query, args);
        }

        private static bool DeletePerson(int id)
        {
            const string query = "DELETE FROM Person WHERE ID = @ID; ";

            Dictionary<string, object> args = new Dictionary<string, object>()
            {
                { "@ID", id }
            };

            return NonQuery(query, args);
        }

        /// <summary>
        /// Füllt ein HTML-Select Element mit Optionen
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private static string HtmlOptionContacts(Person p)
        {
            const string queryAdmin = "SELECT ID, Name, Level, Company AS Firma FROM Person WHERE Level >= @Recieverlevel ORDER BY Name;";
            const string queryUser = "SELECT ID, Name, Level, Company AS Firma FROM Person WHERE ID = @ID";

            Dictionary<string, object> argsAdmin = new Dictionary<string, object>() { { "@Level", Level_Reciever } };
            Dictionary<string, object> argsUser = new Dictionary<string, object>() { { "@ID", p.Id } };

            DataTable dt;

            if (p.Level >= Level_Admin)
                dt = SelectDataTable(queryAdmin, argsAdmin);
            else
                dt = SelectDataTable(queryUser, argsUser);

            string options = string.Empty;
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                int id = int.Parse(dt.Rows[i][0].ToString());
                string name = dt.Rows[i][1].ToString();
                string selected = (id == p.Id) ? "selected" : string.Empty;

                if (p.Level >= Level_Admin || id == p.Level)
                    options += $"<option value='{id}' {selected}>{name}</option>" + Environment.NewLine;
            }

            return options;

        }

    }

    internal class Person
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Password { get; set; }

        public int Level { get; set; }

        public string Company { get; set; }

        public string Phone { get; set; }

        public string Email { get; set; }

        public Program.Via Via { get; set; }

        public string KeyWord { get; set; }

        public int MaxInactive { get; set; }

        /*
                    "CREATE TABLE IF NOT EXISTS Person ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Name TEXT NOT NULL, " +
                    "Password TEXT, " +
                    "Level INTEGER DEFAULT 0, " +
                    "CompanyId INTEGER, " +
                    "Phone TEXT, " +
                    "Email TEXT, " +
                    "Via INTEGER, " +
                    "KeyWord TEXT, " +
                    "MaxInactive INTEGER, " +
        */
    }

    }
