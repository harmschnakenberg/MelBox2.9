//using Microsoft.Graph;
//using Microsoft.Identity.Client;
//using Microsoft.Exchange.WebServices;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.Http.Headers;
//using System.Text;
//using System.Threading.Tasks;
//using Microsoft.Exchange.WebServices.Data;
//using System.Net;
//using System.Net.Security;
//using System.Security.Cryptography.X509Certificates;
//using Azure.Core;
//using Azure.Identity;
//using Microsoft.Graph;

using Azure.Core;
//using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.IdentityModel;
using System.Windows;
using Microsoft.Graph.SecurityNamespace;
using Microsoft.Graph.CallRecords;
using System.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using OfficeOpenXml.FormulaParsing.LexicalAnalysis;
using Azure.Identity;
using System.Threading;
using System.Windows.Forms;

namespace MelBox2
{
    // To change from Microsoft public cloud to a national cloud, use another value of AzureCloudInstance

    public class MelExchange
    {
        //Quelle: https://aad.portal.azure.com/#view/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/~/Overview
        //Name:             MelBox2
        //Anwendungs-ID:    170abe04-b2d5-4120-9e68-cf8ac6f3c7f5
        //Objekt-ID;        5946fae0-bc3e-4c26-b7db-3db3998b4620

        internal static string clientId = "170abe04-b2d5-4120-9e68-cf8ac6f3c7f5"; //Anwendungs-ID für MelBox2 bei MS Azure
        internal static string authTenant = "6f135fc0-3063-4a29-b094-08146694ddb8"; //Verzeichnis-ID (Mandant) für MelBox2 bei MS Azure
                                                                                    // f4777693-e729-4627-bd59-1e8076f6aa5c  //Gegeime ID
                                                                                    // ObjectId 4d83350b-3eb1-4dcb-bd5c-23e59efefcda
        internal static string clientSecret = "R1k8Q~mtoACkPN54r_mJyJg-Jvg19dAcgFfGYaM2"; //Wert
        internal static string redirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient"; // Eingestellt unter https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Authentication/appId/170abe04-b2d5-4120-9e68-cf8ac6f3c7f5
        internal static string Instance = "https://login.microsoftonline.com/";
  
        private static GraphServiceClient GraphClient {
            get {              
                return AuthProvider();
            } 
        }

//        //Quelle: https://briantjackett.com/2018/12/13/introduction-to-calling-microsoft-graph-from-a-c-net-core-application/
//        internal static void InitializeGraph()
//        {
//            //var clientId = "<AzureADAppClientId>";
//            //var clientSecret = "<AzureADAppClientSecret>";
//            //var redirectUri = "<AzureADAppRedirectUri>";
//            //var authority = "https://login.microsoftonline.com/<AzureADAppTenantId>/v2.0";
//            var authority = $"https://login.microsoftonline.com/{authTenant}/v2.0";
//            var cca = ConfidentialClientApplicationBuilder.Create(clientId)
//                                                          .WithAuthority(authority)
//                                                          .WithRedirectUri(redirectUri)
//                                                          .WithClientSecret(clientSecret)
//                                                          .Build();

        //            var cca2 = PublicClientApplicationBuilder.Create(clientId)
        //                                                    .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
        //                                                    .WithAuthority(AzureCloudInstance.AzurePublic, authTenant)
        //                                                    .Build();

        //            // use the default permissions assigned from within the Azure AD app registration portal
        //            List<string> scopes = new List<string>
        //            {
        //                "https://graph.microsoft.com/.default"
        //            };



        //            var authenticationProvider = new MsalAuthenticationProvider(cca, scopes.ToArray());

        //            GraphServiceClient graphClient1 = new GraphServiceClient(authenticationProvider);


        //            try
        //            {
        //                //Beispielabfrage

        //                var graphResult1 = graphClient1.Users.Request().GetAsync().Result;
        //                Console.WriteLine("###################### " + graphResult1[0].DisplayName);
        //                // !!! FEHLERMELDUNG:
        //                // Microsoft.Graph.ServiceException: Code: Authorization_RequestDenied
        //                // Message: Insufficient privileges to complete the operation.
        //                // Freischalten lassen durch Kreutzträger-Admin?

