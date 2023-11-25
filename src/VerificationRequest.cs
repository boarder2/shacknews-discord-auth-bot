namespace shacknews_discord_auth_bot;

public class VerificationRequest(SocketMessage message)
{
	public SocketMessage Message { get; private set; } = message;
	public string Token { get; private set; } = Guid.NewGuid().ToString()[..6];
	public string ShackUserName { get; set; }
	public AuthSessionState SessionState { get; set; } = AuthSessionState.NeedUser;

	public override string ToString()
	{
		return $"{nameof(Message)}: {Message}\n{nameof(Token)}: {Token}\n{nameof(ShackUserName)}: {ShackUserName}\n{nameof(SessionState)}: {SessionState}";
	}

}