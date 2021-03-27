using System;
using Discord;

namespace shacknews_discord_auth_bot
{
    public class VerificationRequest
    {
        public IGuildUser User { get; private set; }
        public string Token { get; private set; }
        public string ShackUserName { get; set; }
        public AuthSessionState SessionState { get; set; }

        public VerificationRequest(IGuildUser user)
        {
            User = user;
            Token = Guid.NewGuid().ToString().Substring(0, 6);
            SessionState = AuthSessionState.NeedUser;
        }
    }
}