        //            }
        //            catch (Exception ex)
        //            {

        //                Console.WriteLine(ex.ToString());

        //                //graphResult = graphClient.Me.Request().GetAsync().Result;
        //                //Console.WriteLine("###################### " + graphResult.DisplayName);
        //                // !!! FEHLERMELDUNG:
        //                // Microsoft.Graph.ServiceException: Code: BadRequest
        //                // Message: /me request is only valid with delegated authentication flow.

        //            }
        //        }


        //        // This class encapsulates the details of getting a token from MSAL and exposes it via the
        //        // IAuthenticationProvider interface so that GraphServiceClient or AuthHandler can use it.
        //        // A significantly enhanced version of this class will in the future be available from
        //        // the GraphSDK team.  It will supports all the types of Client Application as defined by MSAL.
        //        public class MsalAuthenticationProvider : IAuthenticationProvider
        //        {
        //            private IConfidentialClientApplication _clientApplication;
        //            private string[] _scopes;

        //            public MsalAuthenticationProvider(IConfidentialClientApplication clientApplication, string[] scopes)
        //            {
        //                _clientApplication = clientApplication;
        //                _scopes = scopes;
        //            }


        //            /// <summary>
        //            /// Update HttpRequestMessage with credentials
        //            /// </summary>
        //            public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        //            {
        //                var token = await GetTokenAsync();
        //                request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
        //            }


        //            /// <summary>
        //            /// Acquire Token
        //            /// </summary>
        //            public async Task<string> GetTokenAsync()
        //            {
        //                AuthenticationResult authResult = await _clientApplication.AcquireTokenForClient(_scopes).ExecuteAsync();
        //                return authResult.AccessToken;
        //            }
        //        }


        //        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        //        {
        //            var token = await GetTokenAsync2();
        //            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
        //        }
















        //        //// Quelle: https://learn.microsoft.com/en-us/graph/sdks/choose-authentication-providers?tabs=CS#client-credentials-provider
        //        //// DelegateAuthenticationProvider is a simple auth provider implementation
        //        //// that allows you to define an async function to retrieve a token
        //        //// Alternatively, you can create a class that implements IAuthenticationProvider
        //        //// for more complex scenarios
        //        //var authProvider = new DelegateAuthenticationProvider(async (request) => {
        //        //    // Use Microsoft.Identity.Client to retrieve token
        //        //    var assertion = new UserAssertion(token);
        //        //    var result = await cca.AcquireTokenOnBehalfOf(scopes, assertion).ExecuteAsync();

        //        //    request.Headers.Authorization =
        //        //        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
        //        //});

        //        //var graphClient = new GraphServiceClient(authProvider);














        //        internal static async Task<string> GetTokenAsync2()
        //        {


        //            // Quelle: https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Quickstart/appId/170abe04-b2d5-4120-9e68-cf8ac6f3c7f5/isMSAApp~/false
        //            IPublicClientApplication publicClientApp = PublicClientApplicationBuilder.Create(clientId)
        //                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
        //                .WithAuthority(AzureCloudInstance.AzurePublic, authTenant)
        //                .Build();

        //            List<string> scopes = new List<string>
        //            {
        //                "mail.read"
        //            };

        //            AuthenticationResult authResult = null;

        //            try
        //            {
        //                // Stille Benutzeranmeldung
        //                var accounts = await publicClientApp.GetAccountsAsync();
        //                var firstAccount = accounts.FirstOrDefault();
        //                authResult = await publicClientApp.AcquireTokenSilent(scopes, firstAccount)
        //                                                      .ExecuteAsync();
        //            }
        //            catch (MsalUiRequiredException)
        //            {
        //                // Interaktive Benutzeranmeldung
        //                authResult = await publicClientApp.AcquireTokenInteractive(scopes)
        //                                      .ExecuteAsync();

        //            }

        //            string token = authResult.AccessToken;

        //#if DEBUG
        //            IAccount account = authResult.Account;
        //            string userName = account.Username;

        //            Console.WriteLine("Der angemeldete Benutzer ist: " + userName + "\r\nToken: " + token); // FUNKTIONIERT!
        //#endif

        //            return token;
        //            // request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);

        //        }


