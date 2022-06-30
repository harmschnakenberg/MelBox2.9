using MelBoxGsm;
using System;

namespace MelBox2
{
    partial class Program
    {
        /// <summary>
        /// Weiterleitung von Sprachanrufen an diese Nummer erzwingen 
        /// </summary>
        public static string OverideCallForwardingNumber { get; set; } = string.Empty;

        /// <summary>
        /// Liest Initialisierungswerte aus der Datenbank
        /// </summary>
        private static void GetIniValues()
        {
            if (!System.IO.File.Exists(Sql.DbPath))
            {
                Log.Warning($"Ini-Datei kann nicht gelesen werden. Datei '{Sql.DbPath}' nicht gefunden.Es wird versucht eine neue Datenbank anzulegen.", 3333);
                Sql.CheckDbFile();
                
            }

            try
            {
                Sql.DbPath = GetIniValue(nameof(Sql.DbPath), Sql.DbPath); // Erst die richtige Datenbank laden!
                Console.WriteLine("Initialisiere Konfiguration aus Datenbankdatei: " + Sql.DbPath);

                ReliableSerialPort.Debug = (ReliableSerialPort.GsmDebug)GetIniValue(nameof(ReliableSerialPort.Debug), (int)ReliableSerialPort.Debug);

                Sql.Level_Admin = GetIniValue(nameof(Sql.Level_Admin), Sql.Level_Admin);
                Sql.Level_Reciever = GetIniValue(nameof(Sql.Level_Reciever), Sql.Level_Reciever);

                Gsm.MaxSendTrysPerSms = GetIniValue(nameof(Gsm.MaxSendTrysPerSms), Gsm.MaxSendTrysPerSms);
                Gsm.RingSecondsBeforeCallForwarding = GetIniValue(nameof(Gsm.RingSecondsBeforeCallForwarding), Gsm.RingSecondsBeforeCallForwarding);
                Gsm.TrackingTimeoutMinutes = GetIniValue(nameof(Gsm.TrackingTimeoutMinutes), Gsm.TrackingTimeoutMinutes);

                Program.OverideCallForwardingNumber = GetIniValue(nameof(Gsm.CallForwardingNumber), Gsm.CallForwardingNumber);
                Gsm.SetCallForewarding(Sql.GetCurrentCallForwardingNumber(OverideCallForwardingNumber));
                Gsm.AdminPhone = GetIniValue(nameof(Gsm.AdminPhone), Gsm.AdminPhone);
                Gsm.SerialPortName = GetIniValue(nameof(Gsm.SerialPortName), Gsm.SerialPortName);
                Gsm.SimPin = GetIniValue(nameof(Gsm.SimPin), Gsm.SimPin);
                Gsm.GsmCharacterSet = GetIniValue(nameof(Gsm.GsmCharacterSet), Gsm.GsmCharacterSet);

                Email.SmtpPort = GetIniValue(nameof(Email.SmtpPort), Email.SmtpPort);
                Email.Admin = GetIniValue(nameof(Email.Admin), Email.Admin);
                Email.From = GetIniValue(nameof(Email.From), Email.From);
                Email.SmtpHost = GetIniValue(nameof(Email.SmtpHost), Email.SmtpHost);
                Email.SmtpUser = GetIniValue(nameof(Email.SmtpUser), Email.SmtpUser);
                Email.SmtpPassword = GetIniValue(nameof(Email.SmtpPassword), Email.SmtpPassword);
                Email.SmtpEnableSSL = GetIniValue(nameof(Email.SmtpEnableSSL), Email.SmtpEnableSSL);

                EmailListener.ImapServer = GetIniValue(nameof(EmailListener.ImapServer), EmailListener.ImapServer);
                EmailListener.ImapPort = GetIniValue(nameof(EmailListener.ImapPort), EmailListener.ImapPort);
                EmailListener.ImapUserName = GetIniValue(nameof(EmailListener.ImapUserName), EmailListener.ImapUserName);
                EmailListener.ImapPassword = GetIniValue(nameof(EmailListener.ImapPassword), EmailListener.ImapPassword);
                EmailListener.ImapEnableSSL = GetIniValue(nameof(EmailListener.ImapEnableSSL), EmailListener.ImapEnableSSL);

                Program.LifeMessageTrigger = GetIniValue(nameof(Program.LifeMessageTrigger), string.Join(",", Program.LifeMessageTrigger)).Split(',');
                Program.SmsTestTrigger = GetIniValue(nameof(Program.SmsTestTrigger), Program.SmsTestTrigger);
                Program.HourOfDailyTasks = GetIniValue(nameof(Program.HourOfDailyTasks), Program.HourOfDailyTasks);

                Sql.StartOfBusinessDay = GetIniValue(nameof(Sql.StartOfBusinessDay), Sql.StartOfBusinessDay);
                Sql.EndOfBusinessFriday = GetIniValue(nameof(Sql.EndOfBusinessFriday), Sql.EndOfBusinessFriday);
                Sql.EndOfBusinessDay = GetIniValue(nameof(Sql.EndOfBusinessDay), Sql.EndOfBusinessDay);

                Scheduler.TaskName = GetIniValue(nameof(Scheduler.TaskName), Scheduler.TaskName);

                Console.WriteLine("Fehler und Debug-Meldungen gehen an: " + Gsm.AdminPhone + " " + Email.Admin.Address);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
            {
                Log.Warning("Mindestens ein Initialwerte konnte nicht aus der Datenbank gelesen werden. Es werden Standardwerte genommen.", 31635);
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }


        private static int GetIniValue(string propertyName, int standardValue)
        {
            object result = Sql.SelectIniProperty(propertyName);

            if (result == null && Sql.InsertIniProperty(propertyName, standardValue.ToString()))
            {
                return GetIniValue(propertyName, standardValue);
            }

            return int.Parse((result ?? 0).ToString());
        }

        private static string GetIniValue(string propertyName, string standardValue)
        {

            object result = Sql.SelectIniProperty(propertyName);

            if (result == null && Sql.InsertIniProperty(propertyName, standardValue))
            {
                return GetIniValue(propertyName, standardValue).ToString();
            }

            return (result ?? string.Empty).ToString();
        }

        private static System.Net.Mail.MailAddress GetIniValue(string propertyName, System.Net.Mail.MailAddress mail)
        {
            object result = Sql.SelectIniProperty(propertyName);

            if (result == null && Sql.InsertIniProperty(propertyName, $"<{mail.Address}> {mail.DisplayName}"))
            {
                return GetIniValue(propertyName, mail);
            }

            string[] rawEmail = (result ?? string.Empty).ToString().Split('>');

            if (rawEmail.Length == 2)
                mail = new System.Net.Mail.MailAddress(rawEmail[0].TrimStart('<'), rawEmail[1].Trim());
            else if (rawEmail.Length == 1)
                mail = new System.Net.Mail.MailAddress(rawEmail[0].TrimStart('<'), rawEmail[0].Trim());

            return mail;
        }

        private static bool GetIniValue(string propertyName, bool standardValue)
        {
            object result = Sql.SelectIniProperty(propertyName);

            if (result == null && Sql.InsertIniProperty(propertyName, standardValue.ToString()))
            {
                return GetIniValue(propertyName, standardValue);
            }

            return bool.Parse(result.ToString());
        }
    }
}
