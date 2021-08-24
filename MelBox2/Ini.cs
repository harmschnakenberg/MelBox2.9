using MelBoxGsm;
using System;

namespace MelBox2
{
    partial class Program
    {
        private static void GetIniValues()
        {
            Console.WriteLine("Initialisiere Konfiguration aus Datenbank.");

            //try
            //{
                Program.HourOfDailyTasks = GetIniValue(nameof(Program.HourOfDailyTasks), Program.HourOfDailyTasks);
                Sql.Level_Admin = GetIniValue(nameof(Sql.Level_Admin), Sql.Level_Admin);
                Sql.Level_Reciever = GetIniValue(nameof(Sql.Level_Reciever), Sql.Level_Reciever);
                Email.SmtpPort = GetIniValue(nameof(Email.SmtpPort), Email.SmtpPort);
                Gsm.MaxSendTrysPerSms = GetIniValue(nameof(Gsm.MaxSendTrysPerSms), Gsm.MaxSendTrysPerSms);
                Gsm.RingTimeToCallForwarding = GetIniValue(nameof(Gsm.RingTimeToCallForwarding), Gsm.RingTimeToCallForwarding);
                Gsm.TrackingTimeoutMinutes = GetIniValue(nameof(Gsm.TrackingTimeoutMinutes), Gsm.TrackingTimeoutMinutes);

                Email.Admin = GetIniValue(nameof(Email.Admin), Email.Admin);
                Email.From = GetIniValue(nameof(Email.From), Email.From);

                if (bool.TryParse((Sql.SelectIniProperty(nameof(Email.SmtpEnableSSL))?? true).ToString(), out bool b))
                    Email.SmtpEnableSSL = b;

                Program.SmsTestTrigger = GetIniValue(nameof(Program.SmsTestTrigger), Program.SmsTestTrigger);

                Email.SmtpHost = GetIniValue(nameof(Email.SmtpHost), Email.SmtpHost); ;
                Email.SmtpUser = GetIniValue(nameof(Email.SmtpUser), Email.SmtpUser); ;
                Email.SmtpPassword = GetIniValue(nameof(Email.SmtpPassword), Email.SmtpPassword); ;

                Gsm.AdminPhone = GetIniValue(nameof(Gsm.AdminPhone), Gsm.AdminPhone); ;
                Gsm.CallForwardingNumber = GetIniValue(nameof(Gsm.CallForwardingNumber), Gsm.CallForwardingNumber); ;
                Gsm.AdminPhone = GetIniValue(nameof(Gsm.AdminPhone), Gsm.AdminPhone); ;
                Gsm.SerialPortName = GetIniValue(nameof(Gsm.SerialPortName), Gsm.SerialPortName); ;

                Program.LifeMessageTrigger = GetIniValue(nameof(Program.LifeMessageTrigger), string.Join(",", Program.LifeMessageTrigger)).Split(',');
//            }
//#pragma warning disable CA1031 // Do not catch general exception types
//            catch
//            {
//                Log.Warning("Mindestens ein Initialwerte konnte nicht aus der Datenbank gelesen werden. Es werden Standardwerte genommen.", 31635);
//            }
//#pragma warning restore CA1031 // Do not catch general exception types
        }


        private static int GetIniValue(string propertyName, int standardValue)
        {
            object result = Sql.SelectIniProperty(propertyName);

            if (result == null && Sql.InsertIniProperty(propertyName, standardValue.ToString()))
            {
                return GetIniValue(propertyName, standardValue);
            }

            return int.Parse((result?? 0).ToString());                
        }

        private static string GetIniValue(string propertyName, string standardValue)
        {
            object result = Sql.SelectIniProperty(propertyName);

            if (result == null && Sql.InsertIniProperty(propertyName, standardValue))
            {
                return GetIniValue(propertyName, standardValue).ToString();
            }

            return (result?? string.Empty).ToString();
        }

        private static System.Net.Mail.MailAddress GetIniValue(string propertyName, System.Net.Mail.MailAddress mail)
        {
            object result = Sql.SelectIniProperty(propertyName);

            if (result == null && Sql.InsertIniProperty(propertyName, $"<{mail.Address}> {mail.DisplayName}"))
            {
                return GetIniValue(propertyName, mail);
            }

            string[] rawEmail = (result?? string.Empty).ToString().Split('>');
            
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