        //        //Quelle: https://stackoverflow.com/questions/43289741/get-user-name-and-email-using-microsoft-identity-client
        //        public static async Task<string> GetUserData(string token)
        //        {
        //            //get data from API
        //            HttpClient client = new HttpClient();
        //            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
        //            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token);
        //            HttpResponseMessage response = await client.SendAsync(message);
        //            string responseString = await response.Content.ReadAsStringAsync();
        //            if (response.IsSuccessStatusCode)
        //                return responseString;
        //            else
        //                return null;
        //        }


        public static async void Test()
        {

            try
            {

                #region neue E-Mails lesen

                var inboxMessages = await GraphClient.Me.MailFolders["inbox"].Messages.Delta().Request().GetAsync();
                
                Console.WriteLine($"Es sind {inboxMessages.Count} neue Nachrichten im Posteingang.");

                //List<string> senderNames = inboxMessages.Select(x => x.Sender.EmailAddress.Name).ToList();

                //foreach (var senderName in senderNames)
                //    Console.WriteLine(senderName);


                #endregion


                #region Unterordner "Posteingang" lesen
               
                // var mailFolders = await GraphClient.Me.MailFolders
                var inboxSubFolders = await GraphClient.Me.MailFolders["inbox"].ChildFolders
               .Request()
                .GetAsync();

                List<string> inboxSubFolderNames = inboxSubFolders.Select(x => x.DisplayName).ToList();

                Console.WriteLine($"Es gibt {inboxSubFolders.Count} Unterordner im Posteingang.");

#if DEBUG
                foreach (var mailFolder in inboxSubFolders)
                {
                    //mailFolderIds.Add(mailFolder.DisplayName, mailFolder.Id);
                    Console.WriteLine("\t- " + mailFolder.DisplayName + "\t" + mailFolder.Id);
                }
#endif

                #endregion

              
                bool createdNeuSubfolder = false;

                foreach (var newMessage in inboxMessages)
                {
                    #region ggf. Unterordner im Posteingang erzeugen
                    string destFolderName = newMessage.From.EmailAddress.Name;

                    if (destFolderName.Length > 30)
                        destFolderName = destFolderName.Substring(0, 30);


                    //Gibt es den Sendernamen als InBox-Ordner?
                    if (!inboxSubFolderNames.Contains(destFolderName))
                    {
                        Console.WriteLine($"Erstelle Unterordner >{destFolderName}<");

                        //ggf. InBox-Unterordner erstellen
                        var mailFolder = new MailFolder
                        {
                            DisplayName = destFolderName,
                            IsHidden = false
                        };

                        await GraphClient.Me.MailFolders["inbox"].ChildFolders
                            .Request()
                            .AddAsync(mailFolder);

                        createdNeuSubfolder = true;
                    }

                    if (createdNeuSubfolder)
                        inboxSubFolders = await GraphClient.Me.MailFolders["inbox"].ChildFolders
                                            .Request()
                                            .GetAsync();
                    #endregion

                    #region Neue E-Mail in Unterordner kopieren

                    Console.WriteLine("Verschiebe: " +
                        newMessage.Sender.EmailAddress.Name + "\r\n\t"
                        //+ newMessage.BodyPreview + "\r\n\tID= "
                        //+ newMessage.Id + "\r\n\r\n"
                        );

                    MailFolder destination = inboxSubFolders.Where(x => x.DisplayName.Substring(0,30) == destFolderName).FirstOrDefault();

                    if (destination?.Id == null)
                    {
                        Console.WriteLine($"Den Unterordner '{destFolderName}' gibt es nicht im Posteingang.");
                        continue;
                    }

                        await GraphClient.Me.Messages[newMessage.Id]
                        .Move(destination.Id)
                        .Request()
                        .PostAsync();
                    #endregion

                    //TODO: Email in Datenbank schreiben und auswerten
                }

            }
            catch(Exception ex)
            {
                Console.WriteLine("Fehler beim Abrufen aus Exchange:\r\n" + ex + "\r\n\r\n" + ex.InnerException);
            }
        }


