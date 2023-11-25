namespace shacknews_discord_auth_bot;

public class DiscordService(ILogger<DiscordService> _logger, IConfiguration _config, AuthService _auth) : IHostedService
{
	private DiscordSocketClient _client;
	private string[] _rolesToAssign;
	private string[] _rolesToUnasign;
	private bool _addReactions;
	private string[] _authChannelNames;

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		_client = new DiscordSocketClient(new DiscordSocketConfig() { GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildIntegrations | GatewayIntents.DirectMessages | GatewayIntents.GuildMessages | GatewayIntents.MessageContent });

		_client.Log += Log;
		_client.MessageReceived += GotAMessage;
		_client.Ready += () =>
		{
			_logger.LogInformation($"Logged in as {_client.CurrentUser.Username}");
			return Task.CompletedTask;
		};

		var token = _config.GetValue<string>("DISCORD_TOKEN");
		_rolesToAssign = _config.GetValue("ROLLS_TO_ASSIGN", "Shacker").Split(";");
		_rolesToUnasign = _config.GetValue("ROLLS_TO_REMOVE", "StillNewb;Guest").Split(";");
		_addReactions = _config.GetValue("ADD_REACTIONS", true);
		_authChannelNames = _config.GetValue("CHANNEL_NAMES", "help-and-requests;commands").Split(";");

		LogContext.Push(
			 new PropertyEnricher("BotVersion", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()),
			 new PropertyEnricher("BotMonitorChannels", _authChannelNames, true),
			 new PropertyEnricher("BotRolesToAssign", _rolesToAssign, true),
			 new PropertyEnricher("BotRolesToUnassign", _rolesToUnasign, true),
			 new PropertyEnricher("AddReactions", _addReactions, false)
		);

		_logger.LogInformation("Bot starting.");
		await _client.LoginAsync(TokenType.Bot, token);
		await _client.StartAsync();
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		await _client.LogoutAsync();
		await _client.StopAsync();
		_client.Dispose();
	}

	private Task Log(LogMessage msg)
	{
		_logger.LogInformation(msg.ToString());
		return Task.CompletedTask;
	}

	private async Task GotAMessage(SocketMessage message)
	{
		using (LogContext.Push(
			 new PropertyEnricher("Author", message.Author.ToString()),
			 new PropertyEnricher("Content", message.Content),
			 new PropertyEnricher("Channel", message.Channel.ToString()),
			 new PropertyEnricher("Guild", (message.Author as IGuildUser)?.Guild.ToString())
		))
		{
			try
			{
				if (message.Author.Id == _client.CurrentUser.Id) return;

				if (_authChannelNames.Contains(message.Channel.Name))
				{
					var trimmed = message.Content.Trim();
					if (trimmed.StartsWith("!verify-help"))
					{
						await message.Channel.SendMessageAsync("I can do the following things:\r\n`!verify` - Begin the verification process.\r\n`!verify-help` - Show this message.");
						_logger.LogInformation("Sent help message.");
					}
					// Better thing to do would be get the role for @ShackMe and use that
					// But at this point I'm not trying very hard to make this robust or maintainable so we'll just do it the easy way. HARD CODE!
					else if (trimmed.StartsWith("!verify") || message.MentionedRoles.Any(r => r.Name == "ShackMe"))
					{
						try
						{
							await SendUserNameMessage(message);
						}
						catch (Exception ex)
						{
							var guid = _logger.LogErrorWithGuid(ex, "Error sending initiation message.");
							await message.Channel.SendMessageAsync($"Sorry, <@{message.Author.Id}>.  I can't start an auth session with you for some reason.  Make sure you don't have direct messaging blocked for this server and try again.\r\nIf you continue to have problems contact an admin with the following error code `{guid}`.");
							throw;
						}
						return;
					}
				}
				else if (message.Channel.Name.StartsWith('@'))
				{
					var session = _auth.GetVerificationRequest(message.Author);
					if (session != null)
					{
						//using (LogContext.Push(new PropertyEnricher("VerificationSession", session, true)))
						//{
						if (session.SessionState == AuthSessionState.NeedUser)
						{
							await SendTokenMessage(message);
							_logger.LogInformation("Direct message.");
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
								_logger.LogInformation("Token didn't match {VerificationSession}", session);
							}
						}
						//}
					}
					else
					{
						if (message.ToString().Equals("!version"))
						{
							await message.Channel.SendMessageAsync(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
							_logger.LogInformation("Sent version response.");
						}
						else
						{
							await message.Channel.SendMessageAsync($"I cannot handle requests directly. Use the `!verify` command on the server you want to verify your account with.\r\nIf you started a authentication session and are seeing this message, your token may have timed out.");
							_logger.LogInformation("Unhandled message.");
						}
					}
				}
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error handling message.");
			}
		}
	}

	private async Task SendUserNameMessage(SocketMessage message)
	{
		try
		{
			_auth.CreateAuthSession(message);
			await message.Author.SendMessageAsync($"Starting verification session. This proces must be completed within 10 minutes.\r\n\r\nWhat is your shacknews username?");
			_logger.LogInformation($"Starting auth session.");
			return;
		}
		catch (Exception e)
		{
			if (_addReactions)
			{
				var originalRequest = _auth.GetVerificationRequest(message.Author);
				await originalRequest.Message.AddReactionAsync(new Emoji("⚠️"));
			}
			_auth.ClearSessionForUser(message.Author);
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
			if (_addReactions)
			{
				var originalRequest = _auth.GetVerificationRequest(message.Author);
				await originalRequest.Message.AddReactionAsync(new Emoji("⚠️"));
			}
			await SendErrorMessage(e, message.Author, "Unable to verify, please try again later.");
		}
	}

	private async Task ProcessValidVerification(SocketMessage message, VerificationRequest request)
	{
		try
		{
			var guildUser = request.Message.Author as IGuildUser;
			var guild = guildUser.Guild;
			var rolesToAssign = guild.Roles.Where(r => _rolesToAssign.Contains(r.Name));
			var rolesToUnassign = guild.Roles.Where(r => _rolesToUnasign.Contains(r.Name));
			_logger.LogInformation("Assigning roles {RolesToAssign} {RolesToUnassign}", rolesToAssign, rolesToUnassign);
			await guildUser.AddRolesAsync(rolesToAssign);
			await guildUser.RemoveRolesAsync(rolesToUnassign);
			if (_addReactions) await request.Message.AddReactionAsync(new Emoji("✅"));
			await message.Channel.SendMessageAsync($"Verification succeded!");
			_logger.LogInformation($"Verification success.");
		}
		catch (Exception e)
		{
			if (_addReactions) await request.Message.AddReactionAsync(new Emoji("⚠️"));
			await SendErrorMessage(e, message.Author, "Unable to verify, please try again later.");
		}
	}

	private async Task SendErrorMessage(Exception ex, SocketUser user, string message)
	{
		var guid = _logger.LogErrorWithGuid(ex, message);
		await user.SendMessageAsync($"{message}\r\nContact an admin with the following error code for more info `{guid}`");
	}
}