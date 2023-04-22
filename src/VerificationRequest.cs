using System;
using Discord;
using Discord.WebSocket;

namespace shacknews_discord_auth_bot
{
    public class VerificationRequest
    {
        public SocketMessage Message { get; private set; }
        public string Token { get; private set; }
        public string ShackUserName { get; set; }
        public AuthSessionState SessionState { get; set; }

        public VerificationRequest(SocketMessage message)
        {
            Message = message;
            Token = Guid.NewGuid().ToString().Substring(0, 6);
            SessionState = AuthSessionState.NeedUser;
        }

		public override string ToString()
		{
			return $"{nameof(Message)}: {Message}\n{nameof(Token)}: {Token}\n{nameof(ShackUserName)}: {ShackUserName}\n{nameof(SessionState)}: {SessionState}";
		}
	
    }
}