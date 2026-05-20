using Aion.ChatServer.Network;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Services;

public sealed class ChatServerHostedService : IHostedService
{
	private readonly ClientSocketServer _clientSocketServer;
	private readonly GameServerSocketServer _gameServerSocketServer;
	private readonly ILogger<ChatServerHostedService> _logger;
	private Task? _clientSocketTask;
	private Task? _gameServerTask;

	public ChatServerHostedService(
		ClientSocketServer clientSocketServer,
		GameServerSocketServer gameServerSocketServer,
		ILogger<ChatServerHostedService> logger)
	{
		_clientSocketServer = clientSocketServer;
		_gameServerSocketServer = gameServerSocketServer;
		_logger = logger;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Starting chat-server listeners");
		_clientSocketTask = Task.Run(() => _clientSocketServer.StartAsync(), cancellationToken);
		_gameServerTask = Task.Run(() => _gameServerSocketServer.StartAsync(), cancellationToken);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Stopping chat-server listeners");
		await _clientSocketServer.StopAsync();
		await _gameServerSocketServer.StopAsync();

		var runningTasks = new[] { _clientSocketTask, _gameServerTask }.Where(task => task != null).Cast<Task>().ToArray();
		if (runningTasks.Length > 0)
			await Task.WhenAny(Task.WhenAll(runningTasks), Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
	}
}
