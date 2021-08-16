using Grapevine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MelBox2
{
    public class Startup
    {
        public IConfiguration Configuration { get; private set; }

        private readonly string _serverPort = PortFinder.FindNextLocalOpenPort(1234);

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

#pragma warning disable CA1822 // Mark members as static
        public void ConfigureServices(IServiceCollection services)
#pragma warning restore CA1822 // Mark members as static
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(LogLevel.Warning);
            });
        }

        public void ConfigureServer(IRestServer server)
        {
            server.Prefixes.Add($"http://localhost:{_serverPort}/");

            /* Configure Router Options (if supported by your router implementation) */
            server.Router.Options.SendExceptionMessages = true;
        }
    }

}
