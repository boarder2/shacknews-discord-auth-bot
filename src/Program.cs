﻿using System.Net.Http;
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
            .ConfigureServices((hostContext, services) =>
            {
                var logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .MinimumLevel.Verbose()
                    .WriteTo.Console(new CompactJsonFormatter())
                    .CreateLogger();
                services.AddHostedService<DiscordService>();
                services.AddSingleton<Serilog.ILogger>(logger);
                services.AddLogging(p => {
                    p.ClearProviders();
                    p.AddSerilog(logger);
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
