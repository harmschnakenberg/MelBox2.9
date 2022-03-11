using System;
using System.Net.Mail;
using System.Collections.Generic;
using S22.Imap;
using System.Text;
using System.Text.RegularExpressions;

namespace MelBox2
{
    static class Email
    {
        #region Properties Email senden
        public static MailAddress From = new MailAddress("SMSZentrale@Kreutztraeger.de", "SMS-Zentrale");

        public static MailAddress Admin = new MailAddress("harm.schnakenberg@kreutztraeger.de", "MelBox2 Admin");

        public static string SmtpHost { get; set; } = "kreutztraeger-de.mail.protection.outlook.com";
        public static int SmtpPort { get; set; } = 25; //587;
        public static bool SmtpEnableSSL { get; set; } = false;
        public static string SmtpUser { get; set; } = "";
        public static string SmtpPassword { get; set; } = "";
        #endregion

        /// <summary>
        /// Sende Email an einen Empfänger.
        /// Sendungsverfolung wird nicht in der Datenbank protokolliert.
        /// </summary>
        /// <param name="to">Empfänger der Email</param>
        /// <param name="message">Inhalt der Email</param>
        /// <param name="subject">Betreff. Leer: Wird aus message generiert.</param>
        /// <param name="sendCC">Sende an Ständige Empänger in CC</param>
        public static void Send(MailAddress to, string message, string subject = "", bool cc = false)
        {
            var toList = new MailAddressCollection { to };
            int emailId = new Random().Next(256, int.MaxValue);

            Send(toList, message, subject, cc, emailId);
        }

        /// <summary>
        /// Sende Email an eine Empängerliste.        
        /// </summary>
        /// <param name="toList">Empfängerliste</param>
        /// <param name="message">Inhalt der Email</param>
        /// <param name="subject">Betreff. Leer: Wird aus message generiert.</param>
        /// <param name="emailId">Id zur Protokollierung der Sendungsverfolgung in der Datenbank</param>
        /// <param name="sendCC">Sende an Ständige Empänger in CC</param>
        public static void Send(MailAddressCollection toList, string message, string subject, bool cc, int emailId)
        {
#if DEBUG
            Console.WriteLine("Sende Email: " + message);
#endif
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
                    foreach (var CC in Sql.GetCurrentEmailRecievers())
                    {
#if DEBUG           //nur zu mir
                        if (CC.Address.ToLower() != Admin.Address.ToLower())
                            Console.WriteLine("Send(): Emailadresse gesperrt: " + CC.Address);
                        else
#endif
                        mail.CC.Add(CC);
                    }
                }

                if (!mail.To.Contains(Admin) && !mail.CC.Contains(Admin)) //Email geht in jedem Fall an Admin
                    mail.Bcc.Add(Admin);

                #endregion

                #region Message                
                if (subject.Length == 0)
                    subject = message.Normalize();

                subject = subject.Normalize().Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

                if (subject.Length > 255) subject = subject.Substring(0, 255); //255 Zeichen max. Betreff bei Outlook 

                mail.Subject = subject;

                mail.Body = message.Normalize();
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
                        Sql.UpdateSent(emailId, MelBoxGsm.Gsm.DeliveryStatus.SendRetry); //Erneut senden

                        System.Threading.Thread.Sleep(5000);
                        using (var smtpClient = new SmtpClient())
                            smtpClient.Send(mail);
                    }
                    else
                    {
                        Sql.InsertLog(1, $"Fehler beim Senden der Email an >{ex.InnerExceptions[i].FailedRecipient}<: {ex.InnerExceptions[i].Message}");
                        Sql.UpdateSent(emailId, MelBoxGsm.Gsm.DeliveryStatus.ServiceDenied); //Abgebrochen                       
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
                Sql.InsertLog(1, "Unbekannter Fehler beim Versenden einer Email");
                Log.Error("Unbekannter Fehler beim Versenden einer Email", 61351);
#endif
            }
#pragma warning restore CA1031 // Do not catch general exception types

