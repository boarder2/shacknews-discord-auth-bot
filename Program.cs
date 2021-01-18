using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace shacknews_discord_auth_bot
{
    class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<DiscordService>();
                services.AddLogging();
                services.AddSingleton<HttpClient>(provider =>
                {
                    var handler = new HttpClientHandler();
                    if (handler.SupportsAutomaticDecompression)
                    {
                        handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
                    }
                    var client = new HttpClient(handler);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Shacknews Discord Auth Bot");
                    return client;
                });
                services.AddSingleton<AuthService>();
            });
    }
}
