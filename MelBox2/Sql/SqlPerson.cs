using MelBoxGsm;
using System;
using System.Collections.Generic;
using System.Data;

namespace MelBox2
{
    partial class Sql
    {
        public enum Via
        {
            Undefined = 0,
            Sms = 1,
            Email = 2,
            SmsAndEmail = 3,
            PermanentEmail = 4,
            PermanentEmailAndSms = 5,
            EmailWhitelist = 8,
            NoCalls = 16
        }

        public static int Level_Admin { get; set; } = 9000; //Benutzerverwaltung u. -Einteilung
        public static int Level_Reciever { get; set; } = 2000; //Empfänger bzw. Bereitschaftsnehmer

        private static string Encrypt(string password)
        {
            if (password == null) return password;

            byte[] data = System.Text.Encoding.UTF8.GetBytes(password);
            data = new System.Security.Cryptography.SHA256Managed().ComputeHash(data);
            return System.Text.Encoding.UTF8.GetString(data);
        }

        private static Person GetPerson(DataTable dt)
        {
            if (dt == null || dt.Rows.Count == 0) return new Person();

            Person p = new Person();

            if (int.TryParse(dt.Rows[0]["ID"].ToString(), out int id))
                p.Id = id;

            if (dt.Rows[0]["Name"] != null)
                p.Name = dt.Rows[0]["Name"].ToString();

            if (int.TryParse(dt.Rows[0]["Level"].ToString(), out int level))
                p.Level = level;

            if (dt.Rows[0]["Company"] != null)
                p.Company = dt.Rows[0]["Company"].ToString();

            if (dt.Rows[0]["Phone"] != null)
                p.Phone = dt.Rows[0]["Phone"].ToString();

            if (dt.Rows[0]["Email"] != null)
                p.Email = dt.Rows[0]["Email"].ToString();

            if (dt.Rows[0]["KeyWord"] != null)
                p.KeyWord = dt.Rows[0]["KeyWord"].ToString();

            if (int.TryParse(dt.Rows[0]["MaxInactive"].ToString(), out int maxInactive))
                p.MaxInactive = maxInactive;

            if (int.TryParse(dt.Rows[0]["Via"].ToString(), out int via))
                p.Via = (Via)via;


            return p;
        }

        internal static Person SelectPerson(int id)
        {

            const string query = "SELECT ID, Name, Level, Company, Phone, Email, Via, KeyWord, MaxInactive FROM Person WHERE ID = @ID;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@ID", id }
            };

            DataTable dt = SelectDataTable(query, args);

