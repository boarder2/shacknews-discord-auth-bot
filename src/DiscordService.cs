using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace shacknews_discord_auth_bot
{
    public class DiscordService : IHostedService
    {
        private DiscordSocketClient _client;
        private AuthService _auth;
        private ILogger<DiscordService> _logger;
        private HttpClient _httpClient;
        private IConfiguration _config;
        private string[] _rolesToAssign;
        private string[] _rolesToUnasign;
        private string _authChannelName;

        public DiscordService(ILogger<DiscordService> logger, HttpClient httpClient, IConfiguration config, AuthService auth)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = config;
            _auth = auth;

            // appLifetime.ApplicationStarted.Register(OnStarted);
            // appLifetime.ApplicationStopping.Register(OnStopping);
            // appLifetime.ApplicationStopped.Register(OnStopped);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client = new DiscordSocketClient();

            _client.Log += Log;
            _client.MessageReceived += GotAMessage;
            _client.Ready += () =>
            {
                Console.WriteLine($"Logged in as {_client.CurrentUser.Username}");
                return Task.CompletedTask;
            };

            var token = _config.GetValue<string>("SHACK_DISCORD_AUTH_BOT_TOKEN");
            _rolesToAssign = _config.GetValue<string>("SHACK_DISCORD_AUTH_BOT_ROLLS_TO_ASSIGN", "Shacker").Split(";");
            _rolesToUnasign = _config.GetValue<string>("SHACK_DISCORD_AUTH_BOT_ROLLS_TO_REMOVE", "Stillnewb;Guest").Split(";");
            _authChannelName = _config.GetValue<string>("SHACK_DISCORD_AUTH_BOT_CHANNEL_NAME", "help-and-requests");

            Console.WriteLine($"Logging in with token {token}");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private Task Log(LogMessage msg)
        {
            _logger.LogInformation(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task GotAMessage(SocketMessage message)
        {
            try
            {
                if (message.Author == _client.CurrentUser) return;

                if (message.Channel.Name.Equals(_authChannelName))
                {
                    Console.WriteLine($"{message.Channel}: {message.Author}: {message.ToString()}");
                    if (message.Content.StartsWith("!verify "))
                    {
                        await SendAuthMessage(message);
                        return;
                    }
                }
                else if (message.Channel.Name.StartsWith("@"))
                {
                    if (_auth.MatchTokenAndRemove(message.Author, message.Content, out var request))
                    {
                        await ProcessValidVerification(message, request);
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"I cannot handle requests directly. Use the `!verify` command on the server you want to verify your account with.");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error handling message '{message}' for author '{message.Author}'");
            }
        }

        private async Task SendAuthMessage(SocketMessage message)
        {
            try
            {
                var username = message.ToString().Replace("!verify ", ""); // This is sketchy but whatever.
                if (string.IsNullOrWhiteSpace(username))
                {
                    await message.Channel.SendMessageAsync("Improper format for the !verify command. Use `!verify Your Shacknews Username`");
                    return;
                }
                await _auth.CreateAuthToken(message.Author as IGuildUser, username);
                await message.Author.SendMessageAsync($"SM Sent to `{username}`. Reply with your token for verification. This token is valid for 5 minutes.");
                return;
            }
            catch (Exception e)
            {
                var errorGuid = Guid.NewGuid();
                await message.Author.SendMessageAsync($"Unable to verify, please try again later. Contact an admin with the following error code for more info `{errorGuid}`");
                _logger.LogError(e, $"{errorGuid} - Error creating auth token for '{message.Author}'");
            }
        }

        private async Task ProcessValidVerification(SocketMessage message, VerificationRequest request)
        {
            try
            {
                var guild = request.User.Guild;
                var rolesToAssign = guild.Roles.Where(r => _rolesToAssign.Contains(r.Name));
                var rolesToUnasign = guild.Roles.Where(r => _rolesToUnasign.Contains(r.Name));
                _logger.LogInformation($"Assigning roles for {message.Author}");
                await request.User.AddRolesAsync(rolesToAssign);
                await request.User.RemoveRolesAsync(rolesToUnasign);
                await message.Channel.SendMessageAsync($"Verification succeded!");
                _logger.LogInformation($"Verification success for {message.Author}");
            }
            catch (Exception e)
            {
                var errorGuid = Guid.NewGuid();
                await message.Author.SendMessageAsync($"Unable to verify, please try again later. Contact an admin with the following error code for more info `{errorGuid}`");
                _logger.LogError(e, $"{errorGuid} - Error verifying '{message.Author}'");
            }
        }
    }
}