            mail.Dispose();
        }



        /// <summary>
        /// Quelle https://codesnippets.fesslersoft.de/how-to-change-the-encoding-of-a-string-using-c-and-vb-net/
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sourceEncoding"></param>
        /// <param name="targetEncoding"></param>
        /// <returns></returns>
        public static string ChangeEncoding(this string input, Encoding sourceEncoding, Encoding targetEncoding)
        {
            byte[] utfBytes = sourceEncoding.GetBytes(input);
            byte[] isoBytes = Encoding.Convert(sourceEncoding, targetEncoding, utfBytes);
            return targetEncoding.GetString(isoBytes);
        }


        public static bool IsValidEmail(this string email)
        {
            string pattern = @"^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|" + @"([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)" + @"@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            return regex.IsMatch(email);
        }

        /// <summary>
        /// Elreha Kühlstellenregeler geben eine html-Email aus, die hiermit auf die wesentliche Nachricht reduziert wird:
        /// </summary>
        /// <param name="body">Email-Body von Elrehe Kühlstellenregler</param>
        /// <returns>relavanten Teil der Email zwischen 'Fehler' und 'Regleradresse'</returns>
        public static string ParseElrehaEmail(string body)
        {
            string txt = Sql.RemoveHTMLTags(body).Replace(Environment.NewLine, " ").Replace('\t', ' ');
           
            string pattern = @"Anlage:(.*)Datum(?:.*)Fehler(.*)Regleradresse";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            Match m =  regex.Match(txt);
            return (m.Groups[1].Value + ", " + m.Groups[2].Value).Trim();
        }

    }

    /// <summary>
    /// siehe S22.Imap 
    /// Quelle: https://github.com/smiley22/S22.Imap copyright © 2012-2014 Torben Könke.
    /// Free to use in commercial and personal projects (MIT license)
    /// </summary>
    class EmailListener : IDisposable
    {
        public EmailListener()
        {            
            Client = new ImapClient(ImapServer, ImapPort, ImapUserName, ImapPassword, AuthMethod.Auto, ImapEnableSSL);

            // We want to be informed when new messages arrive
            Client.NewMessage += new EventHandler<IdleMessageEventArgs>(OnNewMessage);
        }

        public EmailListener(string imapServer, int imapPort, string imapUserName, string imapPassword, bool imapEnableSSL)
        {
            ImapServer = imapServer;
            ImapPort = imapPort;
            ImapUserName = imapUserName;
            ImapPassword = imapPassword;
            ImapEnableSSL = imapEnableSSL;

            Client = new ImapClient(imapServer, imapPort, imapUserName, imapPassword, AuthMethod.Login, imapEnableSSL);
            Client.IdleError += Client_IdleError;
#if DEBUG
            foreach (var item in Client.ListMailboxes())
            {
                Console.WriteLine(">" + item);
            }
#endif
            Client.DefaultMailbox = "Kreualarm"; // Nur zum testen
            Console.WriteLine("Default Mailbox ist >" + Client.DefaultMailbox + "<");

            // We want to be informed when new messages arrive
            Client.NewMessage += new EventHandler<IdleMessageEventArgs>(OnNewMessage);
        }

        private void Client_IdleError(object sender, IdleErrorEventArgs e)
        {
            string txt = "Es ist ein Verbindungsproblem mit dem SMTP-Server aufgetreten. Verbindung wird erneuert.";

            Console.WriteLine(txt);
            Log.Error(txt + e.Exception, 44765);
            Client.Logout();
            Client.Login(ImapUserName, ImapPassword, AuthMethod.Login);
        }

#region Fields
        private readonly ImapClient Client;

        /// <summary>
        /// Wird ausgelöst, wenn eine Email empfangen wurde.
        /// </summary>
        public event EventHandler<MailMessage> EmailInEvent;
#endregion

#region Properties

        public static string ImapServer { get; set; }

        public static int ImapPort { get; set; } = 993;

        public static string ImapUserName { get; set; }

        public static string ImapPassword { get; set; }

        public static bool ImapEnableSSL { get; set; } = true;

        public void Dispose()
        {
            Client.Dispose();            
        }

#endregion

        public void ReadUnseen()
        {           
            //Download unseen mail messages
            IEnumerable<uint> uids1 = Client.Search(SearchCondition.Unseen()); 
            IEnumerable<MailMessage> messages1 = Client.GetMessages(uids1);

            foreach (MailMessage m in messages1)
            {
                Console.WriteLine($"{m.Headers["Date"]}: Neue Email <{(m.IsBodyHtml ? "html" : "Text")}> <{m.BodyEncoding}> von {m.From.Address}<: {m.Body.Substring(0,Math.Min(m.Body.Length,160))}");
                EmailInEvent?.Invoke(this, m);
            }
        }


        /// <summary>
        /// Prüft, ob der Emailserver Benachrichtigungen bei neu empfangenen Emails unterstützt.
        /// </summary>
        /// <returns>true= Server kann benachrichtigen bei neu empfangenen Emails.</returns>
        public bool IsIdleEmailSupported()
        {
            return Client.Supports("IDLE");
        }

        /// <summary>
        /// Wird aufgerufen, wenn eine neue EMail empfangen wurde.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnNewMessage(object sender, IdleMessageEventArgs e)
        {
            //Console.WriteLine("A new message arrived. Message has UID: " +
            //    e.MessageUID);

            // Fetch the new message's headers and print the subject line
            MailMessage m = e.Client.GetMessage(e.MessageUID, FetchOptions.TextOnly);
#if DEBUG
            Console.WriteLine($"{DateTime.Now.ToShortTimeString()}: Neue Email [{e.MessageUID}] <{(m.IsBodyHtml ? "html" : "Text")}> <{m.BodyEncoding}> von {m.From.Address}<: {m.Body.Substring(0, Math.Min(m.Body.Length, 160))}");
#endif
            EmailInEvent?.Invoke(this, m);
        }

    }

}



