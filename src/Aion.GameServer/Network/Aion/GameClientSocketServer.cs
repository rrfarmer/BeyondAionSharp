using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network.Server;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Network.Aion;

public sealed class GameClientSocketServer : BaseSocketServer, IGameClientConnectionRegistry
{
	private readonly GamePacketProcessor<string> _packetProcessor;
	private readonly GameServerOptions _options;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly GameLoginServer? _loginServer;
	private readonly ICharacterSelectionRepository _characterSelectionRepository;
	private readonly CharacterCreationService? _characterCreationService;
	private readonly PlayerEnterWorldService? _playerEnterWorldService;
	private readonly IMailRepository? _mailRepository;
	private readonly IBrokerRepository? _brokerRepository;
	private readonly IDFactory? _idFactory;
	private readonly GameTimeService? _gameTimeService;
	private readonly GameWorld? _world;
	private readonly ConcurrentDictionary<string, GameServerConnection> _connections = new();
	private readonly ConcurrentDictionary<int, GameServerConnection> _playerConnections = new();
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
		IMailRepository? mailRepository = null,
		IBrokerRepository? brokerRepository = null,
		IDFactory? idFactory = null,
		GameTimeService? gameTimeService = null,
		GameWorld? world = null)
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
		_mailRepository = mailRepository;
		_brokerRepository = brokerRepository;
		_idFactory = idFactory;
		_gameTimeService = gameTimeService;
		_world = world;
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
				_mailRepository,
				_brokerRepository,
				this,
				_idFactory,
				_gameTimeService,
				_world);
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

	public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
	{
		// Java parity: world/World player lookup used by SystemMailService.updateRecipientMailbox.
		_playerConnections[playerObjectId] = connection;
	}

	public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
	{
		// Java parity: online player leaves World lookup on disconnect/logout.
		if (_playerConnections.TryGetValue(playerObjectId, out var registeredConnection)
			&& ReferenceEquals(registeredConnection, connection))
			_playerConnections.TryRemove(playerObjectId, out _);
	}

	public async Task<int> BroadcastToVisiblePlayersAsync(WorldPosition sourcePosition, int sourceObjectId, GameServerPacket packet, bool includeSourcePlayer = false)
	{
		// Java parity: utils/PacketSendUtility.broadcastToSightedPlayers using KnownList.sees(object).
		var sent = 0;
		foreach (var connection in _playerConnections.Values)
		{
			var player = connection.ActivePlayer;
			if (player == null)
				continue;
			if (!includeSourcePlayer && player.ObjectId == sourceObjectId)
				continue;
			if (!WorldVisibility.IsVisibleTo(player, sourcePosition))
				continue;

			await connection.SendPacketAsync(packet);
			sent++;
		}

		return sent;
	}

	public async Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
	{
		// Java parity: services/mail/SystemMailService.updateRecipientMailbox online recipient branch.
		if (!_playerConnections.TryGetValue(recipientObjectId, out var connection) || connection.ActivePlayer == null)
			return false;

		var recipient = connection.ActivePlayer;
		recipient.Mailbox = recipient.Mailbox.Concat([mail]).ToArray();
		await connection.SendPacketAsync(new SmMailService(recipient.Mailbox));

		if (recipient.MailboxState != Player.MailboxClosedState)
		{
			var expressOnly = (recipient.MailboxState & Player.MailboxExpressState) == Player.MailboxExpressState;
			foreach (var mailListPacket in SmMailService.CreateListPackets(recipient.ObjectId, recipient.Mailbox, expressOnly))
				await connection.SendPacketAsync(mailListPacket);
		}

		if (mail.LetterType == 1)
			await connection.SendPacketAsync(SmSystemMessage.PostmanNotify());
		return true;
	}

	public async Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
	{
		// Java parity: services/BrokerService.putToSettled online seller notification.
		if (!_playerConnections.TryGetValue(sellerObjectId, out var connection) || connection.ActivePlayer == null)
			return false;

		connection.ActivePlayer.BrokerSettlements = connection.ActivePlayer.BrokerSettlements with { EarnedKinah = settledKinah };
		await connection.SendPacketAsync(new SmBrokerService(settledKinah));
		return true;
	}

}
