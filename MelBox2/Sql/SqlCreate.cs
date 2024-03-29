﻿using System;
using System.IO;

namespace MelBox2
{
    partial class Sql
    {
        private static readonly string AppFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public static string DbPath { get; set; } = Path.Combine(AppFolder , "DB", "MelBox2.db");

        private static void CreateNewDataBase()
        {
            Log.Info($"Erstelle eine neue Datenbank-Datei unter '{DbPath}'", 21405);

            try
            {
                //Erstelle Datenbank-Datei und öffne einmal zum Testen
                _ = Directory.CreateDirectory(Path.GetDirectoryName(DbPath));
                FileStream stream = File.Create(DbPath);
                stream.Close();

                System.Threading.Thread.Sleep(500);

                string query = "CREATE TABLE IF NOT EXISTS Log ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "Prio INTEGER NOT NULL, " +
                    "Content TEXT " +
                    "); ";

                NonQuery(query, null);

                query = "CREATE TABLE IF NOT EXISTS SendWay ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY, " +
                    "Way TEXT " +
                    "); ";
                NonQuery(query, null);

                query = "CREATE TABLE IF NOT EXISTS Person ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Name TEXT NOT NULL UNIQUE, " +
                    "Password TEXT, " +
                    "Level INTEGER DEFAULT 0, " +
                    "Company TEXT, " +
                    "Phone TEXT, " +
                    "Email TEXT, " +
                    "Via INTEGER, " +
                    "KeyWord TEXT, " +
                    "MaxInactive INTEGER DEFAULT 0, " +

                    "CONSTRAINT fk_Via FOREIGN KEY (Via) REFERENCES SendWay (ID) ON DELETE SET NULL " + // Einschränkung bei Bitweise Nutzung von Via sinnvoll?
                    "); ";
                NonQuery(query, null);

                query = "CREATE TABLE IF NOT EXISTS Message ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "Content TEXT NOT NULL UNIQUE, " +
                    "BlockDays INTEGER, " +
                    "BlockStart INTEGER, " +
                    "BlockEnd INTEGER " +
                    "); ";
                NonQuery(query, null);

                query = "CREATE TABLE IF NOT EXISTS Recieved ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "SenderId INTEGER, " +
                    "ContentId INTEGER, " +

                    "CONSTRAINT fk_SenderId FOREIGN KEY (SenderId) REFERENCES Person (ID) ON DELETE SET DEFAULT, " +
                    "CONSTRAINT fk_ContentId FOREIGN KEY (ContentId) REFERENCES Message (ID) ON DELETE SET DEFAULT " +
                    "); ";
                NonQuery(query, null);

                query = "CREATE TABLE IF NOT EXISTS Sent ( " +
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
                NonQuery(query, null);

                //Report-Tabelle nur zum testen. Später rausnehmen?
                query = "CREATE TABLE Report ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "Reference INTEGER, " +
                    "DeliveryCode INTEGER " +
                    "); ";
                NonQuery(query, null);

                //GSM-Signal-Tabelle nur zum testen. Später rausnehmen?
                query = "CREATE TABLE GsmSignal ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "SignalQuality INTEGER " +
                    "); ";
                NonQuery(query, null);

                query = "CREATE TABLE IF NOT EXISTS Shift ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY, " +
                    "PersonId INTEGER, " +
                    "Start TEXT NOT NULL, " +
                    "End TEXT NOT NULL, " +
                    "CONSTRAINT fk_PersonId FOREIGN KEY (PersonId) REFERENCES Person (ID) ON DELETE SET DEFAULT " +
                    "); ";
                NonQuery(query, null);

                query = "CREATE TABLE IF NOT EXISTS Ini ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY, " +
                    "Property TEXT NOT NULL UNIQUE, " +
                    "Value TEXT NOT NULL " +
                    "); ";
                NonQuery(query, null);

                query = "CREATE TABLE IF NOT EXISTS Notepad ( " +
                    "ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                    "Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, " +
                    "AuthorId INTEGER, " +
                    "CustomerId INTEGER, " +
                    "Content TEXT NOT NULL, " +
                    "CONSTRAINT fk_AuthorId FOREIGN KEY(AuthorId) REFERENCES Person(ID) ON DELETE SET DEFAULT, " +
                    "CONSTRAINT fk_CustomerId FOREIGN KEY(CustomerId) REFERENCES Person(ID) ON DELETE SET DEFAULT " +
                    "); ";
                NonQuery(query, null);

                query = "CREATE VIEW ViewYearFromToday AS " +
                    "SELECT CASE(CAST(strftime('%w', d) AS INT) +6) % 7 WHEN 0 THEN 'Mo' WHEN 1 THEN 'Di' WHEN 2 THEN 'Mi' WHEN 3 THEN 'Do' WHEN 4 THEN 'Fr' WHEN 5 THEN 'Sa' ELSE 'So' END AS Tag, d FROM(WITH RECURSIVE dates(d) AS(VALUES(date('now')) " +
                    "UNION ALL " +
                    "SELECT date(d, '+1 day') FROM dates WHERE d<date('now', '+1 year')) SELECT d FROM dates ) WHERE d NOT IN(SELECT date(Start) FROM Shift WHERE date(Start) >= date('now') " +
                    "); ";
                NonQuery(query, null);

                query = "CREATE VIEW View_Recieved AS " +
                         "SELECT r.Id As Nr, strftime('%Y-%m-%d %H:%M:%S', r.Time, 'localtime') AS Empfangen, c.Name AS Von, (SELECT Content FROM Message WHERE Id = r.ContentId) AS Inhalt " +
                         "FROM Recieved AS r " +
                         "JOIN Person AS c " +
                         "ON SenderId = c.Id; ";
                NonQuery(query, null);

                query = "CREATE VIEW View_Sent AS " +
                         "SELECT strftime('%Y-%m-%d %H:%M:%S', ls.Time, 'localtime') AS Gesendet, c.Name AS An, Content AS Inhalt, Reference AS Ref, ls.Via, Confirmation AS Sendestatus " +
                         "FROM Sent AS ls JOIN Person AS c ON ToId = c.Id JOIN Message AS mc ON mc.id = ls.ContentId; ";
                NonQuery(query, null);

                query = "CREATE VIEW View_Blocked AS SELECT Message.Id AS Id, Content As Nachricht, BlockDays As Gesperrt, BlockStart || ' Uhr' As Beginn, BlockEnd || ' Uhr' As Ende FROM Message WHERE BlockDays > 0; ";
                NonQuery(query, null);

                query = "CREATE VIEW View_Overdue AS  " +
                         "SELECT Person.ID AS Id, Person.Name, Person.Company AS Firma, Person.MaxInactive || ' Std.' AS Max_Inaktiv, " +
                         "MAX(datetime(Recieved.Time, 'localtime')) AS Letzte_Nachricht, " +
                         "Message.Content AS Inhalt, " +
                         "CAST((strftime('%s', 'now') - strftime('%s', Recieved.Time, '+' || MaxInactive || ' hours')) / 3600 AS INTEGER) || ' Std.' AS Fällig_seit " +
                         "FROM Recieved JOIN Person ON Person.Id = Recieved.SenderId " +
                         "JOIN Message ON Message.Id = ContentId " +
                         "WHERE MaxInactive > 0 " +
                         "GROUP BY Recieved.SenderId " +
                         "HAVING CAST((strftime('%s', 'now') - strftime('%s', Recieved.Time, '+' || MaxInactive || ' hours')) / 3600 AS INTEGER) > 0; ";
                NonQuery(query, null);

                query = "CREATE VIEW View_WatchedSenders AS SELECT Id, Name, Company AS Firma, MaxInactive || ' Std.' AS Max_Inaktiv FROM Person WHERE MaxInactive > 0 ORDER BY Firma; ";
                NonQuery(query, null);
                
                query = "CREATE VIEW View_Calendar AS " +
                         "SELECT s.ID, p.ID AS PersonId, p.name, p.Via, s.Start, s.End, strftime('%W', s.Start) AS KW, " +
                         "DATE(s.Start, 'localtime', 'weekday 6', '-5 days') || CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-5 days') BETWEEN DATE(s.Start) AND DATE(s.End) THEN 'x' ELSE '' END AS Mo, " +
                         "DATE(s.Start, 'localtime', 'weekday 6', '-4 days') || CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-4 days') BETWEEN DATE(s.Start) AND DATE(s.End) THEN 'x' ELSE '' END AS Di, " +
                         "DATE(s.Start, 'localtime', 'weekday 6', '-3 days') || CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-3 days') BETWEEN DATE(s.Start) AND DATE(s.End) THEN 'x' ELSE '' END AS Mi, " +
                         "DATE(s.Start, 'localtime', 'weekday 6', '-2 days') || CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-2 days') BETWEEN DATE(s.Start) AND DATE(s.End) THEN 'x' ELSE '' END AS Do, " +
                         "DATE(s.Start, 'localtime', 'weekday 6', '-1 days') || CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '-1 days') BETWEEN DATE(s.Start) AND DATE(s.End) THEN 'x' ELSE '' END AS Fr, " +
                         "DATE(s.Start, 'localtime', 'weekday 6', '-0 days') || CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '+0 days') BETWEEN DATE(s.Start) AND DATE(s.End) THEN 'x' ELSE '' END AS Sa, " +
                         "DATE(s.Start, 'localtime', 'weekday 6', '+1 days') || CASE WHEN DATE(s.Start, 'localtime', 'weekday 6', '+1 days') BETWEEN DATE(s.Start) AND DATE(s.End) THEN 'x' ELSE '' END AS So " +
                         "FROM Shift AS s JOIN Person p ON s.PersonId = p.ID WHERE s.End > date('now', '-1 day') ORDER BY Start; ";
                NonQuery(query, null);

                query = "CREATE VIEW View_Calendar_Full AS " +
                        "SELECT * FROM View_Calendar " +
                        "UNION " +
                        "SELECT NULL AS ID, NULL AS PersonId, NULL AS Name, NULL AS Via, DATETIME(d, 'weekday 1') AS Start, NULL AS End, " +
                        "strftime('%W', d) AS KW, " +
                        "date(d, 'weekday 1') || CASE WHEN date(d, 'weekday 1') IN (SELECT DATE(Shift.End) FROM Shift WHERE strftime('%w', Shift.End) = '1') THEN 'x' ELSE '' END AS Mo, " + //Montage markieren, wenn sie das ENde einer Bereitschaft sind
                        "date(d, 'weekday 2') AS Di, date(d, 'weekday 3') AS Mi, " +
                        "date(d, 'weekday 4') AS Do, date(d, 'weekday 5') AS Fr, date(d, 'weekday 6') AS Sa, date(d, 'weekday 0') AS So " +
                        "FROM(WITH RECURSIVE dates(d) AS(VALUES(date('now')) UNION ALL " +
                        "SELECT date(d, '+4 day', 'weekday 1') FROM dates WHERE d < date('now', '+1 year')) SELECT d FROM dates) " +
                        "WHERE KW NOT IN(SELECT KW FROM View_Calendar WHERE date(Start) >= date('now', '-7 day', 'weekday 1') ) " +
                        "ORDER BY Start; ";
                NonQuery(query, null);

                query = "CREATE VIEW ViewNotepad AS " +
                        "SELECT n.ID, n.Time AS Bearbeitet, n.AuthorId AS VonId, p1.name AS Von, n.CustomerId AS KundeId, p2.name As Kunde, n.Content AS Notiz " +
                        "FROM Notepad AS n " +
                        "JOIN Person AS p1 ON p1.ID = n.AuthorId " +
                        "JOIN Person AS p2 ON p2.ID = n.CustomerId ";

                NonQuery(query, null);
            }
            catch (Exception ex)
            {
                Log.Error("CreateNewDataBase() " + ex.Message + ex.StackTrace, 57651);
                throw ex;
            }

            try
            {
                string query = "INSERT INTO Log (Prio, Content) VALUES (3, 'Datenbank neu erstellt.'); ";

                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.Undefined + ", 'nicht definiert'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.Sms + ", 'SMS'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.Email + ", 'Email'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.SmsAndEmail + ", 'SMS + Email'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.PermanentEmail + ", 'immer Email'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.PermanentEmailAndSms + ", 'SMS + immer Email'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.EmailWhitelist + ", 'freigegebener E-Mail-Absender (Whitelist)'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + (int)Via.NoCalls + ", 'keine Sprachanrufe an diese Nummer'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + ((int)Via.NoCalls + (int)Via.Sms) + ", 'keine Sprachanrufe an diese Nummer + SMS'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + ((int)Via.NoCalls + (int)Via.Email) + ", 'keine Sprachanrufe an diese Nummer + Email'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + ((int)Via.NoCalls + (int)Via.SmsAndEmail) + ", 'keine Sprachanrufe an diese Nummer + SMS+EMail'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + ((int)Via.NoCalls + (int)Via.PermanentEmail) + ", 'keine Sprachanrufe an diese Nummer + immerEmail'); ";
                query += "INSERT INTO SendWay (ID, Way) VALUES (" + ((int)Via.NoCalls + (int)Via.PermanentEmailAndSms) + ", 'keine Sprachanrufe an diese Nummer + SMS+immerEmail'); ";


                query += $"INSERT INTO Person (Name, Password, Level, Company, Phone, Email, Via) VALUES ('SMSZentrale', '{Encrypt("7307")}', 9999, 'Kreutzträger Kältetechnik, Bremen', '+4916095285xxx', 'harm.schnakenberg@kreutztraeger.de', 4); ";
                query += $"INSERT INTO Person (Name, Password, Level, Company, Phone, Email, Via) VALUES ('Bereitschaftshandy', '{Encrypt("7307")}', 2000, 'Kreutzträger Kältetechnik, Bremen', '+491728362586', 'bereitschaftshandy@kreutztraeger.de', 2); ";

                query += "INSERT INTO Message (Content, BlockDays, BlockStart, BlockEnd) VALUES ('Datenbank neu erstellt.', 127, 8, 8); ";

                query += "INSERT INTO Recieved (SenderId, ContentId) VALUES (1, 1); ";

                query += "INSERT INTO Sent (ToId, Via, ContentId) VALUES (1, 0, 1); ";

                query += "INSERT INTO Shift (PersonId, Start, End) VALUES (1, DATETIME('now','-3 days','weekday 1'), DATETIME('now','+1 day')); ";

                NonQuery(query, null);

            }
            catch (Exception ex)
            {
                throw ex;
            }

        }


    }
}
