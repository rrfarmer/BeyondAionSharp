using Aion.LoginServer.Data;
using Aion.LoginServer.Network;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Services;

public sealed class LoginServerHostedService : IHostedService
{
	private readonly LoginClientSocketServer _loginClientServer;
	private readonly GameServerSocketServer _gameServerSocketServer;
	private readonly IGameServersRepository _gameServersRepository;
	private readonly IGameServerRegistry _gameServerRegistry;
	private readonly ILogger<LoginServerHostedService> _logger;
	private Task? _loginClientTask;
	private Task? _gameServerTask;

	public LoginServerHostedService(
		LoginClientSocketServer loginClientServer,
		GameServerSocketServer gameServerSocketServer,
		IGameServersRepository gameServersRepository,
		IGameServerRegistry gameServerRegistry,
		ILogger<LoginServerHostedService> logger)
	{
		_loginClientServer = loginClientServer;
		_gameServerSocketServer = gameServerSocketServer;
		_gameServersRepository = gameServersRepository;
		_gameServerRegistry = gameServerRegistry;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Starting login-server listeners");
		var gameServers = await _gameServersRepository.GetAllGameServersAsync(cancellationToken);
		foreach (var gameServer in gameServers.Values)
			_gameServerRegistry.RegisterKnownServer(gameServer);
		_logger.LogInformation("Loaded {Count} registered game servers", gameServers.Count);

		_loginClientTask = Task.Run(() => _loginClientServer.StartAsync(), cancellationToken);
		_gameServerTask = Task.Run(() => _gameServerSocketServer.StartAsync(), cancellationToken);
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Stopping login-server listeners");
		await _loginClientServer.StopAsync();
		await _gameServerSocketServer.StopAsync();

		var runningTasks = new[] { _loginClientTask, _gameServerTask }.Where(task => task != null).Cast<Task>().ToArray();
		if (runningTasks.Length > 0)
			await Task.WhenAny(Task.WhenAll(runningTasks), Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
	}
}
