using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace shacknews_discord_auth_bot
{
    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> SendWithAuth(this HttpClient client, IConfiguration configuration, Uri uri, List<KeyValuePair<string, string>> content, bool sendAuth = false)
        {

            var localContent = new List<KeyValuePair<string, string>>(content);
            if (sendAuth)
            {
                localContent.AddRange(new[]
                {
                        new KeyValuePair<string, string>("username", configuration.GetValue<string>("SHACK_DISCORD_AUTH_BOT_MESSAGE_USERNAME")),
                        new KeyValuePair<string, string>("password", configuration.GetValue<string>("SHACK_DISCORD_AUTH_BOT_MESSAGE_PASSWORD"))
                    });
            }

            // //Winchatty seems to crap itself if the Expect: 100-continue header is there.
            // request.DefaultRequestHeaders.ExpectContinue = false;
            // if (!string.IsNullOrWhiteSpace(acceptHeader))
            // {
            //     request.DefaultRequestHeaders.Add("Accept", acceptHeader);
            // }

            var items = localContent.Select(i => WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
            using var formContent = new StringContent(string.Join("&", items), null, "application/x-www-form-urlencoded");

            var response = await client.PostAsync(uri, formContent).ConfigureAwait(false);
            return response;
        }
    }
}