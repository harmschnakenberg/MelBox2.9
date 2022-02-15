using System;
using System.Net.Mail;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using S22;
using S22.Imap;

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

    }


    class EmailReciever
    {
      

        //Email empfangen
        //Quelle: https://github.com/smiley22/S22.Imap
        //Beispiele: https://github.com/smiley22/S22.Imap/blob/master/Examples.md#1

        public static void Recieve()
        {
            // Connect on port 993 using SSL.
            //            using (ImapClient Client = new ImapClient("imap.gmail.com", 993, true))
            using (ImapClient Client = new ImapClient("imap.gmx.net", 993,
                "harmschnakenberg@gmx.de", "Oyterdamm64!", AuthMethod.Login, true))
            {
                Console.WriteLine("We are connected!");

                //##########
                //Download unseen mail messages
                IEnumerable<uint> uids1 = Client.Search(SearchCondition.Unseen());
                IEnumerable<MailMessage> messages1 = Client.GetMessages(uids1);

                ////##########
                //// Find messages that were sent from abc@def.com and have
                //// the string "Hello World" in their subject line.
                //IEnumerable<uint> uids2 = Client.Search(
                //    SearchCondition.From("erichschnakenberg@web.de").And(
                //    SearchCondition.Subject("WG")));
                //IEnumerable<MailMessage> messages2 = Client.GetMessages(uids2);

                ////##########
                ////Download mail headers only instead of the entire mail message
                //// This returns *ALL* messages in the inbox.
                //IEnumerable<uint> uids3 = Client.Search(SearchCondition.All());


                //// If we're only interested in the subject line or envelope
                //// information, just downloading the mail headers is alot
                //// cheaper and alot faster.
                //IEnumerable<MailMessage> messages3 = Client.GetMessages(uids3, FetchOptions.HeadersOnly);

                foreach (MailMessage message in messages1)
                {
                    Console.WriteLine(message.From + "\t" + message.Subject);
                }

            }
        }



        //IDLE-Message (autom. Empfang)

        public static void SetIdleMessageNotification()
        {
            using (ImapClient Client = new ImapClient("imap.gmx.net", 993,
                "harmschnakenberg@gmx.de", "Oyterdamm64!", AuthMethod.Login, true))
            {
                // Should ensure IDLE is actually supported by the server
                if (Client.Supports("IDLE") == false)
                {
                    Console.WriteLine("Email-EMpfang: Server does not support IMAP IDLE");
                    return;
                }

                // We want to be informed when new messages arrive
                Client.NewMessage += new EventHandler<IdleMessageEventArgs>(OnNewMessage);

                // Put calling thread to sleep. This is just so the example program does
                // not immediately exit.
                System.Threading.Thread.Sleep(60000);
            }
        }

        static void OnNewMessage(object sender, IdleMessageEventArgs e)
        {
            Console.WriteLine("A new message arrived. Message has UID: " +
                e.MessageUID);

            // Fetch the new message's headers and print the subject line
            MailMessage m = e.Client.GetMessage(e.MessageUID, FetchOptions.HeadersOnly);

            Console.WriteLine("New message's subject: " + m.Subject + m.From);
        }
    }

    class EmailListener : IDisposable
    {
        public EmailListener()
        {            
            Client = new ImapClient(ImapServer, ImapPort, ImapUserName, ImapPassword, AuthMethod.Auto, ImapEnableSSL);
        }

        public EmailListener(string imapServer, int imapPort, string imapUserName, string imapPassword, bool imapEnableSSL)
        {
            ImapServer = imapServer;
            ImapPort = imapPort;
            ImapUserName = imapUserName;
            ImapPassword = imapPassword;
            ImapEnableSSL = imapEnableSSL;

            Client = new ImapClient(imapServer, imapPort, imapUserName, imapPassword, AuthMethod.Login, imapEnableSSL);

            // We want to be informed when new messages arrive
            Client.NewMessage += new EventHandler<IdleMessageEventArgs>(OnNewMessage);
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

        /// <summary>
        /// Prüft, ob der Emailserver Benachrichtigungen bei neu empfangenen Emails unterstützt.
        /// </summary>
        /// <returns></returns>
        public bool IsIdleEmailSupported()
        {
            return Client.Supports("IDLE");
        }

        void OnNewMessage(object sender, IdleMessageEventArgs e)
        {
            //Console.WriteLine("A new message arrived. Message has UID: " +
            //    e.MessageUID);

            // Fetch the new message's headers and print the subject line
            MailMessage m = e.Client.GetMessage(e.MessageUID, FetchOptions.TextOnly);
#if DEBUG
            Console.WriteLine($"Neue Email [{e.MessageUID}] {(m.IsBodyHtml ? "<html>" : "<Text>")} von >{m.From}<:\r\n\t{m.Subject}");
#endif
            EmailInEvent?.Invoke(this, m);
        }

    }

}



