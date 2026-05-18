using System.Net.Sockets;
using Aion.Commons.Network.Server;
using Aion.LoginServer.Configuration;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network;

public sealed class LoginClientSocketServer : BaseSocketServer
{
	private long _nextClientId;

	public LoginClientSocketServer(ILogger<LoginClientSocketServer> logger, LoginServerOptions options)
		: base(logger, "Aion Login Client Server", options.ClientEndPoint.Address, options.ClientEndPoint.Port, options.MaxClientConnections)
	{
	}

	protected override async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		var clientId = $"login-client-{Interlocked.Increment(ref _nextClientId)}";
		await using var connection = new LoginClientConnection(_logger, client, clientId);
		await connection.RunAsync();
		ConnectionClosed();
	}
}
