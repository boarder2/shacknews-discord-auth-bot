using System;
using Serilog;

namespace shacknews_discord_auth_bot
{
	public static class LoggerExtensions
	{
		public static Guid LogErrorWithGuid(this ILogger logger, Exception exception, string message, params object[] args)
		{
			var guid = Guid.NewGuid();
			logger.Error(exception, $"{{ErrorGuid}} {message}", guid, args);
			return guid;
		}
	}
}