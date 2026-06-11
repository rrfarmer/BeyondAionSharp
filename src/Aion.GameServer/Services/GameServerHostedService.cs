using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aion.Commons.Concurrent;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

/// <summary>
/// Boots the faithful NioServer game-client listener (Java parity: GameServer.initNioServer -> NioServer +
/// ServerCfg + GameConnectionFactoryImpl creating AionConnections). Infrastructure boundary: a clean C#
/// IHostedService shell around the faithful reactor (gameplay-faithful / infra-idiomatic principle), with
/// the bind endpoint taken from idiomatic GameServerOptions rather than Java's static NetworkConfig.
/// </summary>
public sealed class GameServerHostedService : IHostedService
{
	private readonly GameServerOptions _options;
	private readonly ILogger<GameServerHostedService> _logger;
	private NioServer? _nioServer;

	public GameServerHostedService(GameServerOptions options, ILogger<GameServerHostedService> logger)
	{
		_options = options;
		_logger = logger;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Starting game-server client listener on {EndPoint}", _options.ClientEndPoint);
		// Java game server is single-threaded for read/write (NIO_READ_WRITE_THREADS must be 1).
		_nioServer = new NioServer(1, new ServerCfg(_options.ClientEndPoint, "Aion game clients", new GameConnectionFactoryImpl()));
		_nioServer.Connect(new ThreadPoolExecutor());
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Stopping game-server client listener");
		_nioServer?.Shutdown();
		return Task.CompletedTask;
	}
}
