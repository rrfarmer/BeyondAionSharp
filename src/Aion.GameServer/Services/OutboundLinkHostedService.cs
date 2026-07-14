using Aion.GameServer.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class OutboundLinkHostedService : IHostedService
{
	private readonly Aion.GameServer.Network.LoginServer.LoginServer _loginServer;
	private readonly Aion.GameServer.Network.ChatServer.ChatServer _chatServer;
	private readonly GameServerOptions _options;
	private readonly ILogger<OutboundLinkHostedService> _logger;
	private Task? _loginConnectTask;
	private Task? _chatConnectTask;
	private CancellationTokenSource? _lifetimeTokenSource;

	public OutboundLinkHostedService(
		Aion.GameServer.Network.LoginServer.LoginServer loginServer,
		Aion.GameServer.Network.ChatServer.ChatServer chatServer,
		GameServerOptions options,
		ILogger<OutboundLinkHostedService> logger)
	{
		_loginServer = loginServer;
		_chatServer = chatServer;
		_options = options;
		_logger = logger;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		// The connectors contain the Java-style retry supervisors. Give them a dedicated hosted-service
		// lifetime token so a failed startup connection remains live until StopAsync, not just one attempt.
		_lifetimeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_loginConnectTask = ConnectLoginServerAsync(_lifetimeTokenSource.Token);
		if (_options.Core.EnableChatServer)
			_chatConnectTask = ConnectChatServerAsync(_lifetimeTokenSource.Token);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		// Java parity: bridge shutdown during game-server stop.
		_lifetimeTokenSource?.Cancel();
		await Task.WhenAll(_loginServer.StopAsync(), _chatServer.StopAsync());

		var tasks = new[] { _loginConnectTask, _chatConnectTask }.Where(task => task != null).Cast<Task>().ToArray();
		if (tasks.Length > 0)
			await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
		_lifetimeTokenSource?.Dispose();
		_lifetimeTokenSource = null;
	}

	private async Task ConnectLoginServerAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _loginServer.StartAsync(cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Login-server bridge connection failed; game server will keep running for infrastructure validation");
		}
	}

	private async Task ConnectChatServerAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _chatServer.StartAsync(cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Chat-server bridge connection failed; game server will keep running for infrastructure validation");
		}
	}
}
