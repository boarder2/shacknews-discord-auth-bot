using System.Runtime.Caching;

namespace shacknews_discord_auth_bot;

public class AuthService(HttpClient _httpClient, IConfiguration _configuration, ILogger<AuthService> _logger)
{
	private readonly MemoryCache _cache = MemoryCache.Default;

	public void CreateAuthSession(SocketMessage message)
	{
		var request = new VerificationRequest(message);
		_cache.Set(new CacheItem(message.Author.Username, request), new CacheItemPolicy() { AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(10) });
	}

	public VerificationRequest GetVerificationRequest(SocketUser user)
	{
		var cacheToken = _cache.GetCacheItem(user.Username);
		var req = cacheToken?.Value as VerificationRequest;
		if (req == null)
		{
			_logger.LogWarning("Session not found for {userName}", user.Username);
		}
		else
		{
			_logger.LogInformation("Session found for {userName} with {token}", user.Username, req.Token);
		}

		return req;
	}

	public async Task SetAuthSessionShackNameAndSendSM(SocketUser user, string shackUserName)
	{
		var request = GetVerificationRequest(user);
		var kvp = new List<KeyValuePair<string, string>> {
				new ("to", shackUserName),
				new ("subject", "Discord Verification"),
				new ("body", $"Your discord verification token is: {request.Token}\r\n\r\nIf you did not reqeust this, you can ignore this message.")
			};
		var response = await _httpClient.SendWithAuth(_configuration, new Uri("https://winchatty.com/v2/sendMessage"), kvp, true);
		if (response.IsSuccessStatusCode)
		{
			request.SessionState = AuthSessionState.NeedToken;
			_logger.LogInformation("Verfication token sent with request {VerificationRequest}", request);
		}
		else
		{
			_logger.LogError("Error sending SM with {VerificationRequest} and {SMResponse}", request, response);
			throw new Exception("Error sending shack message");
		}
	}

	public bool MatchTokenAndRemove(SocketUser user, string token, out VerificationRequest request)
	{
		var cacheToken = _cache.GetCacheItem(user.Username);
		if (cacheToken != null)
		{
			request = (VerificationRequest)cacheToken.Value;
			var matched = request.Token.ToUpper().Equals(token.ToUpper().Trim());
			if (matched)
			{
				_cache.Remove(user.Username);
			}
			return matched;
		}
		request = null;
		return false;
	}

	public void ClearSessionForUser(SocketUser user)
	{
		_cache.Remove(user.Username);
	}
}