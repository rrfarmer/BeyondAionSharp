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
	private long _nextClientId;

	public LoginClientSocketServer(ILogger<LoginClientSocketServer> logger, LoginServerOptions options, ILoginKeyGenerator keyGenerator, ILoginAuthService authService)
		: base(logger, "Aion Login Client Server", options.ClientEndPoint.Address, options.ClientEndPoint.Port, options.MaxClientConnections)
	{
		_keyGenerator = keyGenerator;
		_authService = authService;
	}

	protected override async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		var clientId = $"login-client-{Interlocked.Increment(ref _nextClientId)}";
		await using var connection = new LoginClientConnection(_logger, client, clientId, _keyGenerator, _authService);
		await connection.RunAsync();
		ConnectionClosed();
	}
}
