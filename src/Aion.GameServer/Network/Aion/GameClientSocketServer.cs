using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network.Server;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;

namespace Aion.GameServer.Network.Aion;

public sealed class GameClientSocketServer : BaseSocketServer
{
	private readonly GamePacketProcessor<string> _packetProcessor;
	private readonly GameServerOptions _options;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly GameLoginServer? _loginServer;
	private readonly ICharacterSelectionRepository _characterSelectionRepository;
	private readonly CharacterCreationService? _characterCreationService;
	private readonly PlayerEnterWorldService? _playerEnterWorldService;
	private readonly IDFactory? _idFactory;
	private readonly GameTimeService? _gameTimeService;
	private readonly ConcurrentDictionary<string, GameServerConnection> _connections = new();
	private long _nextClientId;

	public GameClientSocketServer(
		ILogger<GameClientSocketServer> logger,
		GameServerOptions options,
		GamePacketProcessor<string> packetProcessor,
		GameServerRuntimeContext? runtimeContext = null,
		GameLoginServer? loginServer = null,
		ICharacterSelectionRepository? characterSelectionRepository = null,
		CharacterCreationService? characterCreationService = null,
		PlayerEnterWorldService? playerEnterWorldService = null,
		IDFactory? idFactory = null,
		GameTimeService? gameTimeService = null)
		: base(
			logger,
			"Aion Game Client Server",
			options.Network.ClientEndPoint.Address,
			options.Network.ClientEndPoint.Port,
			Math.Max(1, options.Network.MaxOnlinePlayers))
	{
		_packetProcessor = packetProcessor;
		_options = options;
		_runtimeContext = runtimeContext;
		_loginServer = loginServer;
		_characterSelectionRepository = characterSelectionRepository ?? new EmptyCharacterSelectionRepository();
		_characterCreationService = characterCreationService;
		_playerEnterWorldService = playerEnterWorldService;
		_idFactory = idFactory;
		_gameTimeService = gameTimeService;
	}

	public IPEndPoint? LocalEndPoint => _listener?.LocalEndpoint as IPEndPoint;

	protected override async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		// Java parity: network/aion/GameConnectionListener accepts clients and creates AionConnection.
		var clientId = $"game-client-{Interlocked.Increment(ref _nextClientId)}";
		GameServerConnection? connection = null;
		try
		{
			connection = new GameServerConnection(
				_logger,
				client,
				clientId,
				_packetProcessor,
				_options,
				_runtimeContext,
				_loginServer,
				_characterSelectionRepository,
				_characterCreationService,
				_playerEnterWorldService,
				_idFactory,
				_gameTimeService);
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
		// Java parity: listener shutdown closes active AionConnection sessions.
		var closeTasks = _connections.Values.Select(connection => connection.CloseAsync()).ToArray();
		return closeTasks.Length == 0 ? Task.CompletedTask : Task.WhenAll(closeTasks);
	}
}
