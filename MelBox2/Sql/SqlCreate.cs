using System;
using System.IO;

namespace MelBox2
{
    partial class Sql
    {
        public static string DbPath { get; set; } = Path.Combine(@"C:\MelBox2", "DB", "MelBox2.db");

        private static void CreateNewDataBase()
        {
            Log.Info("Erstelle eine neue Datenbank-Datei unter " + DbPath, 21405);

            try
            {
                //Erstelle Datenbank-Datei und öffne einmal zum Testen
                _ = Directory.CreateDirectory(Path.GetDirectoryName(DbPath));
                FileStream stream = File.Create(DbPath);
                stream.Close();

                string query = "CREATE TABLE IF NOT EXISTS Log ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "Prio INTEGER NOT NULL, " +
                    "Content TEXT " +
                    "); ";

                query += "CREATE TABLE IF NOT EXISTS SendWay ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY, " +
                    "Way TEXT " +
                    "); ";

                query += "CREATE TABLE IF NOT EXISTS Person ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Name TEXT NOT NULL, " +
                    "Password TEXT, " +
                    "Level INTEGER DEFAULT 0, " +
                    "Company TEXT, " +
                    "Phone TEXT, " +
                    "Email TEXT, " +
                    "Via INTEGER, " +
                    "KeyWord TEXT, " +
                    "MaxInactive INTEGER DEFAULT 0, " +

                    "CONSTRAINT fk_Via FOREIGN KEY (Via) REFERENCES SendWay (ID) ON DELETE SET NULL " +
                    "); ";

                query += "CREATE TABLE IF NOT EXISTS Message ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "Content TEXT NOT NULL UNIQUE, " +
                    "BlockDays INTEGER, " +
                    "BlockStart INTEGER, " +
                    "BlockEnd INTEGER " +
                    "); ";

                query += "CREATE TABLE IF NOT EXISTS Recieved ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "SenderId INTEGER, " +
                    "ContentId INTEGER, " +

                    "CONSTRAINT fk_SenderId FOREIGN KEY (SenderId) REFERENCES Person (ID) ON DELETE SET DEFAULT, " +
                    "CONSTRAINT fk_ContentId FOREIGN KEY (ContentId) REFERENCES Message (ID) ON DELETE SET DEFAULT " +
                    "); ";

                query += "CREATE TABLE IF NOT EXISTS Sent ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "ToId INTEGER NOT NULL, " +
                    "Via INTEGER, " +
                    "ContentId INTEGER, " +
                    "Reference INTEGER, " +
                    "Confirmation INTEGER DEFAULT " + (int)MelBoxGsm.Gsm.DeliveryStatus.Simulated + ", " + 

                    "CONSTRAINT fk_ToId FOREIGN KEY (ToId) REFERENCES Person (ID) ON DELETE SET DEFAULT, " +
                    "CONSTRAINT fk_Via FOREIGN KEY (Via) REFERENCES SendWay (ID) ON DELETE SET NULL, " +
                    "CONSTRAINT fk_ContentId FOREIGN KEY (ContentId) REFERENCES Message (ID) ON DELETE SET DEFAULT " +
                    "); ";

                //Report-Tabelle nur zum testen. Später rausnehmen?
                query += "CREATE TABLE Report ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "Reference INTEGER, " +
                    "DeliveryCode INTEGER " +
                    "); ";

                query += "CREATE TABLE IF NOT EXISTS Shift ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY, " +
                    "PersonId INTEGER, " +
                    "Start TEXT NOT NULL UNIQUE, " +
                    "End TEXT NOT NULL UNIQUE, " +
                    "CONSTRAINT fk_PersonId FOREIGN KEY (PersonId) REFERENCES Person (ID) ON DELETE SET DEFAULT " +
                    "); ";

                query += "CREATE TABLE IF NOT EXISTS Ini ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY, " +                    
                    "Property TEXT NOT NULL UNIQUE, " +
                    "Value TEXT NOT NULL " +                    
                    "); ";

                query += "CREATE VIEW ViewYearFromToday AS " +
                    "SELECT CASE(CAST(strftime('%w', d) AS INT) +6) % 7 WHEN 0 THEN 'Mo' WHEN 1 THEN 'Di' WHEN 2 THEN 'Mi' WHEN 3 THEN 'Do' WHEN 4 THEN 'Fr' WHEN 5 THEN 'Sa' ELSE 'So' END AS Tag, d FROM(WITH RECURSIVE dates(d) AS(VALUES(date('now')) " +
                    "UNION ALL " +
                    "SELECT date(d, '+1 day') FROM dates WHERE d<date('now', '+1 year')) SELECT d FROM dates ) WHERE d NOT IN(SELECT date(Start) FROM Shift WHERE date(Start) >= date('now') " +
                    "); ";

                query += "CREATE VIEW View_Recieved AS " +
                         "SELECT r.Id As Nr, strftime('%Y-%m-%d %H:%M:%S', r.Time, 'localtime') AS Empfangen, c.Name AS Von, (SELECT Content FROM Message WHERE Id = r.ContentId) AS Inhalt " +
                         "FROM Recieved AS r " +
                         "JOIN Person AS c " +
                         "ON SenderId = c.Id; ";

                query += "CREATE VIEW View_Sent AS " +
                         "SELECT strftime('%Y-%m-%d %H:%M:%S', ls.Time, 'localtime') AS Gesendet, c.Name AS An, Content AS Inhalt, Reference AS Ref, ls.Via, Confirmation AS Sendestatus " +
                         "FROM Sent AS ls JOIN Person AS c ON ToId = c.Id JOIN Message AS mc ON mc.id = ls.ContentId; ";

                query += "CREATE VIEW View_Blocked AS SELECT Message.Id AS Id, Content As Nachricht, BlockDays As Gesperrt, BlockStart || ' Uhr' As Beginn, BlockEnd || ' Uhr' As Ende FROM Message WHERE BlockDays > 0; ";

                //query += "CREATE VIEW View_Shift AS " +
                //         "SELECT Shift.Id AS Nr, Person.Id AS PersonId, Person.Name AS Name, Via, CASE(CAST(strftime('%w', Start) AS INT) + 6) % 7 WHEN 0 THEN 'Mo' WHEN 1 THEN 'Di' WHEN 2 THEN 'Mi' WHEN 3 THEN 'Do' WHEN 4 THEN 'Fr' WHEN 5 THEN 'Sa' ELSE 'So' END AS Tag, date(Start) AS Datum " +
                //         "FROM Shift JOIN Person ON PersonId = Person.Id WHERE Start >= date('now', '-1 day') " +
                //         "UNION " +
                //         "SELECT NULL AS Nr, NULL AS PersonId, NULL AS Name, 0 AS Via, CASE(CAST(strftime('%w', d) AS INT) + 6) % 7 WHEN 0 THEN 'Mo' WHEN 1 THEN 'Di' WHEN 2 THEN 'Mi' WHEN 3 THEN 'Do' WHEN 4 THEN 'Fr' WHEN 5 THEN 'Sa' ELSE 'So' END AS Tag, d AS Datum " +
                //         "FROM ViewYearFromToday WHERE d >= date('now', '-1 day') " +
                //         "ORDER BY Datum; ";

                query += "CREATE VIEW View_Overdue AS  " +
                         "SELECT Recieved.ID AS Id, Person.Name, Person.Company AS Firma, Person.MaxInactive || ' Std.' AS Max_Inaktiv, " +
                         "MAX(datetime(Recieved.Time, 'localtime')) AS Letzte_Nachricht, " +
                         "Message.Content AS Inhalt, " +
                         "CAST((strftime('%s', 'now') - strftime('%s', Recieved.Time, '+' || MaxInactive || ' hours')) / 3600 AS INTEGER) || ' Std.' AS Fällig_seit " +
                         "FROM Recieved JOIN Person ON Person.Id = Recieved.SenderId " +
                         "JOIN Message ON Message.Id = ContentId " +
                         "WHERE MaxInactive > 0 " +
                         "GROUP BY Recieved.SenderId " +
                         "HAVING CAST((strftime('%s', 'now') - strftime('%s', Recieved.Time, '+' || MaxInactive || ' hours')) / 3600 AS INTEGER) > 0; ";

                query += "CREATE VIEW View_WatchedSenders AS SELECT Id, Name, Company AS Firma, MaxInactive || ' Std.' AS Max_Inaktiv FROM Person WHERE MaxInactive > 0 ORDER BY Firma; ";

                query += "CREATE VIEW View_Calendar AS " +
                         "SELECT s.ID, p.ID AS PersonId, p.name, p.Via, s.Start, s.End, strftime('%W', s.Start) AS KW, " +
                         "CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-4 days') BETWEEN s.Start AND s.End THEN DATE(s.Start, 'localtime', 'weekday 6', '-5 days')||'x' END AS Mo, " +
                         "CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-3 days') BETWEEN s.Start AND s.End THEN DATE(s.Start, 'localtime', 'weekday 6', '-4 days')||'x'  END AS Di, " +
                         "CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-2 days') BETWEEN s.Start AND s.End THEN DATE(s.Start, 'localtime', 'weekday 6', '-3 days')||'x'  END AS Mi, " +
                         "CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-1 days') BETWEEN s.Start AND s.End THEN DATE(s.Start, 'localtime', 'weekday 6', '-2 days')||'x'  END AS Do, " +
                         "CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-0 days') BETWEEN s.Start AND s.End THEN DATE(s.Start, 'localtime', 'weekday 6', '-1 days')||'x'  END AS Fr, " +
                         "CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '+1 days') BETWEEN s.Start AND s.End THEN DATE(s.Start, 'localtime', 'weekday 6', '+0 days')||'x'  END AS Sa, " +
                         "CASE WHEN strftime('%w', s.Start) = '0' THEN " +
                         "  CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-5 days') BETWEEN s.Start AND s.End THEN DATE(s.Start, 'localtime', 'weekday 6', '-6 days')||'x'  END " +
                         "ELSE " +
                         "  CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '+2 days') BETWEEN s.Start AND s.End THEN DATE(s.Start, 'localtime', 'weekday 6', '+1 days')||'x'  END " +
                         "END  AS So, " +
                         "CASE WHEN s.End > DATE(s.Start, 'weekday 6', '+3 days') THEN '...' END AS mehr " +
                         "FROM Shift AS s JOIN Person p ON s.PersonId = p.ID " +
                         "WHERE s.End > date('now', '-1 day') " +
                         "ORDER BY Start; ";

                query += "CREATE VIEW View_Calendar_Full AS " + 
                        "SELECT * FROM View_Calendar " +
                        "UNION " +
                        "SELECT NULL AS ID, NULL AS PersonId, NULL AS Name, NULL AS Via, DATE(d, 'weekday 1') AS Start, NULL AS End, " +
                        "strftime('%W', d) AS KW, date(d, 'weekday 1') AS Mo, date(d, 'weekday 2') AS Di, date(d, 'weekday 3') AS Mi, " +
                        "date(d, 'weekday 4') AS Do, date(d, 'weekday 5') AS Fr, date(d, 'weekday 6') AS Sa, date(d, 'weekday 0') AS So, " +
                        "NULL AS mehr FROM(WITH RECURSIVE dates(d) AS(VALUES(date('now')) UNION ALL " +
                        "SELECT date(d, '+4 day', 'weekday 1') FROM dates WHERE d < date('now', '+1 year')) SELECT d FROM dates) " +
                        "WHERE KW NOT IN(SELECT KW FROM View_Calendar WHERE date(Start) >= date('now', '-7 day', 'weekday 1') ) " +
                        "ORDER BY Start; ";

                NonQuery(query, null);
            }
            catch (Exception)
            {
                throw;
            }

            try
            {
                string query = "INSERT INTO Log (Prio, Content) VALUES (3, 'Datenbank neu erstellt.'); ";

                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.Undefined + ", 'nicht definiert'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.Sms + ", 'SMS'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.Email + ", 'Email'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.SmsAndEmail + ", 'SMS + Email'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.PermanentEmail + ", 'immer Email'); ";

                query += $"INSERT INTO Person (Name, Password, Level, Company, Phone, Email, Via) VALUES ('SMSZentrale', '{Encrypt("7307")}', 9999, 'Kreutzträger Kältetechnik, Bremen', '+4916095285xxx', 'harm.schnakenberg@kreutztraeger.de', 4); ";
                query += $"INSERT INTO Person (Name, Password, Level, Company, Phone, Email, Via) VALUES ('Bereitschaftshandy', '{Encrypt("7307")}', 2000, 'Kreutzträger Kältetechnik, Bremen', '+4916095285xxx', 'harm.schnakenberg@kreutztraeger.de', 2); ";

                query += "INSERT INTO Message (Content, BlockDays, BlockStart, BlockEnd) VALUES ('Datenbank neu erstellt.', 127, 8, 8); ";

                query += "INSERT INTO Recieved (SenderId, ContentId) VALUES (1, 1); ";

                query += "INSERT INTO Sent (ToId, Via, ContentId) VALUES (1, 0, 1); ";

                query += "INSERT INTO Shift (PersonId, Start, End) VALUES (1, DATETIME('now','-3 days', 'weekday 1'), DATETIME('now', '+2 hours')); ";

                NonQuery(query, null);

            }
            catch (Exception)
            {
                throw;
            }

        }


    }
}
