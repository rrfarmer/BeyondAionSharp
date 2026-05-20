using Aion.GameServer.Network.Aion;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class GameServerHostedService : IHostedService
{
	private readonly GameClientSocketServer _clientSocketServer;
	private readonly ILogger<GameServerHostedService> _logger;
	private Task? _clientSocketTask;

	public GameServerHostedService(GameClientSocketServer clientSocketServer, ILogger<GameServerHostedService> logger)
	{
		_clientSocketServer = clientSocketServer;
		_logger = logger;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		// Java parity: network/aion/GameConnectionListener bind after bootstrap.
		_logger.LogInformation("Starting game-server client listener");
		_clientSocketTask = Task.Run(() => _clientSocketServer.StartAsync(), cancellationToken);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		// Java parity: game client listener shutdown on server stop.
		_logger.LogInformation("Stopping game-server client listener");
		await _clientSocketServer.StopAsync();
		if (_clientSocketTask != null)
			await Task.WhenAny(_clientSocketTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
	}
}