        //Quelle: https://learn.microsoft.com/en-us/graph/sdks/choose-authentication-providers?tabs=CS#interactive-provider
        public static GraphServiceClient AuthProvider()
        {
            Console.WriteLine("Ich versuche einen Token zu erhalten...");

            var scopes = new[] { "Mail.ReadWrite" };

            // Multi-tenant apps can use "common",
            // single-tenant apps must use the tenant ID from the Azure portal
            var tenantId = authTenant; // "common";

            // Value from app registration
            //var clientId = "YOUR_CLIENT_ID";

            CacheOptions cacheOptions = new CacheOptions
            {
                UseSharedCache = true
            };

            IPublicClientApplication pca = PublicClientApplicationBuilder
                        .Create(clientId)                        
                        .WithTenantId(tenantId)                                    
                        .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                        .WithAuthority(AzureCloudInstance.AzurePublic, authTenant)
                        //.WithBroker(true) //Fehlermeldung: weiteres Package erforderlich
                        .WithCacheOptions(cacheOptions) // Wichtig!
                        .Build();

             IEnumerable<IAccount> accounts = pca.GetAccountsAsync().Result;
             IAccount firstAccount = accounts.FirstOrDefault();

            Console.WriteLine(accounts.Count() + " Accounts - Angemeldet als " + firstAccount?.Username);
            AuthenticationResult result = null;

            // DelegateAuthenticationProvider is a simple auth provider implementation
            // that allows you to define an async function to retrieve a token
            // Alternatively, you can create a class that implements IAuthenticationProvider
            // for more complex scenarios
            var authProvider = new DelegateAuthenticationProvider(async (request) =>
            {
                // Use Microsoft.Identity.Client to retrieve token
                // AuthenticationResult result = await pca.AcquireTokenByIntegratedWindowsAuth(scopes).ExecuteAsync(); // -> Fehlermeldung

                try 
                {
                    // Stille Benutzeranmeldung
                    result = await pca.AcquireTokenSilent(scopes, firstAccount).ExecuteAsync();

                    Console.WriteLine("Stille Anmeldung erfolgreich.");
                }
                catch (MsalUiRequiredException)
                {
                    try
                    {
                        // Interaktive Benutzeranmeldung
                        result = await pca.AcquireTokenInteractive(scopes).ExecuteAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Anmeldung bei Microsoft fehlgeschlagen:\r\n\r\n" + ex);
                        return;
                    }
                }

                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
            });

            GraphServiceClient graphClient = new GraphServiceClient(authProvider);

            return graphClient;
        }

        /// <summary>
        /// Benutzer /Me nicht zulässig! Erfordert daher Freigabe von Azure-Admin
        /// </summary>
        /// <returns></returns>
        public static GraphServiceClient AuthProviderAppId()
        {
            // The client credentials flow requires that you request the
            // /.default scope, and preconfigure your permissions on the
            // app registration in Azure. An administrator must grant consent
            // to those permissions beforehand.
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            // Multi-tenant apps can use "common",
            // single-tenant apps must use the tenant ID from the Azure portal
           // var tenantId = "common";

            // Values from app registration
            //var clientId = "YOUR_CLIENT_ID";
            //var clientSecret = "YOUR_CLIENT_SECRET";

            // using Azure.Identity;
            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            // https://learn.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
            var clientSecretCredential = new ClientSecretCredential(
                authTenant, clientId, clientSecret, options);

            return new GraphServiceClient(clientSecretCredential, scopes);
        }


        /// <summary>
        /// 
        /// Quelle: https://learn.microsoft.com/en-us/graph/api/resources/message?view=graph-rest-1.0
        /// </summary>
        public static async void SendEmail()
        {
            // GraphServiceClient graphClient = new GraphServiceClient(authProvider);

            var message = new Microsoft.Graph.Message
            {
                Subject = "Meet for lunch?",
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = "The new cafeteria is open."
                },
                ToRecipients = new List<Recipient>()
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = "fannyd@contoso.onmicrosoft.com"
                        }
                    }
                },
                            CcRecipients = new List<Recipient>()
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = "danas@contoso.onmicrosoft.com"
                        }
                    }
                }
            };

            var saveToSentItems = false;

            await GraphClient.Me
                .SendMail(message, saveToSentItems)
                .Request()
                .PostAsync();

        }

    }
}