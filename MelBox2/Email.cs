using System;
using System.Net.Mail;

namespace MelBox2
{
    class Email
    {
        public static MailAddress From = new MailAddress("SMSZentrale@Kreutztraeger.de", "SMS-Zentrale");

        public static MailAddress Admin = new MailAddress("harm.Schnakenberg@Kreutztraeger.de", "MelBox2 Admin");

        public static string SmtpHost { get; set; } = "kreutztraeger-de.mail.protection.outlook.com";
        public static int SmtpPort { get; set; } = 25; //587;
        public static bool SmtpEnableSSL { get; set; } = false;
        public static string SmtpUser { get; set; } = "";
        public static string SmtpPassword { get; set; } = "";

        /// <summary>
        /// Sende Email an einen Empfänger.
        /// Sendungsverfolung wird nicht in der Datenbank protokolliert.
        /// </summary>
        /// <param name="to">Empfänger der Email</param>
        /// <param name="message">Inhalt der Email</param>
        /// <param name="subject">Betreff. Leer: Wird aus message generiert.</param>
        /// <param name="sendCC">Sende an Ständige Empänger in CC</param>
        public static void Send(MailAddress to, string message, string subject = "")
        {
            var toList = new MailAddressCollection { to };

            Send(toList, message, subject);
        }

        /// <summary>
        /// Sende Email an eine Empängerliste.        
        /// </summary>
        /// <param name="toList">Empfängerliste</param>
        /// <param name="message">Inhalt der Email</param>
        /// <param name="subject">Betreff. Leer: Wird aus message generiert.</param>
        /// <param name="emailId">Id zur Protokollierung der Sendungsverfolgung in der Datenbank</param>
        /// <param name="sendCC">Sende an Ständige Empänger in CC</param>
        public static void Send(MailAddressCollection toList, string message, string subject = "", bool cc = false)
        {
            Console.WriteLine("Sende Email: " + message);

            int emailId = new Random().Next(256, int.MaxValue);

            MailMessage mail = new MailMessage();
                  
            try
            {
                #region From
                mail.From = From;
                mail.Sender = From;
                #endregion

                #region To               
                foreach (var to in toList ?? new MailAddressCollection() { Admin })
                {
#if DEBUG           //nur zu mir
                    if (to.Address.ToLower() != Admin.Address.ToLower())
                        Console.WriteLine("Send(): Emailadresse gesperrt: " + to.Address);
                    else
#endif
                        mail.To.Add(to);
                }

                if (cc)
                {
                    foreach (var CC in Sql.GetCurrentShiftEmailAddresses(true))
                    {
#if DEBUG           //nur zu mir
                        if (CC.Address.ToLower() != Admin.Address.ToLower())
                            Console.WriteLine("Send(): Emailadresse gesperrt: " + CC.Address);
                        else
#endif
                            mail.CC.Add(CC);
                    }
                }

                #endregion

                #region Message
                if (subject.Length > 0)
                    mail.Subject = subject.Normalize().Replace('\r', ' ').Replace('\n', ' ');
                else
                {
                    mail.Subject = message.Normalize().Replace(System.Environment.NewLine, "");
                }

                mail.Body = message;
                #endregion

                #region Smtp
                //Siehe https://docs.microsoft.com/de-de/dotnet/api/system.net.mail.smtpclient.sendasync?view=net-5.0

                using (var smtpClient = new SmtpClient())
                {
                    smtpClient.Host = SmtpHost;
                    smtpClient.Port = SmtpPort;

                    if (SmtpUser.Length > 0 && SmtpPassword.Length > 0)
                        smtpClient.Credentials = new System.Net.NetworkCredential(SmtpUser, SmtpPassword);

                    //smtpClient.UseDefaultCredentials = true;

                    smtpClient.EnableSsl = SmtpEnableSSL;

                    smtpClient.Send(mail);

                    //smtpClient.SendCompleted += SmtpClient_SendCompleted;  
                    //smtpClient.SendAsync(mail, emailId); //emailId = Zufallszahl größer 255 (Sms-Ids können zwischen 0 bis 255 liegen)
                }
                #endregion
            }
            catch (SmtpFailedRecipientsException ex)
            {
                for (int i = 0; i < ex.InnerExceptions.Length; i++)
                {
                    SmtpStatusCode status = ex.InnerExceptions[i].StatusCode;
                    if (status == SmtpStatusCode.MailboxBusy ||
                        status == SmtpStatusCode.MailboxUnavailable)
                    {
                        Sql.InsertLog(2, $"Senden der Email fehlgeschlagen. Neuer Sendeversuch.\r\n" + message);
                        Sql.UpdateSent(emailId, 32); //Erneut senden

                        System.Threading.Thread.Sleep(5000);
                        using (var smtpClient = new SmtpClient())
                            smtpClient.Send(mail);
                    }
                    else
                    {
                        Sql.InsertLog(1, $"Fehler beim Senden der Email an >{ex.InnerExceptions[i].FailedRecipient}<: {ex.InnerExceptions[i].Message}");
                        Sql.UpdateSent(emailId, 64); //Abgebrochen                       
                    }

                }
            }
            catch (System.Net.Mail.SmtpException ex_smtp)
            {
                Sql.InsertLog(1, "Fehler beim Versenden einer Email: " + ex_smtp.Message);
                Log.Error(ex_smtp.Message, 61350);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
            {
#if DEBUG
                throw;
#else
                Program.InsertLog(1, "Unbekannter Fehler beim Versenden einer Email");
                Log.Error("Unbekannter Fehler beim Versenden einer Email", 61351);
#endif
            }
#pragma warning restore CA1031 // Do not catch general exception types

            Sql.UpdateSent(emailId, 1); //Erfolgreich gesendet
            mail.Dispose();
        }


    }

}
