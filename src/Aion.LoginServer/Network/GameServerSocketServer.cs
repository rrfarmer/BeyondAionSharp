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
	private readonly IAccountTimeRepository _accountTimeRepository;
	private readonly IBannedIpRepository _bannedIpRepository;
	private readonly IPremiumRepository _premiumRepository;
	private readonly IAccountsLogRepository _accountsLogRepository;
	private readonly ILoginAuthService _authService;
	private readonly IBannedMacService _bannedMacService;
	private readonly IBannedHddService _bannedHddService;
	private readonly IPlayerTransferService _playerTransferService;
	private readonly LoginServerOptions _options;
	private long _nextClientId;

	public GameServerSocketServer(
		ILogger<GameServerSocketServer> logger,
		LoginServerOptions options,
		IGameServerRegistry registry,
		ILoginSessionRegistry sessionRegistry,
		IAccountRepository accountRepository,
		IAccountTimeRepository accountTimeRepository,
		IBannedIpRepository bannedIpRepository,
		IPremiumRepository premiumRepository,
		IAccountsLogRepository accountsLogRepository,
		ILoginAuthService authService,
		IBannedMacService bannedMacService,
		IBannedHddService bannedHddService,
		IPlayerTransferService playerTransferService)
		: base(logger, "Aion GameServer Bridge", options.GameServerEndPoint.Address, options.GameServerEndPoint.Port, options.MaxGameServerConnections)
	{
		_registry = registry;
		_sessionRegistry = sessionRegistry;
		_accountRepository = accountRepository;
		_accountTimeRepository = accountTimeRepository;
		_bannedIpRepository = bannedIpRepository;
		_premiumRepository = premiumRepository;
		_accountsLogRepository = accountsLogRepository;
		_authService = authService;
		_bannedMacService = bannedMacService;
		_bannedHddService = bannedHddService;
		_playerTransferService = playerTransferService;
		_options = options;
	}

	protected override async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		var clientId = $"game-server-{Interlocked.Increment(ref _nextClientId)}";
		await using var connection = new GameServerConnection(
			_logger,
			client,
			clientId,
			_registry,
			_sessionRegistry,
			_accountRepository,
			_accountTimeRepository,
			_bannedIpRepository,
			_premiumRepository,
			_accountsLogRepository,
			_authService,
			_bannedMacService,
			_bannedHddService,
			_playerTransferService,
			_options);
		await connection.RunAsync();
		ConnectionClosed();
	}
}
