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
using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;

namespace shacknews_discord_auth_bot
{
    public class DiscordService : IHostedService
    {
        private DiscordSocketClient _client;
        private AuthService _auth;
        private ILogger _logger;
        private IDisposable _globalLogContext;
        private HttpClient _httpClient;
        private IConfiguration _config;
        private string[] _rolesToAssign;
        private string[] _rolesToUnasign;
        private string[] _authChannelNames;

        public DiscordService(ILogger logger, HttpClient httpClient, IConfiguration config, AuthService auth)
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
                _logger.Information($"Logged in as {_client.CurrentUser.Username}");
                return Task.CompletedTask;
            };

            var token = _config.GetValue<string>("DISCORD_TOKEN");
            _rolesToAssign = _config.GetValue<string>("ROLLS_TO_ASSIGN", "Shacker").Split(";");
            _rolesToUnasign = _config.GetValue<string>("ROLLS_TO_REMOVE", "StillNewb;Guest").Split(";");
            _authChannelNames = _config.GetValue<string>("CHANNEL_NAMES", "help-and-requests;commands").Split(";");

            _globalLogContext = LogContext.Push(
                new PropertyEnricher("BotVersion", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()),
                new PropertyEnricher("BotMonitorChannels", _authChannelNames, true),
                new PropertyEnricher("BotRolesToAssign", _rolesToAssign, true),
                new PropertyEnricher("BotRolesToUnassign", _rolesToUnasign, true)
            );

            _logger.Information("Bot starting.");
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
            _logger.Information(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task GotAMessage(SocketMessage message)
        {
            using(LogContext.Push(
                new PropertyEnricher("Author", message.Author.ToString()),
                new PropertyEnricher("Content", message.Content),
                new PropertyEnricher("Channel", message.Channel.ToString()),
                new PropertyEnricher("Guild", (message.Author as IGuildUser)?.Guild.ToString())
            ))
            {
                try
                {
                    if (message.Author == _client.CurrentUser) return;

                    if (_authChannelNames.Contains(message.Channel.Name))
                    {
                        if (message.Content.Trim().Equals("!verify"))
                        {
                            await SendUserNameMessage(message);
                            return;
                        }
                        else if (message.Content.Equals("!verify-help"))
                        {
                            await message.Channel.SendMessageAsync("I can do the following things:\r\n`!verify` - Begin the verification process.\r\n`!verify-help` - Show this message.");
                            _logger.Information("Sent help message.");
                        }
                    }
                    else if (message.Channel.Name.StartsWith("@"))
                    {
                        var session = _auth.GetVerificationRequest(message.Author);
                        if (session != null)
                        {
                            //using (LogContext.Push(new PropertyEnricher("VerificationSession", session, true)))
                            //{
                                if (session.SessionState == AuthSessionState.NeedUser)
                                {
                                    await SendTokenMessage(message);
                                    _logger.Information("Direct message.");
                                }
                                else if (session.SessionState == AuthSessionState.NeedToken)
                                {
                                    if (_auth.MatchTokenAndRemove(message.Author, message.Content, out var request))
                                    {
                                        await ProcessValidVerification(message, request);
                                    }
                                    else
                                    {
                                        await message.Author.SendMessageAsync("Token did not match. Please try again.");
                                        _logger.Information("Token didn't match {VerificationSession}", session);
                                    }
                                }
                            //}
                        }
                        else
                        {
                            if (message.ToString().Equals("!version"))
                            {
                                await message.Channel.SendMessageAsync(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
                                _logger.Information("Sent version response.");
                            }
                            else
                            {
                                await message.Channel.SendMessageAsync($"I cannot handle requests directly. Use the `!verify` command on the server you want to verify your account with.\r\nIf you started a authentication session and are seeing this message, your token may have timed out.");
                                _logger.Information("Unhandled message.");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error handling message.");
                }
            }
        }

        private async Task SendUserNameMessage(SocketMessage message)
        {
            try
            {
                _auth.CreateAuthSession(message.Author as IGuildUser);
                await message.Author.SendMessageAsync($"Starting verification session. This proces must be completed within 10 minutes.\r\n\r\nWhat is your shacknews username?");
                _logger.Information($"Starting auth session.");
                return;
            }
            catch (Exception e)
            {
                await SendErrorMessage(e, message.Author, "Unable to verify, please try again later.");
            }
        }

        private async Task SendTokenMessage(SocketMessage message)
        {
            try
            {
                var username = message.Content.Trim();
                await _auth.SetAuthSessionShackNameAndSendSM(message.Author, username);
                await message.Author.SendMessageAsync($"Shackmessage sent to `{username}`. Messages can be found at https://www.shacknews.com/messages\r\nReply here with your token for verification.");
                return;
            }
            catch (Exception e)
            {
                await SendErrorMessage(e, message.Author, "Unable to verify, please try again later.");
            }
        }

        private async Task ProcessValidVerification(SocketMessage message, VerificationRequest request)
        {
            try
            {
                var guild = request.User.Guild;
                var rolesToAssign = guild.Roles.Where(r => _rolesToAssign.Contains(r.Name));
                var rolesToUnassign = guild.Roles.Where(r => _rolesToUnasign.Contains(r.Name));
                _logger.Information("Assigning roles {RolesToAssign} {RolesToUnassign}", rolesToAssign, rolesToUnassign);
                await request.User.AddRolesAsync(rolesToAssign);
                await request.User.RemoveRolesAsync(rolesToUnassign);
                await message.Channel.SendMessageAsync($"Verification succeded!");
                _logger.Information($"Verification success.");
            }
            catch (Exception e)
            {
                await SendErrorMessage(e, message.Author, "Unable to verify, please try again later.");
            }
        }

        private async Task SendErrorMessage(Exception ex, SocketUser user, string message)
        {
            var guid = _logger.LogErrorWithGuid(ex, message);
            await user.SendMessageAsync($"{message}\r\nContact an admin with the following error code for more info `{guid}`");
        }
    }
}