using System;
using Discord;

namespace shacknews_discord_auth_bot
{
    public class VerificationRequest
    {
        public IGuildUser User { get; private set; }
        public Guid Token { get; private set; }

        public VerificationRequest(IGuildUser user)
        {
            User = user;
            Token = Guid.NewGuid();
        }
    }
}