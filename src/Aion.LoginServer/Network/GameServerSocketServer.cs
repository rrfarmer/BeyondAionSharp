using System.Net.Sockets;
using Aion.Commons.Network.Server;
using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network;

public sealed class GameServerSocketServer : BaseSocketServer
{
	private readonly IGameServerRegistry _registry;
	private readonly ILoginSessionRegistry _sessionRegistry;
	private readonly IAccountRepository _accountRepository;
	private readonly IPremiumRepository _premiumRepository;
	private readonly ILoginAuthService _authService;
	private long _nextClientId;

	public GameServerSocketServer(
		ILogger<GameServerSocketServer> logger,
		LoginServerOptions options,
		IGameServerRegistry registry,
		ILoginSessionRegistry sessionRegistry,
		IAccountRepository accountRepository,
		IPremiumRepository premiumRepository,
		ILoginAuthService authService)
		: base(logger, "Aion GameServer Bridge", options.GameServerEndPoint.Address, options.GameServerEndPoint.Port, options.MaxGameServerConnections)
	{
		_registry = registry;
		_sessionRegistry = sessionRegistry;
		_accountRepository = accountRepository;
		_premiumRepository = premiumRepository;
		_authService = authService;
	}

	protected override async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		var clientId = $"game-server-{Interlocked.Increment(ref _nextClientId)}";
		await using var connection = new GameServerConnection(_logger, client, clientId, _registry, _sessionRegistry, _accountRepository, _premiumRepository, _authService);
		await connection.RunAsync();
		ConnectionClosed();
	}
}
