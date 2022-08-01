using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MelBox2.Allgemein
{
    //internal class MsExchange
    //{

    //    public async Task SendEmail()
    //    {
    //        // Arrange.
    //        GraphServiceClient graphService = new GraphServiceClient();
    //        string subject = "Test email from ASP.NET 4.6 Connect sample";
    //        string bodyContent = "<html><body>The body of the test email.</body></html>";
    //        List<Recipient> recipientList = new List<Recipient>();
    //        recipientList.Add(new Recipient
    //        {
    //            EmailAddress = new EmailAddress
    //            {
    //                Address = userName
    //            }
    //        });
    //        Message message = new Message
    //        {
    //            Body = new ItemBody
    //            {
    //                Content = bodyContent,
    //                ContentType = BodyType.Html,
    //            },
    //            Subject = subject,
    //            ToRecipients = recipientList
    //        };

    //        // Act
    //        Task task = graphService.SendEmail(client, message);

    //        // Assert
    //        Task.WaitAll(task);
    //    }


    //    //#############
    //    public async void Test()
    //    {
    //        var tenantId = "you-azure-tenand-id";
    //        var clientId = "azure-ad-application-id";
    //        var clientSecret = "unique-secret-generated-for-this-console-app";

    //        // Configure app builder
    //        var authority = $"https://login.microsoftonline.com/{tenantId}";
    //        var app = ConfidentialClientApplicationBuilder
    //            .Create(clientId)
    //            .WithClientSecret(clientSecret)
    //            .WithAuthority(new Uri(authority))
    //            .Build();

    //        // Acquire tokens for Graph API
    //        var scopes = new[] { "https://graph.microsoft.com/.default" };
    //        var authenticationResult = await app.AcquireTokenForClient(scopes).ExecuteAsync();

    //        // Create GraphClient and attach auth header to all request (acquired on previous step)
    //        var graphClient = new GraphServiceClient(
    //            new DelegateAuthenticationProvider(requestMessage => {
    //                requestMessage.Headers.Authorization =
    //                    new AuthenticationHeaderValue("bearer", authenticationResult.AccessToken);

    //                return Task.FromResult(0);
    //            }));

    //        // Call Graph API
    //        var user = await graphClient.Users["Me@domain.com"].Request().GetAsync();

    //   }
    //}
}
