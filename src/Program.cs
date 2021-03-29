using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;
using Microsoft.Extensions.Logging;

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
            .ConfigureLogging(b => {
                b.ClearProviders();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<DiscordService>();
                services.AddSingleton<Serilog.ILogger>(p =>
                {
                    return new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .MinimumLevel.Verbose()
                    .WriteTo.Console(new CompactJsonFormatter())
                    .CreateLogger();
                });
                services.AddLogging(p => {
                    var built = p.Services.BuildServiceProvider();
                    p.AddSerilog(built.GetRequiredService<Serilog.ILogger>(), true);
                });
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
