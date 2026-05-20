using Aion.ChatServer.Configuration;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Services;

public sealed class GameServerService : IGameServerService
{
	private readonly ChatServerOptions _options;
	private readonly ILogger<GameServerService> _logger;
	private readonly object _gate = new();

	public GameServerService(ChatServerOptions options, ILogger<GameServerService> logger)
	{
		_options = options;
		_logger = logger;
	}

	public byte? GameServerId { get; private set; }

	public bool IsOnline { get; private set; }

	public GsAuthResponse RegisterGameServer(byte gameServerId, string password)
	{
		lock (_gate)
		{
			if (IsOnline)
				return GsAuthResponse.AlreadyRegistered;

			if (!string.Equals(password, _options.GameServerPassword, StringComparison.Ordinal))
				return GsAuthResponse.NotAuthed;

			IsOnline = true;
			GameServerId = gameServerId;
			_logger.LogInformation("Gameserver #{GameServerId} is now online", gameServerId);
			return GsAuthResponse.Authed;
		}
	}

	public void SetOffline()
	{
		lock (_gate)
		{
			_logger.LogInformation("Gameserver #{GameServerId} is disconnected", GameServerId);
			IsOnline = false;
			GameServerId = null;
		}
	}
}
