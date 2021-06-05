using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace shacknews_discord_auth_bot
{
    public class AuthService
    {
        private HttpClient _httpClient;
        private IConfiguration _configuration;
        private ILogger _logger;
        private MemoryCache _cache = MemoryCache.Default;
        public AuthService(HttpClient httpClient, IConfiguration configuration, ILogger logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public void CreateAuthSession(IGuildUser user)
        {
            var request = new VerificationRequest(user);
            _cache.Set(new CacheItem(user.Username, request), new CacheItemPolicy() { AbsoluteExpiration = DateTime.Now.AddMinutes(10) });
        }

        public VerificationRequest GetVerificationRequest(SocketUser user)
        {
            var cacheToken = _cache.GetCacheItem(user.Username);
            return cacheToken?.Value as VerificationRequest;
        }

        public async Task SetAuthSessionShackNameAndSendSM(SocketUser user, string shackUserName)
        {
            var request = GetVerificationRequest(user);
            var kvp = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("to", shackUserName),
                new KeyValuePair<string, string>("subject", "Discord Verification"),
                new KeyValuePair<string, string>("body", $"Your discord verification token is: {request.Token}\r\n\r\nIf you did not reqeust this, you can ignore this message.")
            };
            var response = await _httpClient.SendWithAuth(_configuration, new Uri("https://winchatty.com/v2/sendMessage"), kvp, true);
            if (response.IsSuccessStatusCode)
            {
                request.SessionState = AuthSessionState.NeedToken;
                _logger.Information("Verfication token sent with request {VerificationRequest}", request);
            }
            else
            {
                _logger.Error("Error sending SM with {VerificationRequest} and {SMResponse}", request, response);
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
                if(matched)
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
}