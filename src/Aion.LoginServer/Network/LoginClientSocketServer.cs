using System.Collections.Concurrent;
using System.Net.Sockets;
using Aion.Commons.Network.Server;
using Aion.LoginServer.Configuration;
using Aion.LoginServer.Network.Crypto;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network;

public sealed class LoginClientSocketServer : BaseSocketServer
{
	private readonly ILoginKeyGenerator _keyGenerator;
	private readonly ILoginAuthService _authService;
	private readonly ILoginSessionRegistry _sessionRegistry;
	private readonly IGameServerRegistry _gameServerRegistry;
	private readonly ConcurrentDictionary<string, LoginClientConnection> _connections = new();
	private long _nextClientId;

	public LoginClientSocketServer(
		ILogger<LoginClientSocketServer> logger,
		LoginServerOptions options,
		ILoginKeyGenerator keyGenerator,
		ILoginAuthService authService,
		ILoginSessionRegistry sessionRegistry,
		IGameServerRegistry gameServerRegistry)
		: base(logger, "Aion Login Client Server", options.ClientEndPoint.Address, options.ClientEndPoint.Port, options.MaxClientConnections)
	{
		_keyGenerator = keyGenerator;
		_authService = authService;
		_sessionRegistry = sessionRegistry;
		_gameServerRegistry = gameServerRegistry;
	}

	protected override async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		var clientId = $"login-client-{Interlocked.Increment(ref _nextClientId)}";
		LoginClientConnection? connection = null;
		try
		{
			connection = new LoginClientConnection(_logger, client, clientId, _keyGenerator, _authService, _sessionRegistry, _gameServerRegistry);
			_connections[clientId] = connection;
			await connection.RunAsync();
		}
		finally
		{
			if (connection != null)
			{
				_connections.TryRemove(clientId, out _);
				await connection.DisposeAsync();
			}

			ConnectionClosed();
		}
	}

	protected override Task CloseActiveConnectionsAsync()
	{
		var closeTasks = _connections.Values.Select(connection => connection.CloseAsync()).ToArray();
		return closeTasks.Length == 0 ? Task.CompletedTask : Task.WhenAll(closeTasks);
	}
}
