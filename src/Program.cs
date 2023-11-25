using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Formatting.Compact;
using shacknews_discord_auth_bot;

Log.Logger = new LoggerConfiguration()
					.Enrich.FromLogContext()
					.MinimumLevel.Verbose()
					.WriteTo.Console(new CompactJsonFormatter())
					.CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<DiscordService>();
builder.Logging.ClearProviders();
builder.Services.AddSerilog();

builder.Services.AddSingleton<HttpClient>(provider =>
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

builder.Services.AddSingleton<AuthService>();

var app = builder.Build();
await app.RunAsync();