            return GetPerson(dt);
        }

        internal static Person SelectPerson(string name)
        {

            const string query = "SELECT ID, Name, Level, Company, Phone, Email, Via, KeyWord, MaxInactive FROM Person WHERE Name = @Name;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Name", name }
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

            if (payload.TryGetValue("viaEmail", out string viaEmail) && viaEmail.Length > 0)
                p.Via += (int)Via.Email;

            if (payload.TryGetValue("viaAlwaysEmail", out string viaAlwaysEmail) && viaAlwaysEmail.Length > 0)
                p.Via += (int)Via.PermanentEmail;

            if (payload.TryGetValue("onEmailWhitelist", out string onEmailWhitelist) && onEmailWhitelist.Length > 0)
                p.Via += (int)Via.EmailWhitelist;

            if (payload.TryGetValue("email", out string email))
                p.Email = email;

            if (payload.TryGetValue("viaPhone", out string viaPhone) && viaPhone.Length > 0)
                p.Via += (int)Via.Sms;

            if (payload.TryGetValue("noCalls", out string noCalls) && noCalls.Length > 0)
                p.Via += (int)Via.NoCalls;

            if (payload.TryGetValue("phone", out string phoneStr))
                p.Phone = NormalizePhone(phoneStr);

            if (payload.TryGetValue("keyWord", out string keyWord))
                p.KeyWord = keyWord;

            if (payload.TryGetValue("MaxInactiveHours", out string maxInactiveHoursStr))
                p.MaxInactive = int.Parse(maxInactiveHoursStr);

            if (payload.TryGetValue("Accesslevel", out string accesslevelStr))
                p.Level = int.Parse(accesslevelStr);

            return p;
        }

        internal static string NormalizePhone(string phone)
        {
            // as VB 
            // Entfernt Zeichen aus psAbsNr sodass in Absendertabelle danach gesucht werden kann
            // siehe Erläuterungen im TextFile1

            if (phone == null || phone.Length < 4) return string.Empty;

            phone = phone.Replace(" ", "");
            phone = phone.Replace(",", "");
            phone = phone.Replace(";", "");
            phone = phone.Replace(":", "");
            phone = phone.Replace(".", "");
            phone = phone.Replace("-", "");
            phone = phone.Replace("(", "");
            phone = phone.Replace(")", "");

            if (phone.StartsWith("+"))
                return phone;
            else if (phone.StartsWith("00"))
                phone = "+" + phone.Remove(0, 2);
            else if (phone.StartsWith("0"))
                phone = "+49" + phone.TrimStart('0');

            if (phone[3] == '0')
                phone = phone.Remove(3, 1);

            return phone;
        }

        internal static Person SelectOrCreatePerson(SmsIn sms)
        {
            string keyWord = GetKeyWord(sms.Message);

            //Erst nach Keyword suchen, da Phone nicht eindeutig sein kann.
            const string query1 = "SELECT ID, Name, Level, Company, Phone, Email, Via, KeyWord, MaxInactive FROM Person WHERE Phone = @Phone AND (KeyWord IS NULL OR length(KeyWord) = 0 OR LOWER(KeyWord) = @KeyWord) ORDER BY KeyWord DESC; ";
            const string query2 = "INSERT INTO Person (Name, Company, Level, Phone, Via, KeyWord) VALUES ('Neu_' || @Phone, '', 0, @Phone, 0, @KeyWord); ";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Phone", sms.Phone },
                { "@KeyWord", keyWord }
            };

            DataTable dt = SelectDataTable(query1, args);

            if (dt.Rows.Count == 0 && NonQuery(query2, args))
            {
                return SelectOrCreatePerson(sms);
            }

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

        internal static Person SelectOrCreatePerson(System.Net.Mail.MailAddress email)
        {
            //Suchen, ob vorhanden
            const string query1 = "SELECT ID, Name, Level, Company, Phone, Email, Via, KeyWord, MaxInactive FROM Person WHERE lower(Email) = @Email OR Name = @Name; ";
            //Neu erstellen
            const string query2 = "INSERT INTO Person (Name, Level, Email) VALUES ('Neu_' || @Name, 0, @Email); ";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Name", email.DisplayName },
                { "@Email", email.Address.ToLower() }
            };

            DataTable dt = SelectDataTable(query1, args);

            if (dt.Rows.Count == 0 && NonQuery(query2, args))
            {
                System.Threading.Thread.Sleep(1000);
                return SelectOrCreatePerson(email);
            }

            return GetPerson(dt);
        }

        internal static string CheckCredentials(string name, string password)
        {
            try
            {
                string encryped_pw = Encrypt(password);

                const string query = "SELECT ID, Name, Level, Company, Phone, Email, Via, KeyWord, MaxInactive FROM Person WHERE Name = @Name AND Password = @Password AND Level > 0;";

                Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@Name", name },
                    { "@Password", encryped_pw }
                };

                DataTable dt = SelectDataTable(query, args);

                Person p = GetPerson(dt);

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
            }
            catch (Exception)
            {
                throw;
                // Was tun?
            }

            return string.Empty;
        }

        internal static bool InsertPerson(string name, string password, int level, string company, string phone, string email, Via via, int maxInactiveHours)
        {
            const string query = "INSERT INTO Person (Name, Password, Level, Company, Phone, Email, Via, MaxInactive) VALUES (@Name, @Password, @Level, @Company, @Phone, @Email, @Via, @MaxInactive); ";

            Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@Name", name },
                    { "@Password", Encrypt(password) },
                    { "@Level", level },
                    { "@Company", company },
                    { "@Phone", NormalizePhone(phone) },
                    { "@Email", email },
                    { "@Via", (int)via},
                    { "@MaxInactive", maxInactiveHours}
                };

            return NonQuery(query, args);
        }

        /// <summary>
        /// Hilfsmethode zum Importieren von Kontaktdaten aus MelBox1. 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="phone"></param>
        /// <param name="maxInactiveHours"></param>
        /// <param name="keyWord"></param>
        /// <returns></returns>
        private static bool ImportPerson(string name, string phone, int maxInactiveHours, string keyWord, string company, string email = "", int level = 0)
        {
            const string query = "INSERT INTO Person (Name, Password, Level, Company, Phone, Email, Via, KeyWord, MaxInactive) VALUES (@Name, @Password, @Level, @Company, @Phone, @Email, @Via, @KeyWord, @MaxInactive); ";

            Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@Name", name },
                    { "@Password", Encrypt(name) },
                    //{ "@Password", DBNull.Value },
                    { "@Level", level },
                    { "@Company", company },
                    { "@Phone", NormalizePhone(phone) },
                    { "@Email", email},
                    { "@Via", (int)Via.Undefined},
                    { "@KeyWord", keyWord.ToLower()},
                    { "@MaxInactive", maxInactiveHours}
                };
            return NonQuery(query, args);
        }

        internal static bool UpdatePerson(int id, string name, string password, int accesslevel, string company, string phone, string email, int via, string keyWord, int maxInactive)
        {
            try
            {
                string query = "UPDATE Person SET Name = @Name, Level = @Level, Company = @Company, Phone = @Phone, Email = @Email, Via = @Via, KeyWord = @KeyWord, MaxInactive = @MaxInactive " +
                                (password?.Length > 3 ? ", Password = @Password " : string.Empty) +
                                "WHERE ID = @ID; ";

                Dictionary<string, object> args = new Dictionary<string, object>()
            {
                { "@ID", id },
                { "@Name", name?? string.Empty },
                { "@Level", accesslevel },
                { "@Company", company?? string.Empty  },
                { "@Phone", NormalizePhone( phone?? string.Empty ) },
                { "@Email", email?? string.Empty  },
                { "@Via", via },
                { "@KeyWord", keyWord?? string.Empty },
                { "@MaxInactive", maxInactive }
            };

                if (password?.Length > 3) args.Add("@Password", Encrypt(password));

                return NonQuery(query, args);
            }
            catch
            {
                return false;
            }
        }

        internal static bool DeletePerson(int id)
        {
            const string query = "DELETE FROM Person WHERE ID = @ID; ";

            Dictionary<string, object> args = new Dictionary<string, object>()
            {
                { "@ID", id }
            };

            return NonQuery(query, args);
        }

        /// <summary>
        /// Gibt in Abhänigkeit von Benutzerrechten eine html-Optionsliste von Benutzern für ein DropDown-Menu aus. 
        /// </summary>
        /// <param name="p">Person-Objekt der eingeloggten Person</param>
        /// <param name="selectPersonId">ID, der Person, die ausgewählt sein soll</param>
        /// <returns>html-Optionsliste für DropDown-Menu</returns>
        internal static string HtmlOptionContacts(Person p, int selectPersonId)
        {
            int selectedId = (selectPersonId == 0 ? p.Id : selectPersonId);
            const string queryAdmin = "SELECT ID, Name, Level, Company AS Firma FROM Person WHERE Level > 0 ORDER BY Name;";
            const string queryUser = "SELECT ID, Name, Level, Company AS Firma FROM Person WHERE ID = @ID";

            // Dictionary<string, object> argsAdmin = new Dictionary<string, object>() { { "@Level", Level_Reciever } };
            Dictionary<string, object> argsUser = new Dictionary<string, object>() { { "@ID", p.Id } };

            DataTable dt;

            if (p.Level >= Level_Admin)
                dt = SelectDataTable(queryAdmin, null); // argsAdmin);
            else
                dt = SelectDataTable(queryUser, argsUser);

            string options = string.Empty;
            string readOnly = dt.Rows.Count == 1 ? " readonly" : string.Empty;

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                int id = int.Parse(dt.Rows[i][0].ToString());
                string name = dt.Rows[i][1].ToString();
                string selected = (id == selectedId) ? "selected" : string.Empty;

                if (p.Level >= Level_Admin || id == p.Id)
                    options += $"<option value='{id}' {selected}{readOnly}>{name}</option>" + Environment.NewLine;
            }

            return options;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user">Person Objekt des anfragenden (Berechtigung)</param>
        /// <param name="company">Filter nach Firmennamen. erstes zeichen '-' = Ausschließender Filter</param>
        /// <returns></returns>
        internal static DataTable SelectViewablePersons(Person user, string company = "")
        {
            company = (company == null) ? string.Empty : company.ToLower();

            Dictionary<string, object> args1 = new Dictionary<string, object>()
            {
                { "@ID", user.Id },
                { "@CallF", Gsm.CallForwardingNumber }
            };

            Dictionary<string, object> args2 = new Dictionary<string, object>()
            {
                { "@Level", user.Level },
                { "@CallF", Gsm.CallForwardingNumber },
                { "@Company",  "%" + company.TrimStart('-') + "%" }
            };

            const string query1 = "SELECT ID, Name, Company AS Firma, Level, Phone AS Telefon, Email, " +
                "CASE WHEN (Via & 8) > 0 THEN 'z' WHEN (Via & 8) < 1 THEN '0' END || " +
                "CASE WHEN Phone = @CallF THEN 'y' WHEN Phone != @CallF THEN '0' END || " +
                "CASE WHEN (Via & 4) > 0 THEN 'x' WHEN (Via & 4) < 1 THEN '0' END || " +
                "CASE WHEN (Via & 2) > 0 THEN 'v' WHEN (Via & 2) < 1 THEN '0' END || " +
                "CASE WHEN (Via & 16) > 0 THEN 'u' WHEN (Via & 16) < 1 THEN '0' END || " +
                "CASE WHEN (Via & 1) > 0 THEN 'w' WHEN (Via & 1) < 1 THEN '0' END AS Attribut " +
                "FROM Person WHERE ID = @ID;";

            const string query2 = "SELECT ID, Name, Company AS Firma, Level, Phone AS Telefon, Email, " +
                "CASE WHEN (Via & 8) > 0 THEN 'z' WHEN (Via & 8) < 1 THEN '0' END || " +
                "CASE WHEN Phone = @CallF THEN 'y' WHEN Phone != @CallF THEN '0' END || " +
                "CASE WHEN (Via & 4) > 0 THEN 'x' WHEN (Via & 4) < 1 THEN '0' END || " +
                "CASE WHEN (Via & 2) > 0 THEN 'v' WHEN (Via & 2) < 1 THEN '0' END || " +
                "CASE WHEN (Via & 16) > 0 THEN 'u' WHEN (Via & 16) < 1 THEN '0' END || " +
                "CASE WHEN (Via & 1) > 0 THEN 'w' WHEN (Via & 1) < 1 THEN '0' END AS Attribut " +
                "FROM Person WHERE Level <= @Level AND lower(Company) LIKE @Company ORDER BY Name;";

            const string query3 = "SELECT ID, Name, Company AS Firma, Level, Phone AS Telefon, Email, " +
                "CASE WHEN (Via & 8) > 0 THEN 'z' WHEN (Via & 8) < 1 THEN '0' END || " +
                "CASE WHEN Phone = @CallF THEN 'y' WHEN Phone != @CallF THEN '0' END || " +
                "CASE WHEN (Via & 4) > 0 THEN 'x' WHEN (Via & 4) < 1 THEN '0' END || " +
                "CASE WHEN (Via & 2) > 0 THEN 'v' WHEN (Via & 2) < 1 THEN '0' END || " +
                "CASE WHEN (Via & 16) > 0 THEN 'u' WHEN (Via & 16) < 1 THEN '0' END || " +
                "CASE WHEN (Via & 1) > 0 THEN 'w' WHEN (Via & 1) < 1 THEN '0' END AS Attribut " +
                "FROM Person WHERE Level <= @Level AND lower(Company) NOT LIKE @Company ORDER BY Name;";

            if (user.Level >= Level_Admin)
                if (company.StartsWith("-"))
                    return SelectDataTable(query3, args2);
                else
                    return SelectDataTable(query2, args2);
            else
                return SelectDataTable(query1, args1);
        }

        /// <summary>
        /// Stellt fest, ob 'mailAddress' Emails an dieses Programm senden darf.
        /// </summary>
        /// <param name="mailAddress">E-Mail-Adresse eines Absenders, der geprüft werden soll.</param>
        /// <returns>true = Der Absender malAddress darf Emails an dieses Programm senden.</returns>
        public static bool IsKnownAddress(System.Net.Mail.MailAddress mailAddress)
        {
            //nur bekannte E-Mail-Adressen zulassen
            string query = "SELECT Count(ID) FROM Person WHERE lower(Email) = @Email;"; // AND (Via & @Via) > 0; ";

            Dictionary<string, object> args = new Dictionary<string, object>{
                { "@Email", mailAddress.Address.ToLower() }
                //{ "@Via", (int)Via.EmailWhitelist }
            };

            if (!int.TryParse(SelectValue(query, args).ToString(), out int result))
                return false;

            return result > 0;
        }

        /// <summary>
        /// Import von Kontaktdaten aus MelBox1: Tabelle tbl_Absender als CSV einladen.
        /// Erforderliche Spalten in CSV: 'AbsName;AbsInakt;AbsNr;AbsRegelID;AbsKey;' Optionale Spalten in CSV: 'AbsEmail'
        /// </summary>
        /// <param name="path">pfad zur CSV-Datei</param>
        public static void LoadPersonsFromCsv(string path)
        {
            Console.WriteLine("Versuche Kontakte aus CSV-Datei zu importieren...");

            if (!System.IO.File.Exists(path))
            {
                Console.WriteLine($"Der angegebene Pfad >{path}< ist ungültig. Import abgebrochen.");
                return;
            }

            int counter = 0;

            try
            {
                string[] lines = System.IO.File.ReadAllLines(path, System.Text.Encoding.GetEncoding("ISO-8859-1")); //wg. Umlaute!
                string[] captions = lines[0].Split(';');


                int iCol = captions.Length;
                int iName = 0;
                int iAbsInakt = 0;
                int iAbsNr = 0;
                int iAbsKey = 0;
                int iAbsEmail = 0;
                int iAbsLevel = 0;

                for (int i = 0; i < iCol; i++)
                {
                    switch (captions[i])
                    {
                        case "AbsName":
                            iName = i;
                            break;
                        case "AbsInakt":
                            iAbsInakt = i;
                            break;
                        case "AbsNr":
                            iAbsNr = i;
                            break;
                        case "AbsKey":
                            iAbsKey = i;
                            break;
                        case "AbsEmail":
                            iAbsEmail = i;
                            break;
                        case "AbsLevel":
                            iAbsLevel = i;
                            break;
                    }
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    string[] values = lines[i].Split(';');

                    if (values.Length < iCol)
                    {
                        Log.Warning($"Fehler beim Import von Kontaktdaten aus CSV-File >{path}< Zeile {i} hat die falsche Spaltenanzahl.", 26534);
                        continue;
                    }

                    _ = int.TryParse(values[iAbsInakt], out int maxInactive);

                    int level = 0;
                    if (iAbsLevel > 0)
                        _ = int.TryParse(values[iAbsLevel], out level);

                    string name = values[iName];
                    string email = string.Empty;
                    string company = string.Empty;

                    if (iAbsEmail > 0)
                    {
                        email = values[iAbsEmail];

                        if (email.ToLower().Contains("@kreutztraeger."))
                            company = "Kreutzträger Kältetechnik";
                        //int indexAT = email.IndexOf('@') + 1;
                        //int indexLastDot = email.LastIndexOf('.');

                        //if (indexLastDot - indexAT > 0)
                        //{
                        //    company = email.Substring(indexAT, indexLastDot - indexAT);
                        //    company = company.Substring(0, 1).ToUpper() + company.Substring(1); //erster Bustabe groß
                        //}
                    }

                    if (name.Length > 2)
                    {
                        Person p = Sql.SelectPerson(name);

                        if (p.Id > 0)
                            Console.WriteLine($"Zeile {i} >{name}<".PadRight(32) + " bereits vergeben. KEIN NEUER EINTRAG!");
                        else if (ImportPerson(name, values[iAbsNr], maxInactive, values[iAbsKey], company, email, level))
                        {
                            Console.WriteLine($"Zeile {i} >{name}< ".PadRight(32) + $">{values[iAbsNr]}< ");
                            counter++;
                        }
                        else
                            Console.WriteLine($"Zeile {i} >{name}< ".PadRight(32) + $">{values[iAbsNr]}<\tFehler beim Schreiben in die Datenbank. KEIN NEUER EINTRAG!");
                    }
                }

                Console.WriteLine($"Es wurden {counter} Kontakte mit Name=Passwort über CSV-Import hinzugefügt. Ergebnis prüfen!");
                Log.Info($"Es wurden {counter} Kontakte über CSV-Import hinzugefügt.", 5623);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                Log.Warning($"Fehler beim Import von Kontaktdaten aus CSV-File >{path}< {ex.Message} \r\n{ex.StackTrace}", 26535);
            }
#pragma warning restore CA1031 // Do not catch general exception types
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

        public Sql.Via Via { get; set; }

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
