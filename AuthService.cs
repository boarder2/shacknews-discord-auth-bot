using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace shacknews_discord_auth_bot
{
    public class AuthService
    {
        private HttpClient _httpClient;
        private IConfiguration _configuration;
        private ILogger<AuthService> _logger;
        private MemoryCache _cache = MemoryCache.Default;
        public AuthService(HttpClient httpClient, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task CreateAuthToken(IGuildUser user, string shackUserName)
        {
            var request = new VerificationRequest(user);
            var kvp = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("to", shackUserName),
                new KeyValuePair<string, string>("subject", "Discord Verification"),
                new KeyValuePair<string, string>("body", $"Your discord verification token is: {request.Token}\r\n\r\nIf you did not reqeust this, you can ignore this message.")
            };
            var response = await _httpClient.SendWithAuth(_configuration, new Uri("https://winchatty.com/v2/sendMessage"), kvp, true);
            if (response.IsSuccessStatusCode)
            {
                _cache.Set(new CacheItem(user.Username, request), new CacheItemPolicy() { AbsoluteExpiration = DateTime.Now.AddMinutes(5) });
                _logger.LogInformation($"Verfication token {request.Token} sent to {shackUserName} for {user.Username} verification");
            }
            else
            {
                var message = $"Error sending SM to {shackUserName} for {user.Username} verification.{Environment.NewLine}Status:{response.StatusCode}{Environment.NewLine}Message:{await response.Content.ReadAsStringAsync()}";
                _logger.LogError(message);
                throw new Exception(message);
            }
        }

        public bool MatchTokenAndRemove(SocketUser user, string token, out VerificationRequest request)
        {
            var cacheToken = _cache.GetCacheItem(user.Username);
            if (cacheToken != null)
            {
                _cache.Remove(user.Username);
                request = (VerificationRequest)cacheToken.Value;
                return (request.Token.ToString()).Equals(token.Trim());
            }
            request = null;
            return false;
        }
    }
}