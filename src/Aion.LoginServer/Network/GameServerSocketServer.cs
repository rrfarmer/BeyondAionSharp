using System.Net.Sockets;
using Aion.Commons.Network.Server;
using Aion.LoginServer.Configuration;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network;

public sealed class GameServerSocketServer : BaseSocketServer
{
	private readonly IGameServerRegistry _registry;
	private long _nextClientId;

	public GameServerSocketServer(ILogger<GameServerSocketServer> logger, LoginServerOptions options, IGameServerRegistry registry)
		: base(logger, "Aion GameServer Bridge", options.GameServerEndPoint.Address, options.GameServerEndPoint.Port, options.MaxGameServerConnections)
	{
		_registry = registry;
	}

	protected override async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		var clientId = $"game-server-{Interlocked.Increment(ref _nextClientId)}";
		await using var connection = new GameServerConnection(_logger, client, clientId, _registry);
		await connection.RunAsync();
		ConnectionClosed();
	}
}
