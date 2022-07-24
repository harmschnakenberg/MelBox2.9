//using MailKit;
//using MailKit.Net.Imap;
//using MailKit.Security;
using S22.Imap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Exchange.WebServices.Data;

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
        public static void Send(MailAddress to, string message, string subject = "")
        {
            var toList = new MailAddressCollection { to };
            int emailId = new Random().Next(256, int.MaxValue);

            Send(toList, message, subject, emailId);
        }

        /// <summary>
        /// Sende Email an eine Empängerliste.        
        /// </summary>
        /// <param name="toList">Empfängerliste</param>
        /// <param name="message">Inhalt der Email</param>
        /// <param name="subject">Betreff. Leer: Wird aus message generiert.</param>
        /// <param name="emailId">Id zur Protokollierung der Sendungsverfolgung in der Datenbank</param>
        /// <param name="sendCC">Sende an Ständige Empänger in CC</param>
        public static void Send(MailAddressCollection toList, string message, string subject, int emailId)
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

//                if (cc)
//                {
//                    foreach (var CC in Sql.GetCurrentEmailRecievers())
//                    {
//#if DEBUG           //nur zu mir
//                        if (CC.Address.ToLower() != Admin.Address.ToLower())
//                            Console.WriteLine("Send(): Emailadresse gesperrt: " + CC.Address);
//                        else
//#endif
//                        mail.CC.Add(CC);
//                    }
//                }

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
            
            //TEST
            _ = ByteArrayToFile("C:\\MelBox2\\raw\\bytes" + DateTime.Now.Ticks.ToString() + ".byt", utfBytes);
            _ = ByteArrayToFile("C:\\MelBox2\\raw\\bytes" + DateTime.Now.Ticks.ToString() + ".utf8", targetEncoding.GetBytes(input));

            byte[] isoBytes = Encoding.Convert(sourceEncoding, targetEncoding, utfBytes);

            return targetEncoding.GetString(isoBytes);
        }

        /// <summary>
        /// Nur zum Testen/prüfen der Bytes
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="byteArray"></param>
        /// <returns></returns>
        private static bool ByteArrayToFile(string fileName, byte[] byteArray)
        {
            try
            {
                using (var fs = new System.IO.FileStream(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                {
                    fs.Write(byteArray, 0, byteArray.Length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught in process: {0}", ex);
                return false;
            }
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

            Match m = regex.Match(txt);
            return (m.Groups[1].Value + ", " + m.Groups[2].Value).Replace("()", string.Empty).Trim();
        }

    }

    #region Emails lesen mit S22.Imap

    /// <summary>
    /// siehe S22.Imap 
    /// Quelle: https://github.com/smiley22/S22.Imap copyright © 2012-2014 Torben Könke.
    /// Free to use in commercial and personal projects (MIT license)
    /// </summary>
    class EmailListener : IDisposable
    {
        #region Constructor
        public EmailListener()
        {
            try
            {
                Client = new ImapClient(ImapServer, ImapPort, ImapUserName, ImapPassword, AuthMethod.Login, ImapEnableSSL);
                Client.IdleError += Client_IdleError;

#if DEBUG
                    Console.WriteLine("ImapServer:" + ImapServer + ":" + ImapPort);
#endif
                if (!Client.Authed)
                    Console.WriteLine("Der Imap-Server konnte nicht angemeldet werden.");

                if (ImapUserName == "harmschnakenberg@gmx.de")
                {
                    Client.DefaultMailbox = "Kreualarm"; // zum testen von meinem privat-Account
                    Console.WriteLine("Default Mailbox ist >" + Client.DefaultMailbox + "<");
                }

                Client.NewMessage += new EventHandler<IdleMessageEventArgs>(OnNewMessage);
            }
            catch (S22.Imap.InvalidCredentialsException invalid_auth)
            {
                string txt = "E-Mails können nicht empfangen werden. Fehler bei der Anmeldung: " + invalid_auth;
                Console.WriteLine(txt);
                Sql.InsertLog(1, txt);
            }
        }

        public EmailListener(string imapServer, int imapPort, string imapUserName, string imapPassword, bool imapEnableSSL)
        {
            ImapServer = imapServer;
            ImapPort = imapPort;
            ImapUserName = imapUserName;
            ImapPassword = imapPassword;
            ImapEnableSSL = imapEnableSSL;

            Client = new ImapClient(ImapServer, ImapPort, ImapUserName, ImapPassword, AuthMethod.Login, ImapEnableSSL);
            Client.IdleError += Client_IdleError;
#if DEBUG
                foreach (var item in Client.ListMailboxes())
                {
                    Console.WriteLine(">" + item);
                }
#endif

            //We want to be informed when new messages arrive
            Client.NewMessage += new EventHandler<IdleMessageEventArgs>(OnNewMessage);
        }

        #endregion

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

        public static string ImapServer { get; set; } = "imap.gmx.net";

        public static int ImapPort { get; set; } = 993;

        public static string ImapUserName { get; set; } = @"kreubereit@gmx.de";

        public static string ImapPassword { get; set; } = @"Bernd&Co";

        public static bool ImapEnableSSL { get; set; } = true;

        public static int ImapConnectionRenewInterval { get; set; } = 2;

        #endregion

        #region Methods
        public void Dispose()
        {
            if (Client != null)
                Client.Dispose();
        }

        public void ReadUnseen()
        {
            if (Client is null || !Client.Authed)
            {
                Log.Warning("Ungelesen Emails können nicht abgeholt werden. Keine Verbindung zum Email-Server.", 9641);
                return;
            }
            //Download unseen mail messages
            IEnumerable<uint> uids1 = Client.Search(SearchCondition.Unseen());
            IEnumerable<MailMessage> messages1 = Client.GetMessages(uids1, FetchOptions.Normal);

            foreach (MailMessage m in messages1)
            {
#if DEBUG
                    Console.WriteLine($"{DateTime.Now.ToShortTimeString()} - Neue Email; gesendet {m.Headers["Date"]}; " +
                            $"<{(m.IsBodyHtml ? "html" : "Text")}>; BodyEncoding <{m.BodyEncoding}>; " +
                            $"von {m.From.Address}<"); //: {m.Body.Substring(0, Math.Min(m.Body.Length, 160))}");
#endif

                EmailInEvent?.Invoke(this, m);
            }
        }


        /// <summary>
        /// Prüft, ob der Emailserver Benachrichtigungen bei neu empfangenen Emails unterstützt.
        /// </summary>
        /// <returns>true= Server kann benachrichtigen bei neu empfangenen Emails.</returns>
        public bool IsIdleEmailSupported()
        {
            return Client != null && Client.Authed && Client.Supports("IDLE");
        }

        /// <summary>
        /// Wird aufgerufen, wenn eine neue EMail empfangen wurde.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnNewMessage(object sender, IdleMessageEventArgs e)
        {
            MailMessage m = e.Client.GetMessage(e.MessageUID, FetchOptions.NoAttachments);
#if DEBUG
                Console.WriteLine($"{DateTime.Now.ToShortTimeString()}: Neue Email [{e.MessageUID}] Format: <{(m.IsBodyHtml ? "html" : "Text")}> <{m.BodyEncoding}> Transport<{m.BodyTransferEncoding}> von {m.From.Address}<: {m.Body.Substring(0, Math.Min(m.Body.Length, 160))}");
#endif
            EmailInEvent?.Invoke(this, m);
        }

        #endregion
    }

    //ENDE S22.Imap */
    #endregion

    #region Emails lesen mit MimeKit und MailKit

    //class EmailListener : IDisposable
    //{

    //    public EmailListener()
    //    {
    //        IdleClient = new IdleClient(ImapServer, ImapPort, SecureSocketOptions.Auto, ImapUserName, ImapPassword);

    //    }

    //    private static IdleClient IdleClient;

    //    /// <summary>
    //    /// Wird ausgelöst, wenn eine Email empfangen wurde.
    //    /// </summary>


    //    #region Properties

    //    public static string ImapServer { get; set; } = "imap.gmx.net";

    //    public static int ImapPort { get; set; } = 993;

    //    public static string ImapUserName { get; set; } = @"kreubereit@gmx.de";

    //    public static string ImapPassword { get; set; } = @"Bernd&Co";

    //    public static bool ImapEnableSSL { get; set; } = true;

    //    public static int ImapConnectionRenewInterval { get; set; } = 2;

    //    #endregion


    //    /// <summary>
    //    /// Prüft, ob der Emailserver Benachrichtigungen bei neu empfangenen Emails unterstützt.
    //    /// </summary>
    //    /// <returns>true= Server kann benachrichtigen bei neu empfangenen Emails.</returns>
    //    public bool IsIdleEmailSupported()
    //    {
    //        return IdleClient.IsIdleSupported();
    //    }

    //    public void ReadUnseen()
    //    {
    //        if (IdleClient is null || !IdleClient.IsAuthenticated() )
    //        {
    //            Log.Warning("Ungelesen Emails können nicht abgeholt werden. Keine Verbindung zum Email-Server.", 9641);
    //            return;
    //        }
    //        //Download unseen mail messages
    //        Task idleTask = IdleClient.FetchMessageSummariesAsync(false);
    //        idleTask.Wait();

    //        IdleClient.Exit();
    //        idleTask.GetAwaiter().GetResult();
    //    }

    //    public void Dispose()
    //    {
    //        if (IdleClient != null)
    //        {
    //            IdleClient.Exit();
    //            IdleClient.Dispose();
    //        }
    //    }

    //}

    //class IdleClient : IDisposable
    //{
    //    readonly string host, username, password;
    //    readonly SecureSocketOptions sslOptions;
    //    readonly int port;
    //    List<IMessageSummary> messages;
    //    CancellationTokenSource cancel;
    //    CancellationTokenSource done;
    //    FetchRequest request;
    //    bool messagesArrived;
    //    ImapClient client;

    //    public IdleClient(string host, int port, SecureSocketOptions sslOptions, string username, string password)
    //    {
    //        this.client = new ImapClient(new ProtocolLogger(Console.OpenStandardError()));
    //        this.request = new FetchRequest(MessageSummaryItems.Full | MessageSummaryItems.UniqueId);
    //        this.messages = new List<IMessageSummary>();
    //        this.cancel = new CancellationTokenSource();
    //        this.sslOptions = sslOptions;
    //        this.username = username;
    //        this.password = password;
    //        this.host = host;
    //        this.port = port;
    //    }


    //    async Task ReconnectAsync()
    //    {
    //        if (!client.IsConnected)
    //            await client.ConnectAsync(host, port, sslOptions, cancel.Token);

    //        if (!client.IsAuthenticated)
    //        {
    //            await client.AuthenticateAsync(username, password, cancel.Token);

    //            await client.Inbox.OpenAsync(FolderAccess.ReadOnly, cancel.Token);
    //        }
    //    }

    //   public async Task FetchMessageSummariesAsync(bool print)
    //    {
    //        IList<IMessageSummary> fetched = null;

    //        do
    //        {
    //            try
    //            {
    //                // fetch summary information for messages that we don't already have
    //                int startIndex = messages.Count;

    //                fetched = client.Inbox.Fetch(startIndex, -1, request, cancel.Token);
    //                break;
    //            }
    //            catch (ImapProtocolException)
    //            {
    //                // protocol exceptions often result in the client getting disconnected
    //                await ReconnectAsync();
    //            }
    //            catch (IOException)
    //            {
    //                // I/O exceptions always result in the client getting disconnected
    //                await ReconnectAsync();
    //            }
    //        } while (true);

    //        foreach (var message in fetched)
    //        {
    //            if (print)
    //                Console.WriteLine("{0}: new message: {1}", client.Inbox, message.Envelope.Subject);

    //            EmailListener .EmailInEvent?.Invoke(this, m);
    //            messages.Add(message);
    //        }
    //    }

    //    async Task WaitForNewMessagesAsync()
    //    {
    //        do
    //        {
    //            try
    //            {
    //                if (client.Capabilities.HasFlag(ImapCapabilities.Idle))
    //                {
    //                    // Note: IMAP servers are only supposed to drop the connection after 30 minutes, so normally
    //                    // we'd IDLE for a max of, say, ~29 minutes... but GMail seems to drop idle connections after
    //                    // about 10 minutes, so we'll only idle for 9 minutes.
    //                    done = new CancellationTokenSource(new TimeSpan(0, 9, 0));
    //                    try
    //                    {
    //                        await client.IdleAsync(done.Token, cancel.Token);
    //                    }
    //                    finally
    //                    {
    //                        done.Dispose();
    //                        done = null;
    //                    }
    //                }
    //                else
    //                {
    //                    // Note: we don't want to spam the IMAP server with NOOP commands, so lets wait a minute
    //                    // between each NOOP command.
    //                    await Task.Delay(new TimeSpan(0, 1, 0), cancel.Token);
    //                    await client.NoOpAsync(cancel.Token);
    //                }
    //                break;
    //            }
    //            catch (ImapProtocolException)
    //            {
    //                // protocol exceptions often result in the client getting disconnected
    //                await ReconnectAsync();
    //            }
    //            catch (IOException)
    //            {
    //                // I/O exceptions always result in the client getting disconnected
    //                await ReconnectAsync();
    //            }
    //        } while (true);
    //    }

    //    async Task IdleAsync()
    //    {
    //        do
    //        {
    //            try
    //            {
    //                await WaitForNewMessagesAsync();

    //                if (messagesArrived)
    //                {
    //                    await FetchMessageSummariesAsync(true);
    //                    messagesArrived = false;
    //                }
    //            }
    //            catch (OperationCanceledException)
    //            {
    //                break;
    //            }
    //        } while (!cancel.IsCancellationRequested);
    //    }

    //    public async Task RunAsync()
    //    {
    //        // connect to the IMAP server and get our initial list of messages
    //        try
    //        {
    //            await ReconnectAsync();
    //            await FetchMessageSummariesAsync(false);
    //        }
    //        catch (OperationCanceledException)
    //        {
    //            await client.DisconnectAsync(true);
    //            return;
    //        }

    //        // Note: We capture client.Inbox here because cancelling IdleAsync() *may* require
    //        // disconnecting the IMAP client connection, and, if it does, the `client.Inbox`
    //        // property will no longer be accessible which means we won't be able to disconnect
    //        // our event handlers.
    //        var inbox = client.Inbox;

    //        // keep track of changes to the number of messages in the folder (this is how we'll tell if new messages have arrived).
    //        inbox.CountChanged += OnCountChanged;

    //        // keep track of messages being expunged so that when the CountChanged event fires, we can tell if it's
    //        // because new messages have arrived vs messages being removed (or some combination of the two).
    //        inbox.MessageExpunged += OnMessageExpunged;

    //        // keep track of flag changes
    //        inbox.MessageFlagsChanged += OnMessageFlagsChanged;

    //        await IdleAsync();

    //        inbox.MessageFlagsChanged -= OnMessageFlagsChanged;
    //        inbox.MessageExpunged -= OnMessageExpunged;
    //        inbox.CountChanged -= OnCountChanged;

    //        await client.DisconnectAsync(true);
    //    }

    //    // Note: the CountChanged event will fire when new messages arrive in the folder and/or when messages are expunged.
    //    void OnCountChanged(object sender, EventArgs e)
    //    {
    //        var folder = (ImapFolder)sender;

    //        // Note: because we are keeping track of the MessageExpunged event and updating our
    //        // 'messages' list, we know that if we get a CountChanged event and folder.Count is
    //        // larger than messages.Count, then it means that new messages have arrived.
    //        if (folder.Count > messages.Count)
    //        {
    //            int arrived = folder.Count - messages.Count;

    //            if (arrived > 1)
    //                Console.WriteLine("\t{0} new messages have arrived.", arrived);
    //            else
    //                Console.WriteLine("\t1 new message has arrived.");

    //            // Note: your first instinct may be to fetch these new messages now, but you cannot do
    //            // that in this event handler (the ImapFolder is not re-entrant).
    //            // 
    //            // Instead, cancel the `done` token and update our state so that we know new messages
    //            // have arrived. We'll fetch the summaries for these new messages later...
    //            messagesArrived = true;
    //            done?.Cancel();
    //        }
    //    }

    //    void OnMessageExpunged(object sender, MessageEventArgs e)
    //    {
    //        var folder = (ImapFolder)sender;

    //        if (e.Index < messages.Count)
    //        {
    //            var message = messages[e.Index];

    //            Console.WriteLine("{0}: message #{1} has been expunged: {2}", folder, e.Index, message.Envelope.Subject);

    //            // Note: If you are keeping a local cache of message information
    //            // (e.g. MessageSummary data) for the folder, then you'll need
    //            // to remove the message at e.Index.
    //            messages.RemoveAt(e.Index);
    //        }
    //        else
    //        {
    //            Console.WriteLine("{0}: message #{1} has been expunged.", folder, e.Index);
    //        }
    //    }

    //    void OnMessageFlagsChanged(object sender, MessageFlagsChangedEventArgs e)
    //    {
    //        var folder = (ImapFolder)sender;

    //        Console.WriteLine("{0}: flags have changed for message #{1} ({2}).", folder, e.Index, e.Flags);
    //    }

    //    public bool IsIdleSupported()
    //    {
    //        return client != null && client.IsAuthenticated && client.Capabilities.HasFlag(ImapCapabilities.Idle);
    //    }

    //    public bool IsAuthenticated()
    //    {
    //        return client != null && client.IsAuthenticated;
    //    }

    //    public event EventHandler<MailMessage> EmailInEvent;

    //    public void Exit()
    //    {
    //        cancel.Cancel();
    //    }

    //    public void Dispose()
    //    {
    //        client.Dispose();
    //        cancel.Dispose();
    //    }

    //}

    //#endregion

    //#region Microsoft Exchange Webservice
    //class Exchange
    //{
    //    #region Properties

    //    public static string ImapServer { get; set; } = "imap.gmx.net";

    //    public static int ImapPort { get; set; } = 993;

    //    public static string ImapUserName { get; set; } = @"kreubereit@gmx.de";

    //    public static string ImapPassword { get; set; } = @"Bernd&Co";

    //    public static bool ImapEnableSSL { get; set; } = true;

    //    public static int ImapConnectionRenewInterval { get; set; } = 2;

    //    #endregion

    //    public void Connect()
    //    {
    //        ExchangeService _service;

    //        Console.WriteLine("Registering Exchange connection");

    //        _service = new ExchangeService
    //        {
    //            Credentials = new WebCredentials("schnakenberg@kreutztraeger.com", "schnaha12")
    //        };

    //        // This is the office365 webservice URL
    //        _service.Url = new Uri("https://outlook.office365.com/EWS/Exchange.asmx");

    //        PropertySet propSet = new PropertySet(BasePropertySet.FirstClassProperties);
    //        propSet.Add(ItemSchema.MimeContent);
    //        propSet.Add(ItemSchema.TextBody);

    //        foreach (EmailMessage email in _service.FindItems(WellKnownFolderName.Inbox, new ItemView(10)))
    //        {
    //            var message = EmailMessage.Bind(_service, email.Id, propSet);

    //            Console.WriteLine("Email body: " + message.Sender + ":\r\n" + message.TextBody);
    //            Console.WriteLine();
    //        }


    //    }




    //}


     #endregion

}



