using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network.Server;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using GameChatServer = Aion.GameServer.Network.ChatServer.ChatServer;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Network.Aion;

public sealed class GameClientSocketServer : BaseSocketServer, IGameClientConnectionRegistry
{
	private readonly GamePacketProcessor<string> _packetProcessor;
	private readonly GameServerOptions _options;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly GameLoginServer? _loginServer;
	private readonly GameChatServer? _chatServer;
	private readonly ICharacterSelectionRepository _characterSelectionRepository;
	private readonly CharacterCreationService? _characterCreationService;
	private readonly PlayerEnterWorldService? _playerEnterWorldService;
	private readonly IMailRepository? _mailRepository;
	private readonly IBrokerRepository? _brokerRepository;
	private readonly ISocialRepository? _socialRepository;
	private readonly IHouseAuctionRepository? _houseAuctionRepository;
	private readonly IHousingRepository? _housingRepository;
	private readonly HouseAuctionTimingService? _houseAuctionTiming;
	private readonly HouseMaintenanceTimingService? _houseMaintenanceTiming;
	private readonly IMotionRepository? _motionRepository;
	private readonly ExpirableTaskService? _expirableTaskService;
	private readonly HousingVisibilityService _housingVisibilityService;
	private readonly IDFactory? _idFactory;
	private readonly GameTimeService? _gameTimeService;
	private readonly ThreadPoolManager? _threadPoolManager;
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
		GameChatServer? chatServer = null,
		ICharacterSelectionRepository? characterSelectionRepository = null,
		CharacterCreationService? characterCreationService = null,
		PlayerEnterWorldService? playerEnterWorldService = null,
		IMailRepository? mailRepository = null,
		IBrokerRepository? brokerRepository = null,
		ISocialRepository? socialRepository = null,
		IHouseAuctionRepository? houseAuctionRepository = null,
		IHousingRepository? housingRepository = null,
		HouseAuctionTimingService? houseAuctionTiming = null,
		HouseMaintenanceTimingService? houseMaintenanceTiming = null,
		IMotionRepository? motionRepository = null,
		ExpirableTaskService? expirableTaskService = null,
		HousingVisibilityService? housingVisibilityService = null,
		IDFactory? idFactory = null,
		GameTimeService? gameTimeService = null,
		ThreadPoolManager? threadPoolManager = null,
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
		_chatServer = chatServer;
		_characterSelectionRepository = characterSelectionRepository ?? new EmptyCharacterSelectionRepository();
		_characterCreationService = characterCreationService;
		_playerEnterWorldService = playerEnterWorldService;
		_mailRepository = mailRepository;
		_brokerRepository = brokerRepository;
		_socialRepository = socialRepository;
		_houseAuctionRepository = houseAuctionRepository;
		_housingRepository = housingRepository;
		_houseAuctionTiming = houseAuctionTiming;
		_houseMaintenanceTiming = houseMaintenanceTiming;
		_motionRepository = motionRepository;
		_expirableTaskService = expirableTaskService;
		_housingVisibilityService = housingVisibilityService ?? new HousingVisibilityService(options);
		_idFactory = idFactory;
		_gameTimeService = gameTimeService;
		_gameTimeService?.SetWorldBroadcaster((packet, _) => BroadcastToWorldAsync(packet));
		_threadPoolManager = threadPoolManager;
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
				_chatServer,
				_characterSelectionRepository,
				_characterCreationService,
				_playerEnterWorldService,
				_mailRepository,
				_brokerRepository,
				_socialRepository,
				_houseAuctionRepository,
				_housingRepository,
				_houseAuctionTiming,
				_houseMaintenanceTiming,
				_motionRepository,
				_expirableTaskService,
				this,
				_idFactory,
				_gameTimeService,
				_world,
				threadPoolManager: _threadPoolManager);
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
		{
			_playerConnections.TryRemove(playerObjectId, out _);
			_housingVisibilityService.ClearKnownHouses(playerObjectId);
		}
	}

	public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
	{
		// Java parity: world/World.getPlayer(String) lookup used by whisper and social packets.
		foreach (var connection in _playerConnections.Values)
		{
			player = connection.ActivePlayer;
			if (player != null && string.Equals(player.Name, playerName, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		player = null;
		return false;
	}

	public async Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
	{
		// Java parity: PacketSendUtility.sendPacket(targetPlayer, packet).
		if (!_playerConnections.TryGetValue(playerObjectId, out var connection) || connection.ActivePlayer == null)
			return false;

		await connection.SendPacketAsync(packet);
		return true;
	}

	public async Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
	{
		// Java parity: utils/PacketSendUtility.broadcastToWorld.
		var sent = 0;
		foreach (var connection in _playerConnections.Values)
		{
			var player = connection.ActivePlayer;
			if (player == null)
				continue;
			if (filter != null && !filter(player))
				continue;

			await connection.SendPacketAsync(packet);
			sent++;
		}

		return sent;
	}

	public async Task<int> BroadcastToVisiblePlayersAsync(
		WorldPosition sourcePosition,
		int sourceObjectId,
		GameServerPacket packet,
		bool includeSourcePlayer = false,
		Func<Player, bool>? filter = null)
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
			if (filter != null && !filter(player))
				continue;

			await connection.SendPacketAsync(packet);
			sent++;
		}

		return sent;
	}

	public async Task<int> RefreshHousingVisibilityAsync(
		IReadOnlyList<WorldHouse> houses,
		HousingTemplateTable? housingTemplates,
		int? playerObjectId = null)
	{
		// Java parity: PlayerController.see/notSee sends SM_HOUSE_RENDER/SM_DELETE_HOUSE from KnownList deltas.
		var sent = 0;
		foreach (var pair in _playerConnections)
		{
			if (playerObjectId != null && pair.Key != playerObjectId.Value)
				continue;
			var connection = pair.Value;
			var player = connection.ActivePlayer;
			if (player == null)
				continue;

			var delta = _housingVisibilityService.UpdateKnownHouses(player, houses);
			foreach (var house in delta.Appeared)
			{
				await connection.SendPacketAsync(new SmHouseRender(house, housingTemplates));
				sent++;
				foreach (var obj in GetVisibleHouseObjects(house))
				{
					await connection.SendPacketAsync(new SmHouseObject(obj));
					sent++;
				}
			}

			foreach (var addressId in delta.DisappearedAddressIds)
			{
				var house = houses.FirstOrDefault(house => house.AddressId == addressId);
				if (house != null)
				{
					foreach (var obj in GetVisibleHouseObjects(house))
					{
						await connection.SendPacketAsync(new SmDeleteHouseObject(obj.ObjectId));
						sent++;
					}
				}

				await connection.SendPacketAsync(new SmDeleteHouse(addressId));
				sent++;
			}
		}

		return sent;
	}

	private static IReadOnlyList<PlacedHouseObjectSummary> GetVisibleHouseObjects(WorldHouse house)
	{
		// Java parity: controllers/HouseController.spawnObjects skips inactive houses and exposes spawned registry objects.
		if (house.IsInactive || house.Registry == null)
			return Array.Empty<PlacedHouseObjectSummary>();

		return house.Registry.GetSpawnedObjects(house);
	}

	public async Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
	{
		// Java parity: HouseController.updateAppearance broadcasts SM_HOUSE_UPDATE to known players.
		var sent = 0;
		foreach (var connection in _playerConnections.Values)
		{
			var player = connection.ActivePlayer;
			if (player == null || !_housingVisibilityService.IsVisibleTo(player, house))
				continue;

			await connection.SendPacketAsync(new SmHouseUpdate(house, housingTemplates));
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
