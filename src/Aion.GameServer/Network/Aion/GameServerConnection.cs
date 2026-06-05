using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Aion.GameServer.Configuration;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using AccountAuthResult = Aion.GameServer.Network.LoginServer.AccountAuthResult;
using GameChatServer = Aion.GameServer.Network.ChatServer.ChatServer;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;
using GameWorld = Aion.GameServer.World.World;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Network.Aion;

public sealed class GameServerConnection : BaseClientConnection
{
	private const int MaxCorruptPacketsBeforeDisconnect = 3;
	private const int CubeStorageId = 0;
	private const int BrokerStorageId = 126;
	private const int MailboxStorageId = 127;
	private const int KinahItemId = 182400001;
	private const int FirstAvailableSlot = 65535;
	private const int NoTitleId = 0xFFFF;
	private const int MaxBlockedUsers = 100;
	private const int OpenVendorDialogAction = 33;
	private const string PowerShardItemGroup = "POWER_SHARDS";
	private static readonly TimeSpan ClientPingInterval = TimeSpan.FromMilliseconds(180000);
	private static readonly ConcurrentDictionary<int, int> HouseObjectOccupants = new();
	private readonly GamePacketProcessor<string> _packetProcessor;
	private readonly GameCrypt _crypt;
	private readonly GameServerOptions _options;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly GameLoginServer? _loginServer;
	private readonly GameChatServer? _chatServer;
	private readonly ICharacterSelectionRepository _characterSelectionRepository;
	private readonly CharacterCreationService? _characterCreationService;
	private readonly PlayerEnterWorldService? _playerEnterWorldService;
	private readonly IMailRepository? _mailRepository;
	private readonly IBrokerRepository? _brokerRepository;
	private readonly ISocialRepository _socialRepository;
	private readonly IHouseAuctionRepository _houseAuctionRepository;
	private readonly IHousingRepository _housingRepository;
	private readonly HouseAuctionTimingService _houseAuctionTiming;
	private readonly HouseMaintenanceTimingService _houseMaintenanceTiming;
	private readonly IMotionRepository _motionRepository;
	private readonly ExpirableTaskService? _expirableTaskService;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly IDFactory? _idFactory;
	private readonly GameTimeService? _gameTimeService;
	private readonly GameWorld? _world;
	private readonly ThreadPoolManager? _threadPoolManager;
	private readonly IHouseDoorStateService? _houseDoorStateService;
	private readonly Action<GameServerPacket>? _sentPacketObserver;
	private readonly Action<QuestDialogNpcTargetBranchInputAssemblyPlan>? _dialogSelectPlanObserver;
	private readonly RiftPortalInteractionService? _riftPortalInteractionService;
	private readonly PortalEntryInteractionService? _portalEntryInteractionService;
	private readonly WorldNpcLootService? _worldNpcLootService;
	private readonly WorldNpcSpawnService? _worldNpcSpawnService;
	private readonly InstanceEmptyInstanceCheckerService? _emptyInstanceCheckerService;
	private readonly Func<Player, int, bool>? _isKnownNpc;
	private readonly CreaturePvpZoneCounterService? _creaturePvpZoneCounterService;
	private readonly PlayerGroupRuntime _playerGroupRuntime;
	private readonly PlayerAllianceRuntime _playerAllianceRuntime;
	private readonly AutoGroupInstanceLeaveRuntimeService _autoGroupInstanceLeaveRuntimeService;
	private readonly AutoGroupLookingPartyRegistrationService _autoGroupLookingPartyRegistrations;
	private readonly AutoGroupPenaltyRefreshSchedulerService? _autoGroupPenaltyRefreshScheduler;
	private readonly PeriodicInstanceRegistrationService _periodicInstanceRegistrations;
	private readonly PlayerLeagueRuntime _playerLeagueRuntime;
	private readonly PlayerGroupInviteRequestService _playerGroupInviteRequestService;
	private readonly PlayerAllianceInviteRequestService _playerAllianceInviteRequestService;
	private readonly PlayerDuelRequestService _playerDuelRequestService;
	private readonly PlayerExchangeRequestService _playerExchangeRequestService;
	private readonly PlayerAllianceGroupChangeServicePlanner _playerAllianceGroupChangeServicePlanner;
	private readonly PlayerShowBrandCommandPlanner _showBrandCommandPlanner;
	private readonly PlayerCastSpellEarlyExitService _castSpellEarlyExitService;
	private readonly GameServerCastSpellHandlerHooks _castSpellHooks;
	private readonly Func<bool> _isShuttingDownSoon;
	private readonly Action<CmCraftRuntimePlan>? _cmCraftRuntimePlanObserver;
	private readonly Action<CmCraftStartCompositionPlan>? _cmCraftStartCompositionPlanObserver;
	private readonly Action<CmBuyItemHandlerCompositionPlan>? _cmBuyItemHandlerCompositionPlanObserver;
	private readonly Action<CmBuyItemSideEffectOutcomePlan>? _cmBuyItemSideEffectOutcomePlanObserver;
	private readonly Action<PrivateStoreCreatePlan>? _privateStoreCreatePlanObserver;
	private readonly Action<PrivateStoreNameOpenCompositionPlan>? _privateStoreNameOpenCompositionPlanObserver;
	private readonly Action<GroupDataExchangeHandlerCompositionPlan>? _groupDataExchangeHandlerCompositionPlanObserver;
	private readonly Action<VortexDefenderInvitationResponseConsumptionReport>? _vortexDefenderInvitationResponseObserver;
	private readonly Action<VortexDefenderAcceptanceRuntimeObserverReport>? _vortexDefenderAcceptanceObserver;
	private readonly VortexInvasionRuntime? _vortexInvasionRuntime;
	private readonly Func<int, Player?>? _worldPlayerLookup;
	private readonly VortexLocationService? _defenderAcceptanceVortexLocationService;
	private readonly FindGroupConnectionClientActionCompositionPlanService? _findGroupConnectionClientActionCompositionPlanService;
	private readonly FindGroupConnectionBoundaryDispatchAdapterService? _findGroupConnectionBoundaryDispatchAdapterService;
	private readonly Func<Player, int, object?, bool?>? _buyItemKnownObjectResolver;
	private readonly TradeListTable? _buyItemTradeLists;
	private readonly ItemTemplateTable? _buyItemItemTemplates;
	private readonly GoodsListTable? _buyItemGoodsLists;
	private readonly long? _buyItemCurrentSellLimit;
	private readonly Func<int>? _buyItemDiagnosticObjectIdProvider;
	private readonly PriceInfluenceRates _buyItemPriceInfluenceRates;
	private readonly PlayerSummonCastSpellService _summonCastSpellService;
	private readonly PlayerSummonSkillExecutionService _summonSkillExecutionService;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly SemaphoreSlim _closeLock = new(1, 1);
	private GameConnectionState _state = GameConnectionState.Connected;
	private int _accountId;
	private string _accountName = string.Empty;
	private byte _accessLevel;
	private byte _membership;
	private long? _accountCreationEpochMillis;
	private Player? _activePlayer;
	private bool _accountDisconnectNotified;
	private string _macAddress = string.Empty;
	private string _hddSerial = string.Empty;
	private string _securityToken = string.Empty;
	private int _corruptPackets;
	private DateTimeOffset? _lastPingTime;
	private PendingItemUse? _pendingItemUse;
	private PendingHouseObjectUse? _pendingHouseObjectUse;

	public GameServerConnection(
		ILogger logger,
		TcpClient client,
		string clientId,
		GamePacketProcessor<string> packetProcessor,
		GameServerOptions? options = null,
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
		IGameClientConnectionRegistry? connectionRegistry = null,
		IDFactory? idFactory = null,
		GameTimeService? gameTimeService = null,
		GameWorld? world = null,
		ThreadPoolManager? threadPoolManager = null,
		IHouseDoorStateService? houseDoorStateService = null,
		Action<GameServerPacket>? sentPacketObserver = null,
		RiftService? riftService = null,
		RiftPortalDialogService? riftPortalDialogService = null,
		RiftPortalUseService? riftPortalUseService = null,
		RiftInformerService? riftInformerService = null,
		VortexLocationService? vortexLocationService = null,
		WorldNpcLootService? worldNpcLootService = null,
		Func<Player, int, bool>? isKnownNpc = null,
		RiftPortalInteractionService? riftPortalInteractionService = null,
		GameCrypt? crypt = null,
		CreaturePvpZoneCounterService? creaturePvpZoneCounterService = null,
		PlayerGroupRuntime? playerGroupRuntime = null,
		PlayerAllianceRuntime? playerAllianceRuntime = null,
		AutoGroupInstanceLeaveRuntimeService? autoGroupInstanceLeaveRuntimeService = null,
		AutoGroupLookingPartyRegistrationService? autoGroupLookingPartyRegistrations = null,
		AutoGroupPenaltyRefreshSchedulerService? autoGroupPenaltyRefreshScheduler = null,
		PeriodicInstanceRegistrationService? periodicInstanceRegistrations = null,
		PlayerLeagueRuntime? playerLeagueRuntime = null,
		PlayerGroupInviteRequestService? playerGroupInviteRequestService = null,
		PlayerAllianceInviteRequestService? playerAllianceInviteRequestService = null,
		PlayerDuelRequestService? playerDuelRequestService = null,
		PlayerExchangeRequestService? playerExchangeRequestService = null,
		PlayerShowBrandCommandPlanner? showBrandCommandPlanner = null,
		PlayerCastSpellEarlyExitService? castSpellEarlyExitService = null,
		GameServerCastSpellHandlerHooks? castSpellHooks = null,
		Action<QuestDialogNpcTargetBranchInputAssemblyPlan>? dialogSelectPlanObserver = null,
		Func<bool>? isShuttingDownSoon = null,
		Action<CmCraftRuntimePlan>? cmCraftRuntimePlanObserver = null,
		Action<CmCraftStartCompositionPlan>? cmCraftStartCompositionPlanObserver = null,
		Action<CmBuyItemHandlerCompositionPlan>? cmBuyItemHandlerCompositionPlanObserver = null,
		Action<CmBuyItemSideEffectOutcomePlan>? cmBuyItemSideEffectOutcomePlanObserver = null,
		Action<PrivateStoreCreatePlan>? privateStoreCreatePlanObserver = null,
		Action<PrivateStoreNameOpenCompositionPlan>? privateStoreNameOpenCompositionPlanObserver = null,
		Action<GroupDataExchangeHandlerCompositionPlan>? groupDataExchangeHandlerCompositionPlanObserver = null,
		Action<VortexDefenderInvitationResponseConsumptionReport>? vortexDefenderInvitationResponseObserver = null,
		Action<VortexDefenderAcceptanceRuntimeObserverReport>? vortexDefenderAcceptanceObserver = null,
		VortexInvasionRuntime? vortexInvasionRuntime = null,
		Func<int, Player?>? worldPlayerLookup = null,
		VortexLocationService? defenderAcceptanceVortexLocationService = null,
		FindGroupConnectionClientActionCompositionPlanService? findGroupConnectionClientActionCompositionPlanService = null,
		FindGroupConnectionBoundaryDispatchAdapterService? findGroupConnectionBoundaryDispatchAdapterService = null,
		Func<Player, int, object?, bool?>? buyItemKnownObjectResolver = null,
		TradeListTable? buyItemTradeLists = null,
		ItemTemplateTable? buyItemItemTemplates = null,
		GoodsListTable? buyItemGoodsLists = null,
		long? buyItemCurrentSellLimit = null,
		Func<int>? buyItemDiagnosticObjectIdProvider = null,
		PriceInfluenceRates? buyItemPriceInfluenceRates = null,
		WorldNpcSpawnService? worldNpcSpawnService = null,
		InstanceEmptyInstanceCheckerService? emptyInstanceCheckerService = null)
		: base(logger, client, clientId)
	{
		_packetProcessor = packetProcessor;
		_options = options ?? new GameServerOptions();
		_runtimeContext = runtimeContext;
		_loginServer = loginServer;
		_chatServer = chatServer;
		_characterSelectionRepository = characterSelectionRepository ?? new EmptyCharacterSelectionRepository();
		_characterCreationService = characterCreationService;
		_playerEnterWorldService = playerEnterWorldService;
		_mailRepository = mailRepository;
		_brokerRepository = brokerRepository;
		_socialRepository = socialRepository ?? new EmptySocialRepository();
		_houseAuctionRepository = houseAuctionRepository ?? new EmptyHouseAuctionRepository();
		_housingRepository = housingRepository ?? new EmptyHousingRepository();
		_houseAuctionTiming = houseAuctionTiming ?? new HouseAuctionTimingService();
		_houseMaintenanceTiming = houseMaintenanceTiming ?? new HouseMaintenanceTimingService(_options);
		_motionRepository = motionRepository ?? new EmptyMotionRepository();
		_expirableTaskService = expirableTaskService;
		_connectionRegistry = connectionRegistry;
		_idFactory = idFactory;
		_gameTimeService = gameTimeService;
		_world = world;
		_threadPoolManager = threadPoolManager;
		_houseDoorStateService = houseDoorStateService;
		_sentPacketObserver = sentPacketObserver;
		_dialogSelectPlanObserver = dialogSelectPlanObserver;
		_worldNpcLootService = worldNpcLootService;
		_worldNpcSpawnService = worldNpcSpawnService;
		_emptyInstanceCheckerService = emptyInstanceCheckerService;
		_isKnownNpc = isKnownNpc;
		_creaturePvpZoneCounterService = creaturePvpZoneCounterService;
		_playerGroupRuntime = playerGroupRuntime ?? new PlayerGroupRuntime();
		_playerAllianceRuntime = playerAllianceRuntime ?? new PlayerAllianceRuntime();
		_autoGroupInstanceLeaveRuntimeService = autoGroupInstanceLeaveRuntimeService
			?? new AutoGroupInstanceLeaveRuntimeService(_playerGroupRuntime, _playerAllianceRuntime);
		_autoGroupLookingPartyRegistrations = autoGroupLookingPartyRegistrations ?? new AutoGroupLookingPartyRegistrationService();
		_autoGroupPenaltyRefreshScheduler = autoGroupPenaltyRefreshScheduler;
		_periodicInstanceRegistrations = periodicInstanceRegistrations ?? new PeriodicInstanceRegistrationService();
		_playerLeagueRuntime = playerLeagueRuntime ?? new PlayerLeagueRuntime();
		_playerGroupInviteRequestService = playerGroupInviteRequestService ?? new PlayerGroupInviteRequestService();
		_playerAllianceInviteRequestService = playerAllianceInviteRequestService ?? new PlayerAllianceInviteRequestService();
		_playerDuelRequestService = playerDuelRequestService ?? new PlayerDuelRequestService();
		_playerExchangeRequestService = playerExchangeRequestService ?? new PlayerExchangeRequestService();
		_playerAllianceGroupChangeServicePlanner = new PlayerAllianceGroupChangeServicePlanner(_playerAllianceRuntime);
		_showBrandCommandPlanner = showBrandCommandPlanner
			?? new PlayerShowBrandCommandPlanner(_playerGroupRuntime, _playerAllianceRuntime);
		_castSpellEarlyExitService = castSpellEarlyExitService ?? new PlayerCastSpellEarlyExitService();
		_castSpellHooks = castSpellHooks ?? new GameServerCastSpellHandlerHooks();
		_isShuttingDownSoon = isShuttingDownSoon ?? (() => false);
		_cmCraftRuntimePlanObserver = cmCraftRuntimePlanObserver;
		_cmCraftStartCompositionPlanObserver = cmCraftStartCompositionPlanObserver;
		_cmBuyItemHandlerCompositionPlanObserver = cmBuyItemHandlerCompositionPlanObserver;
		_cmBuyItemSideEffectOutcomePlanObserver = cmBuyItemSideEffectOutcomePlanObserver;
		_privateStoreCreatePlanObserver = privateStoreCreatePlanObserver;
		_privateStoreNameOpenCompositionPlanObserver = privateStoreNameOpenCompositionPlanObserver;
		_groupDataExchangeHandlerCompositionPlanObserver = groupDataExchangeHandlerCompositionPlanObserver;
		_vortexDefenderInvitationResponseObserver = vortexDefenderInvitationResponseObserver;
		_vortexDefenderAcceptanceObserver = vortexDefenderAcceptanceObserver;
		_vortexInvasionRuntime = vortexInvasionRuntime;
		_worldPlayerLookup = worldPlayerLookup;
		_defenderAcceptanceVortexLocationService = defenderAcceptanceVortexLocationService;
		_findGroupConnectionClientActionCompositionPlanService = findGroupConnectionClientActionCompositionPlanService;
		_findGroupConnectionBoundaryDispatchAdapterService = findGroupConnectionBoundaryDispatchAdapterService;
		_buyItemKnownObjectResolver = buyItemKnownObjectResolver;
		_buyItemTradeLists = buyItemTradeLists;
		_buyItemItemTemplates = buyItemItemTemplates;
		_buyItemGoodsLists = buyItemGoodsLists;
		_buyItemCurrentSellLimit = buyItemCurrentSellLimit;
		_buyItemDiagnosticObjectIdProvider = buyItemDiagnosticObjectIdProvider;
		_buyItemPriceInfluenceRates = buyItemPriceInfluenceRates ?? new PriceInfluenceRates();
		_summonCastSpellService = new PlayerSummonCastSpellService();
		_summonSkillExecutionService = new PlayerSummonSkillExecutionService();
		_riftPortalInteractionService = riftPortalInteractionService
			?? (riftService == null
				? null
				: new RiftPortalInteractionService(
					riftService,
					riftPortalDialogService,
					riftPortalUseService,
					riftInformerService ?? (_connectionRegistry == null ? null : new RiftInformerService(riftService, _connectionRegistry)),
					vortexLocationService,
					_world,
					_isKnownNpc));
		_portalEntryInteractionService = _playerEnterWorldService == null
			? null
			: new PortalEntryInteractionService(_playerEnterWorldService);
		_crypt = crypt ?? new GameCrypt();
	}

	internal Player? ActivePlayer => _activePlayer;

	public GameConnectionState State => _state;

	internal FindGroupConnectionBoundaryDispatchAdapterPlan? CreateDisabledFindGroupBoundaryPlan(
		CmFindGroup packet,
		int nowEpochSeconds,
		Func<int, Player?>? resolvePlayer = null)
	{
		// Java parity: network/aion/clientpackets/CM_FIND_GROUP.runImpl dispatches
		// FindGroupService.getInstance() actions. This non-live consumer composes the
		// connection boundary shape without invoking ProcessPacketAsync live sends.
		if (_findGroupConnectionClientActionCompositionPlanService == null
			|| _findGroupConnectionBoundaryDispatchAdapterService == null)
		{
			return null;
		}

		var resolvedPlayer = resolvePlayer ?? ResolveOnlinePlayerByObjectId;
		var compositionPlan = _findGroupConnectionClientActionCompositionPlanService.CreateDisabledPlan(
			this,
			packet,
			nowEpochSeconds,
			resolvedPlayer);
		return _findGroupConnectionBoundaryDispatchAdapterService.CreateDisabledPlan(
			compositionPlan,
			resolvedPlayer,
			_playerGroupRuntime,
			_playerAllianceRuntime);
	}

	private Player? ResolveOnlinePlayerByObjectId(int objectId)
	{
		if (_activePlayer?.ObjectId == objectId)
			return _activePlayer;

		Player? resolvedPlayer = null;
		_connectionRegistry?.ForEachOnlinePlayer(player =>
		{
			if (resolvedPlayer == null && player.ObjectId == objectId)
				resolvedPlayer = player;
		});
		return resolvedPlayer;
	}

	internal static int GetGeneralInfoWarehouseRestrictionFlag(
		int itemId,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: dataholders/ItemRestrictionCleanupData.hasAccountOrLegionWhStorabilityDisabled
		// feeds GeneralInfoBlobEntry's account/legion warehouse restriction bitmask.
		return itemId != 0 && itemRestrictionCleanups?.HasAccountOrLegionWarehouseStorabilityDisabled(itemId) == true ? 3 : 0;
	}

	public override async Task RunAsync()
	{
		// Java parity: network/aion/AionConnection.initialized sends SM_KEY.
		await SendPacketAsync(new SmKey());
		await base.RunAsync();
	}

	public async Task SendPacketAsync(GameServerPacket packet, CancellationToken cancellationToken = default)
	{
		// Java parity: network/aion/AionConnection.sendPacket via AionServerPacket.write.
		await _sendLock.WaitAsync(cancellationToken);
		try
		{
			if (!_isConnected)
				return;

			_sentPacketObserver?.Invoke(packet);
			var frame = packet.SerializeFrame(_crypt);
			await WriteAsync(frame, 0, frame.Length);
		}
		finally
		{
			_sendLock.Release();
		}
	}

	protected override async Task<PacketBuffer?> ReadPacketAsync()
	{
		// Java parity: network/aion/AionPacketHandler frame read + GameCrypt decrypt.
		var header = await ReadExactOrNullAsync(2);
		if (header == null)
			return null;

		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (frameLength < 3)
			return null;

		var payload = await ReadExactOrNullAsync(frameLength - 2);
		if (payload == null)
			return null;

		if (!_crypt.IsEnabled)
			return new PacketBuffer(payload, strictReads: false);

		if (!_crypt.DecryptClientPayload(payload))
		{
			_corruptPackets++;
			if (_corruptPackets >= MaxCorruptPacketsBeforeDisconnect)
			{
				_logger.LogWarning("Client packet decryption failed {Count} times for {ClientId}, disconnecting", _corruptPackets, _clientId);
				await CloseAsync();
				return null;
			}

			_logger.LogDebug("[{Count}/{Max}] Decrypt fail from {ClientId}, packet ignored", _corruptPackets, MaxCorruptPacketsBeforeDisconnect, _clientId);
			return new PacketBuffer(Array.Empty<byte>(), strictReads: false);
		}

		_corruptPackets = 0;
		return new PacketBuffer(payload, strictReads: false);
	}

	protected override async Task ProcessPacketAsync(PacketBuffer packet)
	{
		// Java parity: network/aion/AionClientPacketFactory + packet runImpl dispatch shell.
		if (packet.Capacity < 5)
			return;

		var payload = packet.GetBuffer().AsSpan(0, packet.Capacity).ToArray();
		var parsed = GameClientPacketFactory.TryCreatePacket(payload, _state);
		if (parsed == null)
		{
			_logger.LogDebug("Unknown game client packet from {ClientId} in state {State}", _clientId, _state);
			return;
		}

		await HandleInfrastructurePacketAsync(parsed);
		await _packetProcessor.ProcessAsync(_clientId, parsed);
	}

	public override async Task CloseAsync()
	{
		await _closeLock.WaitAsync();
		try
		{
			if (!_isConnected)
				return;

			// Java parity: online player is removed from World-backed player lookups when the connection closes.
			await LeaveActivePlayerAsync(notifyPostmanClient: false);

			await NotifyAccountDisconnectedAsync();

			await _sendLock.WaitAsync();
			try
			{
				await base.CloseAsync();
			}
			finally
			{
				_sendLock.Release();
			}
		}
		finally
		{
			_closeLock.Release();
		}
	}

	private async Task HandleInfrastructurePacketAsync(GameClientPacket packet)
	{
		// Java parity: runImpl methods for the registered CM_* infrastructure packets handled here.
		switch (packet)
		{
			case CmVersionCheck versionCheck:
				// Java parity: network/aion/clientpackets/CM_VERSION_CHECK.runImpl sends SM_VERSION_CHECK
				// with dynamic config, server-time, chat-server, ratio, passport, and event-theme data.
				// The incompatible-version response is deterministic; the success response remains unported.
				if (versionCheck.AionClientVersion != SmVersionCheck.InternalVersion)
					await SendPacketAsync(new SmVersionCheck(versionCheck.AionClientVersion, EventTheme.None));
				break;
			case CmL2AuthLoginCheck auth:
				var authResult = await AuthenticateAccountAsync(auth);
				_state = authResult.Ok ? GameConnectionState.Authed : GameConnectionState.Connected;
				var accountName = authResult.AccountName.Length > 0 ? authResult.AccountName : $"account-{auth.AccountId}";
				if (authResult.Ok)
				{
					_accountId = auth.AccountId;
					_accountName = accountName;
					_accessLevel = authResult.AccessLevel;
					_membership = authResult.Membership;
					_accountCreationEpochMillis = authResult.CreationDate > 0 ? authResult.CreationDate : null;
					_activePlayer = null;
					_accountDisconnectNotified = false;
				}
				else
				{
					_accountId = 0;
					_accountName = string.Empty;
					_accessLevel = 0;
					_membership = 0;
					_accountCreationEpochMillis = null;
					_activePlayer = null;
				}

				var worldMaps = _runtimeContext?.DataManager?.StaticData.WorldMaps ?? Array.Empty<WorldMapSummary>();
				await SendPacketAsync(new SmL2AuthLoginCheck(authResult.Ok, accountName, worldMaps));
				if (authResult.Ok)
					await NotifyAccountConnectedAsync(auth.AccountId);
				else
					await NotifyLoginAccountDisconnectedAsync(auth.AccountId);
				break;
			case CmMacAddress macAddress:
				_macAddress = macAddress.MacAddress;
				_hddSerial = macAddress.HddSerial;
				break;
			case CmPing ping:
				await HandlePingAsync(ping);
				break;
			case CmPingRequest:
				// Java parity: network/aion/clientpackets/CM_PING_REQUEST.runImpl -> SM_PING_RESPONSE.
				await SendPacketAsync(new SmPingResponse());
				break;
			case CmGameguard:
				// Java parity: network/aion/clientpackets/CM_GAMEGUARD.runImpl delegates to AntiHackService.checkAionBin; deferred.
				break;
			case CmGroupDistribution groupDistribution:
				// Java parity: network/aion/clientpackets/CM_GROUP_DISTRIBUTION.runImpl -> TeamKinahDistributionEvent.
				// The group variant (partyType 1, not in alliance) is live; alliance/league variants remain deferred.
				if (_activePlayer != null)
					await HandleGroupDistributionAsync(_activePlayer, groupDistribution.Amount, groupDistribution.PartyType);
				break;
			case CmDeleteItem deleteItem:
				if (_activePlayer != null)
					await HandleDeleteItemAsync(_activePlayer, deleteItem);
				break;
			case CmAbyssRankingLegions:
				// Java parity: network/aion/clientpackets/CM_ABYSS_RANKING_LEGIONS.runImpl resolves race-specific legion ranking cache; deferred.
				break;
			case CmAbyssRankingPlayers:
				// Java parity: network/aion/clientpackets/CM_ABYSS_RANKING_PLAYERS.runImpl resolves race-specific player ranking cache; deferred.
				break;
			case CmAutoGroup autoGroup:
				if (_activePlayer != null)
					await HandleAutoGroupAsync(_activePlayer, autoGroup);
				break;
			case CmFusionWeapons:
				// Java parity: network/aion/clientpackets/CM_FUSION_WEAPONS.runImpl validates armsfusion NPC targeting before service dispatch; deferred.
				break;
			case CmBreakWeapons:
				// Java parity: network/aion/clientpackets/CM_BREAK_WEAPONS.runImpl validates armsfusion NPC targeting before service dispatch; deferred.
				break;
			case CmOpenStaticDoor:
				// Java parity: network/aion/clientpackets/CM_OPEN_STATICDOOR.runImpl dispatches StaticDoorService.openStaticDoor; deferred.
				break;
			case CmWindstream windstream:
				if (_activePlayer != null)
					await HandleWindstreamAsync(_activePlayer, windstream);
				break;
			case CmLegionWarehouseKinah:
				// Java parity: network/aion/clientpackets/CM_LEGION_WH_KINAH.runImpl mutates player/legion Kinah and history; deferred.
				break;
			case CmGroupDataExchange groupDataExchange:
				await HandleGroupDataExchangeAsync(groupDataExchange);
				break;
			case CmFindGroup findGroup:
				await HandleFindGroupAsync(findGroup);
				break;
			case CmGfWebshopTokenRequest:
				// Java parity: network/aion/clientpackets/CM_GF_WEBSHOP_TOKEN_REQUEST.runImpl sends an empty token response.
				await SendPacketAsync(new SmGfWebshopTokenResponse(string.Empty));
				break;
			case CmChallengeList:
				// Java parity: network/aion/clientpackets/CM_CHALLENGE_LIST.runImpl dispatches ChallengeTaskService list requests; deferred.
				break;
			case CmMegaphone:
				// Java parity: network/aion/clientpackets/CM_MEGAPHONE.runImpl validates item use before MegaphoneAction execution; deferred.
				break;
			case CmUnwrapItem unwrapItem:
				if (_activePlayer != null)
					await HandleUnwrapItemAsync(_activePlayer, unwrapItem);
				break;
			case CmUpgradeArcade:
				// Java parity: network/aion/clientpackets/CM_UPGRADE_ARCADE.runImpl gates on EventsConfig then dispatches UpgradeArcadeService; deferred.
				break;
			case CmTimeCheck timeCheck:
				// Java parity: network/aion/clientpackets/CM_TIME_CHECK.runImpl sends SM_AFTER_TIME_CHECK_4_7_5 before SM_TIME_CHECK.
				await SendPacketAsync(new SmAfterTimeCheck475());
				await SendPacketAsync(new SmTimeCheck(timeCheck.NanoTime));
				break;
			case CmMayLoginIntoGame:
				await SendPacketAsync(new SmMayLoginIntoGame());
				break;
			case CmDisconnect:
				// Java parity: network/aion/clientpackets/CM_DISCONNECT.runImpl has no side effect; the socket closes separately.
				break;
			case CmMayQuit:
				break;
			case CmQuit quit:
				await HandleQuitAsync(quit);
				break;
			case CmLevelReady:
				if (_activePlayer != null)
					await HandleLevelReadyAsync(_activePlayer);
				break;
			case CmRevive revive:
				if (_activePlayer != null)
					await HandleReviveAsync(_activePlayer, revive);
				break;
			case CmRejectRevive:
				// Java parity: network/aion/clientpackets/CM_REJECT_REVIVE.runImpl has no side effect.
				break;
			case CmTeleportAnimationDone:
				if (_activePlayer != null)
					await HandleTeleportAnimationDoneAsync(_activePlayer);
				break;
			case CmTeleportSelect:
				// Java parity: network/aion/clientpackets/CM_TELEPORT_SELECT.runImpl validates an NPC teleporter and dispatches TeleportService.teleport.
				// Teleporter templates, NPC known-list validation, audit logging, and airport route side effects remain unported.
				break;
			case CmHouseTeleportBack:
				// Java parity: network/aion/clientpackets/CM_HOUSE_TELEPORT_BACK.runImpl uses battle-return teleport state; deferred.
				break;
			case CmBindPointTeleport:
				// Java parity: network/aion/clientpackets/CM_BIND_POINT_TELEPORT.runImpl -> BindPointTeleportService.teleport/cancelTeleport;
				// full channeling + teleport dispatch remains deferred (BindPointTeleportHandlerCompositionPlanService.IsLive = false).
				break;
			case CmPositionSelf:
				// Java parity: network/aion/clientpackets/CM_POSITION_SELF.runImpl has no side effect.
				break;
			case CmHeadingUpdate:
				// Java parity: network/aion/clientpackets/CM_HEADING_UPDATE.runImpl has no side effect.
				break;
			case CmQuestionnaire:
				// Java parity: network/aion/clientpackets/CM_QUESTIONNAIRE.runImpl dispatches HTMLService rewards; deferred until questionnaire rewards are ported.
				break;
			case CmGather gather:
				// Java parity: network/aion/clientpackets/CM_GATHER.runImpl dispatches start/cancel gathering to Gatherable.getController();
				// deferred until gathering controller and gatherable world objects are ported.
				break;
			case CmStartLoot startLoot:
				await HandleStartLootAsync(startLoot);
				break;
			case CmLootItem lootItem:
				await HandleLootItemAsync(lootItem);
				break;
			case CmMoveItem moveItem:
				if (_activePlayer != null)
					await HandleMoveItemAsync(_activePlayer, moveItem);
				break;
			case CmSplitItem splitItem:
				if (_activePlayer != null)
					await HandleSplitItemAsync(_activePlayer, splitItem);
				break;
			case CmSubzoneChange:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_SUBZONE_CHANGE.runImpl -> Player.revalidateZones.
					await RevalidatePlayerFlightZonesAsync(_activePlayer);
				}
				break;
			case CmChangeChannel:
				// Java parity: network/aion/clientpackets/CM_CHANGE_CHANNEL.runImpl dispatches TeleportService.changeChannel; deferred until channel instances are ported.
				break;
			case CmSecurityToken:
				if (_accountId != 0)
				{
					// Java parity: network/aion/clientpackets/CM_SECURITY_TOKEN.runImpl -> SecurityTokenService.generateToken + SM_SECURITY_TOKEN.
					await SendPacketAsync(new SmSecurityToken(GetOrCreateSecurityToken()));
				}
				break;
			case CmPlayerStatusInfo playerStatusInfo:
				if (_activePlayer != null)
					await HandlePlayerStatusInfoAsync(_activePlayer, playerStatusInfo);
				break;
			case CmInviteToGroup inviteToGroup:
				if (_activePlayer != null)
					await HandleInviteToGroupAsync(_activePlayer, inviteToGroup);
				break;
			case CmViewPlayerDetails viewPlayerDetails:
				if (_activePlayer != null)
					await HandleViewPlayerDetailsAsync(_activePlayer, viewPlayerDetails);
				break;
			case CmDuelRequest duelRequest:
				if (_activePlayer != null)
					await HandleDuelRequestAsync(_activePlayer, duelRequest);
				break;
			case CmCheckPak checkPak:
				// Java parity: network/aion/clientpackets/CM_CHECK_PAK.runImpl audit-logs suspicious pak status.
				if (!string.IsNullOrEmpty(checkPak.PakStatus)
					&& !checkPak.PakStatus.EndsWith("[1:OK]", StringComparison.Ordinal)
					&& !checkPak.PakStatus.Contains("File not found", StringComparison.Ordinal))
				{
					_logger.LogWarning("Suspicious pak status from {ClientId}: {PakStatus}", _clientId, checkPak.PakStatus);
				}
				break;
			case CmPlayMovieEnd:
				// Java parity: network/aion/clientpackets/CM_PLAY_MOVIE_END.runImpl dispatches quest and instance movie-end hooks; deferred until those systems are ported.
				break;
			case CmShowMap showMap:
				// Java parity: network/aion/clientpackets/CM_SHOW_MAP.runImpl.
				// Action 0 = ConquerorAndProtectorService.intruderScan — deferred until conqueror/protector system is ported.
				// Action 1 = TODO/unknown in Java source.
				if (showMap.Action != 0 && showMap.Action != 1)
					_logger.LogWarning("Unknown show map action {Action} from {ClientId}", showMap.Action, _clientId);
				break;
			case CmCheckMailUnknown:
				// Java parity: network/aion/clientpackets/CM_CHECK_MAIL_UNK.runImpl is TODO/no-op.
				break;
			case CmAtreianPassport:
				// Java parity: network/aion/clientpackets/CM_ATREIAN_PASSPORT.runImpl calls AtreianPassportService.takeReward.
				// Reward execution/account passport mutation remain unported; keep this parser-only for now.
				break;
			case CmObjectSearch objectSearch:
				if (_activePlayer != null)
					await HandleObjectSearchAsync(_activePlayer, objectSearch);
				break;
			case CmPlayerListener:
				// Java parity: network/aion/clientpackets/CM_PLAYER_LISTENER.runImpl dispatches WebRewardService when enabled; deferred until web rewards are ported.
				break;
			case CmDeleteQuest deleteQuest:
				if (_activePlayer != null)
					await HandleDeleteQuestAsync(_activePlayer, deleteQuest.QuestId);
				break;
			case CmUiSettings uiSettings:
				if (_activePlayer != null)
					HandleUiSettings(_activePlayer, uiSettings);
				break;
			case CmCustomSettings customSettings:
				if (_activePlayer != null)
					await HandleCustomSettingsAsync(_activePlayer, customSettings);
				break;
			case CmChatMessagePublic chatMessage:
				if (_activePlayer != null)
					await HandlePublicChatAsync(_activePlayer, chatMessage);
				break;
			case CmChatMessageWhisper whisper:
				if (_activePlayer != null)
					await HandleWhisperChatAsync(_activePlayer, whisper);
				break;
			case CmChatPlayerInfo chatPlayerInfo:
				if (_activePlayer != null)
					await HandleChatPlayerInfoAsync(_activePlayer, chatPlayerInfo);
				break;
			case CmChatGroupInfo chatGroupInfo:
				await HandleChatGroupInfoAsync(chatGroupInfo);
				break;
			case CmSetNote setNote:
				if (_activePlayer != null)
					await HandleSetNoteAsync(_activePlayer, setNote);
				break;
			case CmMotion motion:
				if (_activePlayer != null)
					await HandleMotionAsync(_activePlayer, motion);
				break;
			case CmEmotion emotion:
				if (_activePlayer != null)
					await HandleEmotionAsync(_activePlayer, emotion);
				break;
			case CmLegion:
				// Java parity: network/aion/clientpackets/CM_LEGION.runImpl dispatches LegionService by exOpcode.
				// Legion mutation and membership side effects remain unported; keep this parser-only for now.
				break;
			case CmCharacterEdit:
				// Java parity: CM_CHARACTER_EDIT.runImpl -> PlayerEnterWorldService.enterWorld + appearance mutation; deferred until character edit is ported.
				break;
			case CmCaptcha:
				// Java parity: CM_CAPTCHA.runImpl -> PunishmentService captcha verification; deferred until anti-bot punishment is ported.
				break;
			case CmLegionSendEmblemInfo:
				// Java parity: CM_LEGION_SEND_EMBLEM_INFO.runImpl -> LegionService.getLegion(legionId) -> SM_LEGION_SEND_EMBLEM; deferred.
				break;
			case CmLegionSendEmblem:
				// Java parity: CM_LEGION_SEND_EMBLEM.runImpl -> LegionService data dispatch; deferred.
				break;
			case CmLegionHistory:
				// Java parity: CM_LEGION_HISTORY.runImpl. Java returns silently when player.getLegion() == null, else sends
				// SM_LEGION_HISTORY(legion.getHistory(type), page, type) (with a REWARD-type brigade-general guard).
				// The SM_LEGION_HISTORY server packet is now ported (SmLegionHistory, golden-tested) and legion member rank
				// is loaded (Player.IsBrigadeGeneral) so the REWARD/brigade-general guard is now expressible. Live wiring
				// stays deferred because the C# port still has no legion-history data source (Legion.getHistory) to project
				// into the packet. No response is sent — matching Java exactly for the no-legion case (the only case the
				// port can currently represent).
				break;
			case CmLegionModifyEmblem:
				// Java parity: CM_LEGION_MODIFY_EMBLEM.runImpl -> LegionService.updateEmblem; deferred.
				break;
			case CmLegionUploadInfo:
				// Java parity: CM_LEGION_UPLOAD_INFO.runImpl sends emblem info to LegionService; deferred.
				break;
			case CmLegionUploadEmblem:
				// Java parity: CM_LEGION_UPLOAD_EMBLEM.runImpl sends binary emblem data to LegionService; deferred.
				break;
			case CmLegionDominionRequestRanking:
				// Java parity: CM_LEGION_DOMINION_REQUEST_RANKING.runImpl dispatches DominionService; deferred.
				break;
			case CmQuestShare questShare:
				// Java parity: CM_QUEST_SHARE.runImpl -> QuestService.checkStartConditions + SM_QUEST_ACTION to group.
				if (_activePlayer != null)
					await HandleQuestShareAsync(_activePlayer, questShare.QuestId);
				break;
			case CmBuilderCommand:
				// Java parity: CM_BUILDER_COMMAND.runImpl logs //// prefixed command to ADMINAUDIT_LOG; deferred until admin command pipeline is ported.
				break;
			case CmBuilderControl:
				// Java parity: CM_BUILDER_CONTROL.runImpl — no Java side effect defined; logged only.
				break;
			case CmDebugCommand:
				// Java parity: CM_DEBUG_COMMAND.runImpl logs //// prefixed command to ADMINAUDIT_LOG; deferred.
				break;
			case CmInstanceLeave:
				// Java parity: network/aion/clientpackets/CM_INSTANCE_LEAVE.runImpl delegates to the live instance handler; deferred.
				break;
			case CmClientCommandRoll commandRoll:
				if (_activePlayer != null)
					await HandleClientCommandRollAsync(_activePlayer, commandRoll);
				break;
			case CmStopTraining:
				// Java parity: network/aion/clientpackets/CM_STOP_TRAINING.runImpl delegates to the live instance handler; deferred.
				break;
			case CmRecipeDelete recipeDelete:
				if (_activePlayer != null)
					await HandleRecipeDeleteAsync(_activePlayer, recipeDelete);
				break;
			case CmHouseSettings houseSettings:
				if (_activePlayer != null)
					await HandleHouseSettingsAsync(_activePlayer, houseSettings);
				break;
			case CmHouseKick houseKick:
				if (_activePlayer != null)
					await HandleHouseKickAsync(_activePlayer, houseKick);
				break;
			case CmHouseDecorate houseDecorate:
				if (_activePlayer != null)
					await HandleHouseDecorateAsync(_activePlayer, houseDecorate);
				break;
			case CmHouseEdit houseEdit:
				if (_activePlayer != null)
					await HandleHouseEditAsync(_activePlayer, houseEdit);
				break;
			case CmHouseScript:
				// Java parity: network/aion/clientpackets/CM_HOUSE_SCRIPT.runImpl validates active house ownership,
				// mutates PlayerScripts, sends overflow errors, and broadcasts SM_HOUSE_SCRIPTS. Live script persistence/fanout remains unported.
				break;
			case CmMarkFriendList:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_MARK_FRIENDLIST.runImpl -> SM_MARK_FRIENDLIST.
					await SendPacketAsync(new SmMarkFriendList(_activePlayer.ObjectId));
				}
				break;
			case CmFriendAdd friendAdd:
				if (_activePlayer != null)
					await HandleFriendAddAsync(_activePlayer, friendAdd);
				break;
			case CmFriendDelete friendDelete:
				if (_activePlayer != null)
					await HandleFriendDeleteAsync(_activePlayer, friendDelete);
				break;
			case CmTargetSelect targetSelect:
				if (_activePlayer != null)
					HandleTargetSelect(_activePlayer, targetSelect);
				break;
			case CmCastSpell castSpell:
				if (_activePlayer != null)
					await HandleCastSpellAsync(_activePlayer, castSpell);
				break;
			case CmToggleSkillDeactivate:
				// Java parity: network/aion/clientpackets/CM_TOGGLE_SKILL_DEACTIVATE.runImpl validates toggle/stance skills, removes the effect, and stops matching stance.
				// Live SkillEngine effect-controller and stance-controller mutation remain unported.
				break;
			case CmRemoveAlteredState:
				// Java parity: network/aion/clientpackets/CM_REMOVE_ALTERED_STATE.runImpl blocks client debuff removal and ends non-debuff effects.
				// Live EffectController lookup, debuff audit logging, and effect-end mutation remain unported.
				break;
			case CmSummonMove:
				// Java parity: network/aion/clientpackets/CM_SUMMON_MOVE.runImpl mutates summon/mercenary movement,
				// updates world position/last-move state, and may broadcast SM_MOVE. Live summon movement remains unported.
				break;
			case CmSummonEmotion:
				// Java parity: network/aion/clientpackets/CM_SUMMON_EMOTION.runImpl mutates summon/mercenary emotion state
				// for selected emotions and broadcasts SM_EMOTION. Live summon emotion handling remains unported.
				break;
			case CmSummonCastSpell summonCastSpell:
				if (_activePlayer != null)
					await HandleSummonCastSpellAsync(_activePlayer, summonCastSpell);
				break;
			case CmChargeItem chargeItem:
				if (_activePlayer != null)
					await HandleChargeItemAsync(_activePlayer, chargeItem);
				break;
			case CmEquipItem equipItem:
				if (_activePlayer != null)
					await HandleEquipItemAsync(_activePlayer, equipItem);
				break;
			case CmManastone manastone:
				if (_activePlayer != null)
					await HandleManastoneAsync(_activePlayer, manastone);
				break;
			case CmItemRemodel itemRemodel:
				if (_activePlayer != null)
					await HandleItemRemodelAsync(_activePlayer, itemRemodel);
				break;
			case CmItemPurification itemPurification:
				if (_activePlayer != null)
					await HandleItemPurificationAsync(_activePlayer, itemPurification);
				break;
			case CmDialogSelect dialogSelect:
				if (_activePlayer != null)
					await HandleDialogSelectAsync(_activePlayer, dialogSelect);
				break;
			case CmUseItem useItem:
				if (_activePlayer != null)
					await HandleUseItemAsync(_activePlayer, useItem);
				break;
			case CmCompositeStones compositeStones:
				if (_activePlayer != null)
					await HandleCompositeStonesAsync(_activePlayer, compositeStones);
				break;
			case CmTune tune:
				if (_activePlayer != null)
					await HandleTuneAsync(_activePlayer, tune);
				break;
			case CmTuneResult tuneResult:
				if (_activePlayer != null)
					await HandleTuneResultAsync(_activePlayer, tuneResult);
				break;
			case CmCraft craft:
				await HandleCraftAsync(_activePlayer, craft);
				break;
			case CmBuyItem buyItem:
				HandleBuyItem(_activePlayer, buyItem);
				break;
			case CmBuyTradeInTrade:
				// Java parity: network/aion/clientpackets/CM_BUY_TRADE_IN_TRADE.runImpl calls TradeService.performBuyFromTradeInTrade when count >= 1.
				// Live trade-in validation, inventory mutation, persistence, and packet send side effects remain unported.
				break;
			case CmSelectDecomposable selectDecomposable:
				if (_activePlayer != null)
					await HandleSelectDecomposableAsync(_activePlayer, selectDecomposable);
				break;
			case CmAppearance appearance:
				if (_activePlayer != null)
					await HandleAppearanceAsync(_activePlayer, appearance);
				break;
			case CmTitleSet titleSet:
				if (_activePlayer != null)
					await HandleTitleSetAsync(_activePlayer, titleSet);
				break;
			case CmMove move:
				if (_activePlayer != null)
					await HandleMoveAsync(_activePlayer, move);
				break;
			case CmMoveInAir moveInAir:
				if (_activePlayer != null)
				{
					HandleMoveInAir(_activePlayer, moveInAir);
					await RevalidatePlayerFlightZonesAsync(_activePlayer);
				}
				break;
			case CmQuestionResponse questionResponse:
				if (_activePlayer != null)
					await HandleQuestionResponseAsync(_activePlayer, questionResponse);
				break;
			case CmExchangeAddItem exchangeAddItem:
				if (_activePlayer != null)
					await HandleExchangeAddItemAsync(_activePlayer, exchangeAddItem);
				break;
			case CmExchangeAddKinah exchangeAddKinah:
				if (_activePlayer != null)
					await HandleExchangeAddKinahAsync(_activePlayer, exchangeAddKinah);
				break;
			case CmExchangeRequest exchangeRequest:
				if (_activePlayer != null)
					await HandleExchangeRequestAsync(_activePlayer, exchangeRequest);
				break;
			case CmExchangeLock:
				if (_activePlayer != null)
					await HandleExchangeLockAsync(_activePlayer);
				break;
			case CmExchangeOk:
				if (_activePlayer != null)
					await HandleExchangeOkAsync(_activePlayer);
				break;
			case CmExchangeCancel:
				if (_activePlayer != null)
					await HandleExchangeCancelAsync(_activePlayer);
				break;
			case CmShowDialog showDialog:
				if (_activePlayer != null)
					await HandleShowDialogAsync(_activePlayer, showDialog);
				break;
			case CmCloseDialog closeDialog:
				if (_activePlayer != null)
					HandleCloseDialog(_activePlayer, closeDialog);
				break;
			case CmCharacterList characterList:
				await SendPacketAsync(CreateAccountPropertiesPacket());
				var characters = _accountId == 0
					? Array.Empty<CharacterSelectionEntry>()
					: await _characterSelectionRepository.LoadCharactersAsync(_accountId);
				await SendPacketAsync(new SmCharacterList(characterList.PlayOk2, characters));
				break;
			case CmCreateCharacter createCharacter:
				var creationResult = _characterCreationService == null
					? new CharacterCreationResult(
						createCharacter.Type == 1 ? SmCreateCharacter.ResponseOpenCreationWindow : SmCreateCharacter.ResponseDbError)
					: await _characterCreationService.CreateCharacterAsync(
						createCharacter,
						_accountId,
						_accountName.Length == 0 ? createCharacter.AccountName : _accountName,
						_membership);
				await SendPacketAsync(new SmCreateCharacter(creationResult.ResponseCode, creationResult.Character));
				break;
			case CmDeleteCharacter deleteCharacter:
				var deletionTime = _accountId == 0
					? 0
					: await _characterSelectionRepository.MarkCharacterForDeletionAsync(
						_accountId,
						deleteCharacter.CharacterObjectId,
						TimeSpan.FromMinutes(_options.Custom.CharacterDeletionTimeMinutes));
				await SendPacketAsync(
					deletionTime == 0
						? new SmDeleteCharacter(playerObjectId: 0, deletionTimeSeconds: 0)
						: new SmDeleteCharacter(deleteCharacter.CharacterObjectId, deletionTime));
				break;
			case CmRestoreCharacter restoreCharacter:
				var restored = _accountId != 0 && await _characterSelectionRepository.RestoreCharacterAsync(_accountId, restoreCharacter.CharacterObjectId);
				await SendPacketAsync(new SmRestoreCharacter(restoreCharacter.CharacterObjectId, restored));
				break;
			case CmCheckNickname checkNickname:
			{
				// Java parity: network/aion/clientpackets/CM_CHECK_NICKNAME.runImpl -> SM_NICKNAME_CHECK_RESPONSE.
				var responseCode = _characterCreationService == null
					? SmCreateCharacter.ResponseDbError
					: await _characterCreationService.CheckNicknameAsync(checkNickname.Nickname);
				await SendPacketAsync(new SmNicknameCheckResponse(responseCode));
				break;
			}
			case CmShowBlockList:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_SHOW_BLOCKLIST.runImpl -> SM_BLOCK_LIST.
					await SendPacketAsync(new SmBlockList(_activePlayer.BlockedUsers));
				}
				break;
			case CmBlockDelete blockDelete:
				if (_activePlayer != null)
					await HandleBlockDeleteAsync(_activePlayer, blockDelete);
				break;
			case CmFriendStatus friendStatus:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_FRIEND_STATUS.runImpl -> FriendList.setStatus + SM_FRIEND_STATUS.
					await HandleFriendStatusAsync(_activePlayer, friendStatus);
					await SendPacketAsync(new SmFriendStatus(friendStatus.Status));
				}
				break;
			case CmPlayerSearch playerSearch:
				if (_activePlayer != null)
					await HandlePlayerSearchAsync(_activePlayer, playerSearch);
				break;
			case CmReplaceItem replaceItem:
				if (_activePlayer != null)
					await HandleReplaceItemAsync(_activePlayer, replaceItem);
				break;
			case CmGroupLoot:
				// Java parity: network/aion/clientpackets/CM_GROUP_LOOT.runImpl -> DropDistributionService.handleRollOrBid; deferred until drop distribution is ported.
				break;
			case CmDistributionSettings distributionSettings:
				if (_activePlayer != null)
					await HandleDistributionSettingsAsync(_activePlayer, distributionSettings);
				break;
			case CmBlockSetReason blockSetReason:
				if (_activePlayer != null)
					await HandleBlockSetReasonAsync(_activePlayer, blockSetReason);
				break;
			case CmShowBrand showBrand:
				if (_activePlayer != null)
					await HandleShowBrandCommandAsync(_activePlayer, showBrand);
				break;
			case CmCharacterPasskey characterPasskey:
				await SendPacketAsync(new SmCharacterSelect(type: 2, messageType: characterPasskey.Type, wrongCount: 0));
				break;
			case CmBrokerSellWindow brokerSellWindow:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_BROKER_SELL_WINDOW.runImpl -> BrokerService.showSellWindow.
					if (_activePlayer.IsTrading)
						break;
					var item = _activePlayer.InventoryItems.FirstOrDefault(item => item.ObjectId == brokerSellWindow.ItemObjectId);
					if (item != null)
					{
						var priceRange = _brokerRepository == null
							? new PlayerBrokerPriceRange(0, 0)
							: await _brokerRepository.LoadPriceRangeAsync(_activePlayer.Race, item.ItemId);
						await SendPacketAsync(SmBrokerService.CreateSellWindow(brokerSellWindow.ItemObjectId, priceRange.LowestPrice, priceRange.HighestPrice));
					}
				}
				break;
			case CmPrivateStore privateStore:
				// Java parity: network/aion/clientpackets/CM_PRIVATE_STORE.runImpl closes the private store when no
				// items are listed, otherwise calls PrivateStoreService.createStoreWithItems. Live store mutation,
				// validation, packet fanout, and persistence remain deferred; this unit is parser-only.
				if (_activePlayer != null)
					_privateStoreCreatePlanObserver?.Invoke(CreatePrivateStoreCreatePlan(privateStore, _activePlayer));
				break;
			case CmPrivateStoreName privateStoreName:
				// Java parity: network/aion/clientpackets/CM_PRIVATE_STORE_NAME.runImpl calls
				// PrivateStoreService.openPrivateStore(activePlayer, name). Existing C# open-plan diagnostics
				// remain non-live; handler wiring is deferred until store state mutation is ported.
				if (_activePlayer != null)
					_privateStoreNameOpenCompositionPlanObserver?.Invoke(CreatePrivateStoreNameOpenPlan(privateStoreName, _activePlayer));
				break;
			case CmSummonCommand:
				// Java parity: network/aion/clientpackets/CM_SUMMON_COMMAND.runImpl dispatches SummonsService.doMode.
				// Live summon mode command handling remains unported; existing summon command planners are non-live only.
				break;
			case CmBrokerList brokerList:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_BROKER_LIST.runImpl -> BrokerService.showRequestedItems.
					if (!IsTargetingBroker(_activePlayer, brokerList.BrokerObjectId, "browse for broker items"))
						break;
					_activePlayer.BrokerMaskCache = brokerList.ListMask;
					_activePlayer.BrokerSortTypeCache = brokerList.SortType;
					_activePlayer.BrokerStartPageCache = brokerList.Page;
					_activePlayer.BrokerSearchItemIds = Array.Empty<int>();
					var page = await LoadBrokerMaskPageAsync(_activePlayer, brokerList.SortType, brokerList.Page, brokerList.ListMask);
					await SendPacketAsync(SmBrokerService.CreateSearchedItems(page));
				}
				break;
			case CmBrokerSearch brokerSearch:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_BROKER_SEARCH.runImpl -> BrokerService.showRequestedItems.
					if (!IsTargetingBroker(_activePlayer, brokerSearch.BrokerObjectId, "search for items in broker"))
						break;
					_activePlayer.BrokerMaskCache = brokerSearch.Mask;
					_activePlayer.BrokerSortTypeCache = brokerSearch.SortType;
					_activePlayer.BrokerStartPageCache = brokerSearch.Page;
					_activePlayer.BrokerSearchItemIds = brokerSearch.ItemIds.ToArray();
					var page = _brokerRepository != null && brokerSearch.Mask == 0 && brokerSearch.ItemIds.Count > 0
						? await _brokerRepository.SearchItemsByTemplateIdsAsync(_activePlayer.Race, brokerSearch.SortType, brokerSearch.Page, brokerSearch.ItemIds)
						: await LoadBrokerMaskPageAsync(_activePlayer, brokerSearch.SortType, brokerSearch.Page, brokerSearch.Mask);
					await SendPacketAsync(SmBrokerService.CreateSearchedItems(page));
				}
				break;
			case CmBrokerRegistered brokerRegistered:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_BROKER_REGISTERED.runImpl -> BrokerService.showRegisteredItems.
					if (!IsTargetingBroker(_activePlayer, brokerRegistered.BrokerObjectId, "view registered broker items"))
						break;
					var registeredItems = _brokerRepository == null
						? Array.Empty<PlayerBrokerItem>()
						: await _brokerRepository.LoadRegisteredItemsAsync(_activePlayer.ObjectId, _activePlayer.Race);
					await SendPacketAsync(SmBrokerService.CreateRegisteredItems(registeredItems));
				}
				break;
			case CmBrokerSettleList brokerSettleList:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_BROKER_SETTLE_LIST.runImpl -> BrokerService.showSettledItems.
					if (!IsTargetingBroker(_activePlayer, brokerSettleList.BrokerObjectId, "open broker settled item list"))
						break;
					var page = _brokerRepository == null
						? new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, brokerSettleList.StartPageIndex, _activePlayer.BrokerSettlements.EarnedKinah)
						: await _brokerRepository.LoadSettledItemsAsync(_activePlayer.ObjectId, _activePlayer.Race, brokerSettleList.StartPageIndex);
					await SendPacketAsync(SmBrokerService.CreateSettledItems(page));
				}
				break;
			case CmBrokerCancelRegistered brokerCancelRegistered:
				if (_activePlayer != null && IsTargetingBroker(_activePlayer, brokerCancelRegistered.BrokerObjectId, "unregister broker item"))
					await HandleBrokerCancelRegisteredAsync(_activePlayer, brokerCancelRegistered);
				break;
			case CmBrokerSettleAccount brokerSettleAccount:
				if (_activePlayer != null && IsTargetingBroker(_activePlayer, brokerSettleAccount.BrokerObjectId, "collect broker settlement"))
					await HandleBrokerSettleAccountAsync(_activePlayer);
				break;
			case CmRegisterBrokerItem registerBrokerItem:
				if (_activePlayer != null
					&& !_activePlayer.IsTrading
					&& registerBrokerItem.ItemCount > 0
					&& IsTargetingBroker(_activePlayer, registerBrokerItem.BrokerObjectId, "register broker item"))
					await HandleBrokerRegisterItemAsync(_activePlayer, registerBrokerItem);
				break;
			case CmBuyBrokerItem buyBrokerItem:
				if (_activePlayer != null
					&& buyBrokerItem.ItemCount >= 1
					&& IsTargetingBroker(_activePlayer, buyBrokerItem.BrokerObjectId, "buy broker item"))
					await HandleBuyBrokerItemAsync(_activePlayer, buyBrokerItem);
				break;
			case CmSendMail sendMail:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_SEND_MAIL.runImpl -> MailService.sendMail.
					var responsePacket = await HandleSendMailAsync(_activePlayer, sendMail);
					if (responsePacket != null)
						await SendPacketAsync(responsePacket);
				}
				break;
			case CmCheckMailList checkMailList:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_CHECK_MAIL_LIST.runImpl -> MailService.sendMailList.
					_activePlayer.MailboxState = checkMailList.ExpressOnly ? Player.MailboxExpressState : Player.MailboxRegularState;
					foreach (var mailListPacket in SmMailService.CreateListPackets(
						_activePlayer.ObjectId,
						_activePlayer.Mailbox,
						checkMailList.ExpressOnly))
					{
						await SendPacketAsync(mailListPacket);
					}
				}
				break;
			case CmReadMail readMail:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_READ_MAIL.runImpl -> MailService.readMail.
					var letter = _activePlayer.Mailbox.FirstOrDefault(mail => mail.Id == readMail.MailObjectId);
					if (letter != null)
					{
						var staticData = _runtimeContext?.DataManager?.StaticData;
						var generalInfoWarehouseRestrictionFlag = GetGeneralInfoWarehouseRestrictionFlag(
							letter.AttachedItem?.ItemId ?? letter.AttachedItemTemplateId,
							staticData?.ItemRestrictionCleanups);
						await SendPacketAsync(SmMailService.CreateReadPacket(
							_activePlayer.Mailbox,
							letter,
							staticData?.ItemTemplates,
							generalInfoWarehouseRestrictionFlag));
						_activePlayer.Mailbox = _activePlayer.Mailbox
							.Select(mail => mail.Id == readMail.MailObjectId ? mail with { IsUnread = false } : mail)
							.ToArray();
						if (_mailRepository != null)
							await _mailRepository.MarkMailReadAsync(readMail.MailObjectId);
					}
				}
				break;
			case CmGetMailAttachment getMailAttachment:
				if (_activePlayer != null)
					await HandleGetMailAttachmentAsync(_activePlayer, getMailAttachment);
				break;
			case CmDeleteMail deleteMail:
				if (_activePlayer != null && deleteMail.MailObjectIds.Count > 0)
				{
					// Java parity: network/aion/clientpackets/CM_DELETE_MAIL.runImpl -> MailService.deleteMail.
					var ids = deleteMail.MailObjectIds.ToHashSet();
					_activePlayer.Mailbox = _activePlayer.Mailbox.Where(mail => !ids.Contains(mail.Id)).ToArray();
					if (_mailRepository != null)
						await _mailRepository.DeleteLettersAsync(deleteMail.MailObjectIds);
					await SendPacketAsync(SmMailService.CreateDeletePacket(_activePlayer.Mailbox, deleteMail.MailObjectIds));
				}
				break;
			case CmReadExpressMail readExpressMail:
				if (_activePlayer != null)
					await HandleReadExpressMailAsync(_activePlayer, readExpressMail);
				break;
			case CmUseHouseObject useHouseObject:
				if (_activePlayer != null)
					await HandleUseHouseObjectAsync(_activePlayer, useHouseObject);
				break;
			case CmReleaseObject releaseObject:
				if (_activePlayer != null)
					await HandleReleaseObjectAsync(_activePlayer, releaseObject);
				break;
			case CmBlockAdd blockAdd:
				if (_activePlayer != null)
					await HandleBlockAddAsync(_activePlayer, blockAdd);
				break;
			case CmChatAuth chatAuth:
				if (_activePlayer != null)
					await HandleChatAuthAsync(_activePlayer, chatAuth);
				break;
			case CmMacroCreate macroCreate:
				if (_activePlayer != null)
					await HandleMacroCreateAsync(_activePlayer, macroCreate);
				break;
			case CmMacroDelete macroDelete:
				if (_activePlayer != null)
					await HandleMacroDeleteAsync(_activePlayer, macroDelete);
				break;
			case CmReportPlayer reportPlayer:
				if (_activePlayer != null)
					await HandleReportPlayerAsync(_activePlayer, reportPlayer);
				break;
			case CmInstanceInfo instanceInfo:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_INSTANCE_INFO.runImpl no-team branch -> SM_INSTANCE_INFO(updateType, player).
					var instanceCooltimes = _runtimeContext?.DataManager?.StaticData.InstanceCooltimes
						?? new InstanceCooltimeTable(Array.Empty<InstanceCooltimeSummary>());
					await SendPacketAsync(new SmInstanceInfo(instanceInfo.UpdateType, _activePlayer, instanceCooltimes));
				}
				break;
			case CmShowRestrictions:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_SHOW_RESTRICTIONS.runImpl -> SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_INFO_NORMAL.
					await SendPacketAsync(SmSystemMessage.AccuseInfoNormal());
				}
				break;
			case CmGetHouseBids:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_GET_HOUSE_BIDS.runImpl -> SM_HOUSE_BIDS split list.
					var staticData = _runtimeContext?.DataManager?.StaticData;
					var bidPage = await _houseAuctionRepository.LoadHouseBidsAsync(
						_activePlayer,
						staticData?.HousingTemplates,
						staticData?.NpcTemplates);
					foreach (var bidPacket in SmHouseBids.CreatePackets(bidPage))
						await SendPacketAsync(bidPacket);
				}
				break;
			case CmRegisterHouse registerHouse:
				if (_activePlayer != null)
					await HandleRegisterHouseAsync(_activePlayer, registerHouse);
				break;
			case CmPlaceBid placeBid:
				if (_activePlayer != null)
					await HandlePlaceBidAsync(_activePlayer, placeBid);
				break;
			case CmHousePayRent housePayRent:
				if (_activePlayer != null)
					await HandleHousePayRentAsync(_activePlayer, housePayRent);
				break;
			case CmHouseTeleport:
				// Java parity: network/aion/clientpackets/CM_HOUSE_TELEPORT.runImpl uses HousingService.findActiveHouse + InstanceService.getOrCreateHouseInstance
				// to teleport via relationship crystal; deferred until housing instance access is ported.
				break;
			case CmHouseOpenDoor:
				// Java parity: network/aion/clientpackets/CM_HOUSE_OPEN_DOOR.runImpl teleports player in/out of a house via HousingService.getHouseByAddress;
				// deferred until housing instance entry/exit is ported.
				break;
			case CmShowFriendList:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_SHOW_FRIENDLIST.runImpl -> SM_FRIEND_LIST.
					var staticData = _runtimeContext?.DataManager?.StaticData;
					await SendPacketAsync(new SmFriendList(_activePlayer.Friends, staticData?.PlayerExperienceTable));
				}
				break;
			case CmBonusTitle bonusTitle:
				if (_activePlayer != null)
					await HandleBonusTitleAsync(_activePlayer, bonusTitle);
				break;
			case CmFriendSetMemo friendSetMemo:
				if (_activePlayer != null)
					await HandleFriendSetMemoAsync(_activePlayer, friendSetMemo);
				break;
			case CmEnterWorld enterWorld:
				var enterWorldResult = _playerEnterWorldService == null
					? new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError)
					: await _playerEnterWorldService.EnterWorldAsync(_accountId, enterWorld.ObjectId);
				if (enterWorldResult.Message == EnterWorldCheckMessage.Ok)
					_state = GameConnectionState.InGame;
				_activePlayer = enterWorldResult.Message == EnterWorldCheckMessage.Ok ? enterWorldResult.Player : null;
				if (_activePlayer != null)
				{
					PlayerAccountRuntimeStateService.ApplyLoginAccountState(
						_activePlayer,
						new PlayerAccountRuntimeState(_accessLevel, _membership, _accountCreationEpochMillis));
					_connectionRegistry?.RegisterPlayerConnection(_activePlayer.ObjectId, this);

					var staticData = _runtimeContext?.DataManager?.StaticData;
					if (staticData != null)
					{
						// Java parity: services/StigmaService.onPlayerLogin runs before SM_ENTER_WORLD_CHECK/SM_SKILL_LIST.
						var stigmaLogin = StigmaService.ApplyOnLogin(
							_activePlayer,
							staticData.ItemTemplates,
							staticData.SkillTemplates,
							staticData.SkillTree,
							staticData.PlayerExperienceTable,
							_options.Membership.StigmaAutoLearn,
							_options.Membership.StigmaSlotQuest);
						var savedStigmaCleanup = !stigmaLogin.Changed
							|| stigmaLogin.PersistedItems.Count == 0
							|| _playerEnterWorldService == null
							|| await _playerEnterWorldService.SaveEquipmentMutationAsync(_activePlayer, stigmaLogin.PersistedItems);
						if (stigmaLogin.Changed && savedStigmaCleanup)
						{
							_activePlayer.InventoryItems = stigmaLogin.InventoryItems;
							_activePlayer.Skills = stigmaLogin.Skills;
						}
					}
				}
				await SendPacketAsync(new SmEnterWorldCheck(enterWorldResult.Message));
				if (enterWorldResult is { Message: EnterWorldCheckMessage.Ok, Player: not null })
				{
					// Java parity: PlayerEnterWorldService sends SM_SKILL_LIST after SM_ENTER_WORLD_CHECK.
					await SendPacketAsync(new SmSkillList(enterWorldResult.Player.Skills));
					var skillTemplates = _runtimeContext?.DataManager?.StaticData.SkillTemplates;
					if (skillTemplates != null && enterWorldResult.Player.SkillCooldowns.Count > 0)
					{
						var cooldownPacket = new SmSkillCooldown(
							enterWorldResult.Player.Skills,
							enterWorldResult.Player.SkillCooldowns,
							skillTemplates,
							notify: false);
						if (cooldownPacket.HasCooldowns)
							await SendPacketAsync(cooldownPacket);
					}

					if (enterWorldResult.Player.ItemCooldowns.Count > 0)
					{
						// Java parity: PlayerEnterWorldService sends SM_ITEM_COOLDOWN after SM_SKILL_COOLDOWN when item cooldowns exist.
						await SendPacketAsync(new SmItemCooldown(enterWorldResult.Player.ItemCooldowns));
					}

					var workingQuests = enterWorldResult.Player.Quests.Where(quest => !quest.IsComplete).ToArray();
					foreach (var completedQuestPacket in SmQuestCompletedList.CreateLoginPackets(enterWorldResult.Player.Quests))
						await SendPacketAsync(completedQuestPacket);
					await SendPacketAsync(new SmQuestList(workingQuests));
					await SendPacketAsync(new SmTitleInfo(enterWorldResult.Player.TitleId));
					if (enterWorldResult.Player.BonusTitleId != 0)
						await SendPacketAsync(new SmTitleInfo(6, enterWorldResult.Player.BonusTitleId));
					await SendPacketAsync(new SmMotion(enterWorldResult.Player.Motions));
					await SendPacketAsync(new SmAfterTimeCheck475());
					if (enterWorldResult.Player.Settings.UiSettings != null)
						await SendPacketAsync(new SmUiSettings(enterWorldResult.Player.Settings.UiSettings, type: 0));
					if (enterWorldResult.Player.Settings.Shortcuts != null)
						await SendPacketAsync(new SmUiSettings(enterWorldResult.Player.Settings.Shortcuts, type: 1));
					if (enterWorldResult.Player.Settings.HouseBuddies != null)
						await SendPacketAsync(new SmUiSettings(enterWorldResult.Player.Settings.HouseBuddies, type: 2));

					var staticData = _runtimeContext?.DataManager?.StaticData;
					var itemTemplates = staticData?.ItemTemplates;
					if (itemTemplates != null)
					{
						foreach (var inventoryPacket in SmInventoryInfo.CreateLoginPackets(
							enterWorldResult.Player,
							itemTemplates,
							_idFactory == null ? null : () => _idFactory.NextId(),
							staticData?.ItemRestrictionCleanups))
						{
							await SendPacketAsync(inventoryPacket);
						}
					}

					// Java parity: CreatureController.onAfterSpawn revalidates zones after the player enters the world.
					await RevalidatePlayerFlightZonesAsync(enterWorldResult.Player);
					await SendPacketAsync(new SmChannelInfo(enterWorldResult.Player.Position, staticData?.WorldMaps ?? Array.Empty<WorldMapSummary>()));
					var restoredKiskBinding = _runtimeContext?.Kisks.RestoreOfflineBinding(enterWorldResult.Player);
					WorldPosition? restoredKiskPosition = null;
					if (restoredKiskBinding?.Kisk != null && TryGetKiskPosition(restoredKiskBinding.Kisk.ObjectId, out var resolvedKiskPosition))
						restoredKiskPosition = resolvedKiskPosition;
					var kiskLoginRestorePlan = PlayerKiskLoginRestorePacketPlanService.CreatePlan(
						enterWorldResult.Player,
						restoredKiskBinding,
						restoredKiskPosition,
						staticData);
					foreach (var directPacket in kiskLoginRestorePlan.DirectPackets)
						await SendPacketAsync(directPacket);
					if (kiskLoginRestorePlan is
						{ RestoredKisk: not null, RestoredKiskPosition: { } kiskPosition, ShouldBroadcastAddedMemberUpdate: true })
					{
						await BroadcastKiskUpdateAsync(
							kiskLoginRestorePlan.RestoredKisk,
							kiskPosition,
							excludedPlayerObjectId: enterWorldResult.Player.ObjectId);
					}
					await SendPacketAsync(new SmPlayerSpawn(enterWorldResult.Player));
					RegisterLoadedHouses(enterWorldResult.Player, staticData?.HousingTemplates);
					if (_connectionRegistry != null)
					{
						await _connectionRegistry.BroadcastToVisiblePlayersAsync(
							enterWorldResult.Player.Position,
							enterWorldResult.Player.ObjectId,
							new SmPlayerInfo(enterWorldResult.Player, staticData?.PlayerExperienceTable));
						await _connectionRegistry.BroadcastToVisiblePlayersAsync(
							enterWorldResult.Player.Position,
							enterWorldResult.Player.ObjectId,
							new SmMotion(enterWorldResult.Player.ObjectId, enterWorldResult.Player.Motions));
						if (_world != null && staticData?.HousingTemplates != null)
							await _connectionRegistry.RefreshHousingVisibilityAsync(_world.GetHouses(), staticData.HousingTemplates);
						if (_world != null)
							await _connectionRegistry.RefreshNpcVisibilityAsync(_world.GetNpcs(enterWorldResult.Player.Position.WorldId), enterWorldResult.Player.ObjectId);
					}
					await SendPacketAsync(new SmGameTime(_gameTimeService?.GameMinutes ?? 0));
					if (itemTemplates != null)
					{
						foreach (var warehousePacket in SmWarehouseInfo.CreateLoginPackets(
							enterWorldResult.Player,
							itemTemplates,
							itemRestrictionCleanups: staticData?.ItemRestrictionCleanups))
							await SendPacketAsync(warehousePacket);
					}

					await SendPacketAsync(new SmTitleInfo(enterWorldResult.Player.Titles));
					await SendPacketAsync(new SmEmotionList(0, enterWorldResult.Player.Emotions));
					await SendPacketAsync(new SmPrices());
					if (enterWorldResult.Player.CraftCooldowns.Count > 0)
						await SendPacketAsync(new SmRecipeCooldown(enterWorldResult.Player.CraftCooldowns, mode: 1));
					await SendPacketAsync(new SmFriendList(enterWorldResult.Player.Friends, staticData?.PlayerExperienceTable));
					await SendPacketAsync(new SmBlockList(enterWorldResult.Player.BlockedUsers));
					if (staticData?.InstanceCooltimes.Count > 0)
						await SendPacketAsync(new SmInstanceInfo(2, enterWorldResult.Player, staticData.InstanceCooltimes));
					await SendPacketAsync(new SmAbyssRank(enterWorldResult.Player.AbyssRank));
					await SendPacketAsync(
						new SmStatsInfo(
							enterWorldResult.Player,
							staticData?.PlayerExperienceTable,
							_gameTimeService?.GameMinutes ?? 0,
							staticData?.ItemTemplates,
							staticData?.ItemRandomBonuses,
							staticData?.ItemSets,
							staticData?.EnchantTemplates,
							staticData?.TemperingTemplates,
							staticData?.SkillTemplates,
							staticData?.TitleTemplates));
					// Java parity: services/mail/MailService.onPlayerLogin sends mailbox state before macro/recipe restore.
					await SendPacketAsync(new SmMailService(enterWorldResult.Player.Mailbox));
					foreach (var housingBidSystemMessage in SmReceiveBids.CreateLoginSystemMessages(enterWorldResult.Player))
					{
						// Java parity: services/HousingBidService.onPlayerLogin sends auction-result system messages before SM_RECEIVE_BIDS.
						await SendPacketAsync(housingBidSystemMessage);
					}
					var housingBidRefreshPacket = SmReceiveBids.CreateLoginPacket(enterWorldResult.Player);
					if (housingBidRefreshPacket != null)
					{
						// Java parity: services/HousingBidService.onPlayerLogin after mailbox initialization.
						await SendPacketAsync(housingBidRefreshPacket);
					}

					// Java parity: PlayerEnterWorldService.sendMacroList before SM_RECIPE_LIST.
					foreach (var macroPacket in SmMacroList.CreateLoginPackets(enterWorldResult.Player.ObjectId, enterWorldResult.Player.Macros))
						await SendPacketAsync(macroPacket);
					await SendPacketAsync(new SmRecipeList(enterWorldResult.Player.Recipes));
					if (enterWorldResult.Player.BrokerSettlements.HasSettledItems)
					{
						// Java parity: services/BrokerService.onPlayerLogin settled-icon notification.
						await SendPacketAsync(new SmBrokerService(enterWorldResult.Player.BrokerSettlements.EarnedKinah));
					}

					foreach (var housingSystemMessage in SmHouseOwnerInfo.CreateLoginSystemMessages(enterWorldResult.Player, _options.Housing.PayEnabled))
					{
						// Java parity: services/HousingService.onPlayerLogin sends maintenance/sequestration notices before house owner info.
						await SendPacketAsync(housingSystemMessage);
					}
					// Java parity: services/HousingService.onPlayerLogin sends house owner profile info.
					await SendPacketAsync(new SmHouseOwnerInfo(enterWorldResult.Player, auctionEndSchedule: _houseAuctionTiming.AuctionEndSchedule));
					if (staticData != null)
					{
						// Java parity: services/player/PlayerEnterWorldService.onLogin calls Equipment.checkRankLimitItems before expirable registration.
						var loginRankLimitChange = EquipmentService.CheckRankLimitItems(enterWorldResult.Player, staticData.ItemTemplates);
						if (loginRankLimitChange.Changed || loginRankLimitChange.RankLimitedUnequipMessages.Count > 0)
							await ApplyEquipmentChangeAsync(enterWorldResult.Player, loginRankLimitChange, staticData.ItemTemplates, staticData);
					}
					_expirableTaskService?.RegisterPlayerExpirables(
						enterWorldResult.Player,
						packet => SendPacketAsync(packet),
						async packet =>
						{
							if (_connectionRegistry != null)
							{
								await _connectionRegistry.BroadcastToVisiblePlayersAsync(
									enterWorldResult.Player.Position,
									enterWorldResult.Player.ObjectId,
									packet,
									includeSourcePlayer: true);
							}
						},
						staticData?.TitleTemplates,
						staticData?.ItemTemplates,
						staticData?.HousingObjectTemplates,
						(house, houseObject, template) => ExpireHouseObjectAsync(enterWorldResult.Player, house, houseObject, template),
						CanRuntimeHouseObjectExpireNow);
				}
				break;
		}
	}

	internal async Task HandleLevelReadyAsync(Player player)
	{
		// Java parity: network/aion/clientpackets/CM_LEVEL_READY.runImpl baseline packets after client map load.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var activeHouse = GetActiveHouse(player);
		if (activeHouse != null)
		{
			var registry = await LoadHouseRegistryAsync(player, activeHouse);
			var spawnedObjects = registry.GetSpawnedObjects(activeHouse, player.ObjectId);
			if (spawnedObjects.Count > 0)
				await SendPacketAsync(new SmHouseObjects(spawnedObjects));
		}

		// Java parity: CM_LEVEL_READY.runImpl sends SM_INSTANCE_COUNT_INFO when player.isInInstance().
		// isInInstance() checks WorldMap.isInstanceType(); C# uses WorldMapSummary.IsInstance flag.
		if (staticData?.WorldMaps.Any(m => m.MapId == player.Position.WorldId && m.IsInstance) == true)
			await SendPacketAsync(new SmInstanceCountInfo(player.Position.WorldId, player.Position.InstanceId));

		await SendPacketAsync(new SmPlayerInfo(player, staticData?.PlayerExperienceTable));
		player.PortAnimation = ArrivalAnimation.None;
		await SendPacketAsync(CreateAccountPropertiesPacket());
		await SendPacketAsync(new SmMotion(player.ObjectId, player.Motions));

		// Java parity: CM_LEVEL_READY.runImpl sends SM_WINDSTREAM_ANNOUNCE for each location in the windstream template for the player's map.
		if (staticData?.WindstreamLocations != null)
		{
			foreach (var loc in staticData.WindstreamLocations.GetByMapId(player.Position.WorldId))
				await SendPacketAsync(new SmWindstreamAnnounce(loc.FlyPathId, loc.MapId, loc.StreamId, loc.State));
		}

		// Java parity: CM_LEVEL_READY.runImpl spawns the already-moved player into World, then later player.getController().onEnterWorld updates zone state.
		RevalidatePlayerCreaturePvpZones(player, staticData);
		await PlayerLevelReadyFlightNotifier.NotifyIfFlyingAsync(
			player,
			_connectionRegistry,
			_connectionRegistry == null
				? null
				: new PlayerVisualStatsUpdateService(_connectionRegistry, _runtimeContext, _gameTimeService));

		// Java parity: CM_LEVEL_READY.runImpl -> PlayerController.updateNearbyQuests -> SM_NEARBY_QUESTS.
		// Sends quest markers for quests available on the player's current map/instance.
		var worldMapStates = _runtimeContext?.WorldMapStates;
		if (worldMapStates != null && staticData?.NearbyQuestTemplates != null)
		{
			worldMapStates.TryGetWorldMapInstance(player.Position.WorldId, player.Position.InstanceId, out var mapInstance);
			var nearbyPlan = NearbyQuestRefreshPlanService.CreatePlan(player, mapInstance, staticData.NearbyQuestTemplates);
			var packetPlan = NearbyQuestRefreshPlanService.CreatePacketFactoryPlan(nearbyPlan);
			if (packetPlan.Packet != null)
				await SendPacketAsync(packetPlan.Packet);
		}

		await SendPacketAsync(SmCubeUpdate.CubeSize(player));
	}

	private SmAccountProperties CreateAccountPropertiesPacket()
	{
		// Java parity: network/aion/serverpackets/SM_ACCOUNT_PROPERTIES uses AdminConfig.GM_PANEL.
		return new SmAccountProperties(_accessLevel >= _options.Administration.GmPanelAccessLevel);
	}

	private async Task HandleAutoGroupAsync(Player player, CmAutoGroup packet)
	{
		if (!_options.AutoGroup.Enabled)
		{
			// Java parity: CM_AUTO_GROUP.runImpl checks AutoGroupConfig.AUTO_GROUP_ENABLE
			// before window dispatch and sends PacketSendUtility.sendMessage.
			await SendPacketAsync(new SmMessage("Auto Group is disabled"));
			return;
		}

		switch (packet.WindowId)
		{
			case 100:
			{
				// Java parity: CM_AUTO_GROUP.runImpl window 100 resolves EntryRequestType.getTypeById
				// before AutoGroupService.startLooking. Other windows remain deferred.
				var entryRequestType = AutoGroupEntryRequestTypeParser.GetTypeById(packet.EntryRequestId);
				if (entryRequestType == null)
					return;

				var autoGroups = _runtimeContext?.DataManager?.StaticData.AutoGroups;
				var instanceCooltimes = _runtimeContext?.DataManager?.StaticData.InstanceCooltimes;
				var result = _autoGroupLookingPartyRegistrations.StartLooking(
					player,
					packet.InstanceMaskId,
					entryRequestType.Value,
					autoGroups,
					_playerGroupRuntime,
					_playerAllianceRuntime,
					instanceCooltimes,
					announceBattlegroundRegistrations: _options.AutoGroup.AnnounceBattlegroundRegistrations,
					tryAddOpenQuickEntry: request => _autoGroupInstanceLeaveRuntimeService.TryAddOpenQuickEntry(request));

				if (result.GuardPlan != null)
				{
					foreach (var memberDenial in result.GuardPlan.MemberDenials)
					{
						if (_connectionRegistry != null)
							await _connectionRegistry.SendPacketToPlayerAsync(memberDenial.MemberObjectId, memberDenial.Message);
					}
				}

				if (result.GuardPlan?.DenialMessage != null)
					await SendPacketAsync(result.GuardPlan.DenialMessage);
				else if (result.Status == AutoGroupStartLookingStatus.AlreadyRegistered)
				{
					// Java parity: AutoGroupService.startLooking duplicate branch sends
					// STR_MSG_CANT_INSTANCE_ALREADY_REGISTERED(agt.getTemplate().getInstanceMapId()).
					var autoGroup = autoGroups?.GetTemplateByInstanceMaskId(packet.InstanceMaskId);
					if (autoGroup != null)
						await SendPacketAsync(SmSystemMessage.CantInstanceAlreadyRegistered(autoGroup.InstanceMapId));
				}
				else if (result.Status == AutoGroupStartLookingStatus.Registered && result.Registration != null)
				{
					var autoGroup = autoGroups?.GetTemplateByInstanceMaskId(packet.InstanceMaskId);
					if (autoGroup != null)
					{
						await SendAutoGroupSuccessfulRegistrationAsync(player, result.Registration, autoGroup, entryRequestType.Value);
						await BroadcastAutoGroupBattlegroundRegistrationAnnouncementAsync(result.BattlegroundAnnouncement);
						if (result.OpenQuickEntry != null && _connectionRegistry != null)
						{
							foreach (var delivery in result.OpenQuickEntry.WindowDeliveries)
							{
								var deliveryAutoGroup = autoGroups?.GetTemplateByInstanceMaskId(delivery.MaskId);
								if (deliveryAutoGroup != null)
									await _connectionRegistry.SendPacketToPlayerAsync(delivery.PlayerObjectId, new SmAutoGroup(deliveryAutoGroup, delivery.WindowId));
							}

							ScheduleAutoGroupPenaltyRefreshes(result.OpenQuickEntry.PenaltyRefreshIntents);
						}

						if (result.QueueMatchPlan?.Status == AutoGroupQueueMatchPlanStatus.Ready && _connectionRegistry != null)
							await ApplyAutoGroupReadyMatchPlanAsync(
								result.QueueMatchPlan,
								autoGroups,
								_runtimeContext?.DataManager?.StaticData.InstanceCooltimes,
								_connectionRegistry);
					}
				}
				break;
			}
			case 101:
				if (_connectionRegistry != null)
				{
					var cancelRegistration = await _autoGroupLookingPartyRegistrations.CancelRegistrationAsync(
						player.ObjectId,
						packet.InstanceMaskId,
						_runtimeContext?.DataManager?.StaticData.AutoGroups,
						_connectionRegistry);
					ScheduleAutoGroupPenaltyRefreshes(cancelRegistration.PenaltyRefreshIntents);
				}
				break;
			case 102:
			{
				var pressEnter = _autoGroupInstanceLeaveRuntimeService.PressEnter(player, packet.InstanceMaskId);
				if (pressEnter.Status != AutoGroupInstancePressEnterStatus.ReadyToEnter)
					return;

				var staticData = _runtimeContext?.DataManager?.StaticData;
				var instanceCooltimes = staticData?.InstanceCooltimes
					?? new InstanceCooltimeTable(Array.Empty<InstanceCooltimeSummary>());
				await ApplyInstanceEntranceCooldownAsync(player, pressEnter.WorldId, reenter: false, instanceCooltimes);

				var autoGroup = staticData?.AutoGroups.GetTemplateByInstanceMaskId(packet.InstanceMaskId);
				if (autoGroup != null)
					await SendPacketAsync(new SmAutoGroup(autoGroup, windowId: 5));
				break;
			}
			case 103:
			{
				var staticData = _runtimeContext?.DataManager?.StaticData;
				await ApplyAutoGroupCancelEnterAsync(player, packet.InstanceMaskId, staticData?.AutoGroups, staticData?.InstanceCooltimes);
				break;
			}
			case 104:
			{
				var requestPacket = _periodicInstanceRegistrations.CreateRequestPacket(
					player,
					packet.InstanceMaskId,
					_runtimeContext?.DataManager?.StaticData.AutoGroups);
				if (requestPacket != null)
					await SendPacketAsync(requestPacket);
				break;
			}
			case 105:
				// Java parity: CM_AUTO_GROUP.runImpl window 105 only contains a commented-out
				// DredgionRegService.failedEnterDredgion call and has no active side effect.
				break;
		}
	}

	private async Task ApplyAutoGroupCancelEnterAsync(
		Player player,
		int instanceMaskId,
		AutoGroupTable? autoGroups,
		InstanceCooltimeTable? instanceCooltimes)
	{
		var cancelEnter = _autoGroupInstanceLeaveRuntimeService.CancelEnter(player, instanceMaskId);
		if (cancelEnter.Status != AutoGroupInstanceCancelEnterStatus.Unregistered)
			return;
		ScheduleAutoGroupPenaltyRefreshes(cancelEnter.PenaltyRefreshIntents);

		if (_connectionRegistry != null
			&& cancelEnter.Snapshot?.QuickRegistrationAllowed == true
			&& cancelEnter.RegisteredPlayerCountAfterCancel > 0)
		{
			var refill = _autoGroupLookingPartyRegistrations.TryRefillQueuedQuickEntry(
				instanceMaskId,
				autoGroups,
				instanceCooltimes,
				request => _autoGroupInstanceLeaveRuntimeService.TryAddOpenQuickEntry(request));
			if (refill != null)
			{
				foreach (var delivery in refill.WindowDeliveries)
				{
					var deliveryAutoGroup = autoGroups?.GetTemplateByInstanceMaskId(delivery.MaskId);
					if (deliveryAutoGroup != null)
						await _connectionRegistry.SendPacketToPlayerAsync(delivery.PlayerObjectId, new SmAutoGroup(deliveryAutoGroup, delivery.WindowId));
				}

				ScheduleAutoGroupPenaltyRefreshes(refill.PenaltyRefreshIntents);
			}
		}

		var autoGroup = autoGroups?.GetTemplateByInstanceMaskId(instanceMaskId);
		if (autoGroup != null)
			await SendPacketAsync(new SmAutoGroup(autoGroup, windowId: 2));
	}

	private void ScheduleAutoGroupPenaltyRefreshes(IReadOnlyList<AutoGroupPenaltyRefreshIntent> penaltyRefreshIntents)
	{
		if (penaltyRefreshIntents.Count == 0)
			return;

		if (_connectionRegistry != null)
			_autoGroupPenaltyRefreshScheduler?.ScheduleRefreshes(penaltyRefreshIntents, _connectionRegistry);
	}

	private async Task ApplyAutoGroupReadyMatchPlanAsync(
		AutoGroupQueueMatchPlan queueMatchPlan,
		AutoGroupTable? autoGroups,
		InstanceCooltimeTable? instanceCooltimes,
		IGameClientConnectionRegistry connectionRegistry,
		ISet<int>? recheckedMaskIds = null)
	{
		if (queueMatchPlan.Status != AutoGroupQueueMatchPlanStatus.Ready)
			return;

		recheckedMaskIds ??= new HashSet<int>();
		var scheduledPenaltyRefreshPlayerObjectIds = new HashSet<int>();
		var readyMatchPlan = _autoGroupLookingPartyRegistrations.CreateReadyMatchPlan(queueMatchPlan);
		var applyResult = await _autoGroupLookingPartyRegistrations.ApplyReadyMatchPlanAsync(
			readyMatchPlan,
			autoGroups,
			connectionRegistry,
			registerRuntimeInstance: registration => _autoGroupInstanceLeaveRuntimeService.RegisterInstance(registration),
			materializeRuntimeInstance: MaterializeAutoGroupReadyMatchRuntimeInstance,
			beforeCleanupWindowDeliveryAsync: cleanupIntent =>
			{
				if (!cleanupIntent.WouldPenaliseParty)
					return Task.CompletedTask;

				var immediatePenaltyRefreshes = CreateAutoGroupPenaltyRefreshIntents(cleanupIntent)
					.Where(intent => scheduledPenaltyRefreshPlayerObjectIds.Add(intent.PlayerObjectId))
					.ToArray();
				ScheduleAutoGroupPenaltyRefreshes(immediatePenaltyRefreshes);
				return Task.CompletedTask;
			},
			afterCleanupWindowDeliveryAsync: async cleanupIntent =>
			{
				if (cleanupIntent.WouldPenalisePlayer)
				{
					var immediatePenaltyRefreshes = CreateAutoGroupPenaltyRefreshIntents(cleanupIntent)
						.Where(intent => scheduledPenaltyRefreshPlayerObjectIds.Add(intent.PlayerObjectId))
						.ToArray();
					ScheduleAutoGroupPenaltyRefreshes(immediatePenaltyRefreshes);
				}

				if (!cleanupIntent.WouldRecheckQueueForNewMatches || !recheckedMaskIds.Add(cleanupIntent.MaskId))
					return;

				var recheckPlan = _autoGroupLookingPartyRegistrations.CreateQueueMatchPlan(
					cleanupIntent.MaskId,
					autoGroups,
					instanceCooltimes);
				await ApplyAutoGroupReadyMatchPlanAsync(
					recheckPlan,
					autoGroups,
					instanceCooltimes,
					connectionRegistry,
					recheckedMaskIds);
			});
		var remainingPenaltyRefreshes = applyResult.PenaltyRefreshIntents
			.Where(intent => scheduledPenaltyRefreshPlayerObjectIds.Add(intent.PlayerObjectId))
			.ToArray();
		ScheduleAutoGroupPenaltyRefreshes(remainingPenaltyRefreshes);
	}

	private static IReadOnlyList<AutoGroupPenaltyRefreshIntent> CreateAutoGroupPenaltyRefreshIntents(
		AutoGroupAdditionalRegistrationCleanupIntent cleanupIntent)
	{
		if (cleanupIntent.WouldPenaliseParty)
		{
			return cleanupIntent.NotifiedMemberObjectIds
				.Select(AutoGroupLookingPartyRegistrationService.CreatePenaltyRefreshIntent)
				.ToArray();
		}

		if (cleanupIntent.WouldPenalisePlayer)
			return [AutoGroupLookingPartyRegistrationService.CreatePenaltyRefreshIntent(cleanupIntent.PlayerObjectId)];

		return Array.Empty<AutoGroupPenaltyRefreshIntent>();
	}

	private AutoGroupInstanceRuntimeRegistration MaterializeAutoGroupReadyMatchRuntimeInstance(
		AutoGroupInstanceRuntimeRegistration registration)
	{
		var worldMapStates = _runtimeContext?.WorldMapStates;
		if (worldMapStates == null)
			return registration;

		try
		{
			// Java parity: AutoGroupService.createNewInstance calls InstanceService.getNextAvailableInstance(...)
			// before AutoInstance.onInstanceCreate(instance) stores the map instance and startInstanceTime.
			var instance = InstanceRuntimeService.GetNextAvailableInstance(
				worldMapStates,
				registration.WorldId,
				ownerId: 0,
				maxPlayers: registration.RegisteredPlayerObjectIds.Count,
				difficultyId: registration.DifficultyId,
				instanceHandler: null,
				autoDestroy: false);
			instance.NotifyInstanceCreated();
			return registration with
			{
				InstanceId = instance.InstanceId,
				StartInstanceTime = registration.ReadyEnterStartTime ?? DateTimeOffset.UtcNow,
			};
		}
		catch (InvalidOperationException)
		{
			return registration;
		}
	}

	private async Task BroadcastAutoGroupBattlegroundRegistrationAnnouncementAsync(
		AutoGroupBattlegroundRegistrationAnnouncement? announcement)
	{
		if (announcement == null || _connectionRegistry == null)
			return;

		await _connectionRegistry.BroadcastToWorldAsync(
			new SmMessage(
				senderObjectId: 0,
				senderName: null,
				announcement.Message,
				AutoGroupBattlegroundRegistrationAnnouncement.BrightYellowCenterChatType),
			announcement.ShouldReceive);
	}

	private async Task SendAutoGroupSuccessfulRegistrationAsync(
		Player leader,
		AutoGroupLookingPartyRegistration registration,
		AutoGroupSummary autoGroup,
		AutoGroupEntryRequestType entryRequestType)
	{
		// Java parity: AutoGroupUtility.sendSuccessfulRegistration iterates queued
		// member object ids, skips offline players, then sends optional periodic
		// close icon, success system message, and waiting-window packet.
		foreach (var memberObjectId in registration.MemberObjectIds)
		{
			foreach (var packet in CreateAutoGroupSuccessfulRegistrationPackets(autoGroup, entryRequestType, leader.Name))
			{
				if (_connectionRegistry != null)
					await _connectionRegistry.SendPacketToPlayerAsync(memberObjectId, packet);
				else if (memberObjectId == leader.ObjectId)
					await SendPacketAsync(packet);
			}
		}
	}

	private static IReadOnlyList<GameServerPacket> CreateAutoGroupSuccessfulRegistrationPackets(
		AutoGroupSummary autoGroup,
		AutoGroupEntryRequestType entryRequestType,
		string leaderName)
	{
		var packets = new List<GameServerPacket>(autoGroup.IsPeriodicInstance ? 3 : 2);
		if (autoGroup.IsPeriodicInstance)
			packets.Add(new SmAutoGroup(autoGroup, SmAutoGroup.EntryIconWindowId, close: true));
		packets.Add(SmSystemMessage.InstanceRegisterSuccess());
		packets.Add(new SmAutoGroup(autoGroup, windowId: 1, requestTypeId: (int)entryRequestType, name: leaderName));
		return packets;
	}

	private AbyssPointsAddOptions CreateAbyssPointsOptions(long currentLegionContributionPoints = 0)
	{
		return new AbyssPointsAddOptions(
			CurrentLegionContributionPoints: currentLegionContributionPoints,
			EnableApCap: _options.Custom.EnableApCap,
			ApCapValue: _options.Custom.ApCapValue);
	}

	private async Task HandlePingAsync(CmPing packet)
	{
		// Java parity: network/aion/clientpackets/CM_PING.runImpl sends SM_PONG and audits the client ping interval.
		var now = DateTimeOffset.UtcNow;
		if (_lastPingTime is { } lastPingTime && now - lastPingTime < ClientPingInterval)
		{
			_logger.LogTrace(
				"Client {ClientId} sent CM_PING value {PingValue} after {ElapsedMilliseconds}ms before ping-kick parity is ported",
				_clientId,
				packet.Unknown,
				(now - lastPingTime).TotalMilliseconds);
		}

		_lastPingTime = now;
		await SendPacketAsync(new SmPong());
	}

	internal async Task<PlayerCastSpellEarlyExitResult> HandleCastSpellAsync(Player player, CmCastSpell packet)
	{
		// Java parity: network/aion/clientpackets/CM_CASTSPELL.runImpl early exits before full SkillEngine useSkill dispatch.
		var packets = new List<GameServerPacket>();
		var result = _castSpellEarlyExitService.Evaluate(
			player,
			packet,
			new PlayerCastSpellEarlyExitOptions(
				IsPetOrderSkill: skillId => IsCastSpellPetOrderSkill(player, skillId),
				HasPetSummon: HasCastSpellPetSummon(player),
				GetSkillTemplate: skillId => ResolveCastSpellSkillTemplate(player, skillId),
				NextSkillUseMilliseconds: _castSpellHooks.GetNextSkillUseMilliseconds(player),
				CurrentTimeMilliseconds: _castSpellHooks.GetCurrentTimeMilliseconds(),
				LastSkillId: _castSpellHooks.GetLastSkillId(player),
				SendSkillCannotCastDead: () => packets.Add(SmSystemMessage.SkillCannotCastDead()),
				CancelCurrentSkill: () =>
				{
					var canceledSkill = CancelCurrentSkillForCastSpell(player);
					if (canceledSkill?.Method == PlayerCastingSkillMethod.Cast)
					{
						packets.Add(new SmSkillCancel(player.ObjectId, canceledSkill.SkillId));
						packets.Add(SmSystemMessage.SkillCanceled());
					}
					else if (canceledSkill is { Method: PlayerCastingSkillMethod.Item, HasItemCancellationMetadata: true })
					{
						packets.Add(SmSystemMessage.ItemCanceled());
						if (canceledSkill.ItemCooldownDelayId.HasValue)
							player.RemoveItemCooldown(canceledSkill.ItemCooldownDelayId.Value);
						packets.Add(new SmItemUsageAnimation(
							player.ObjectId,
							canceledSkill.FirstTargetObjectId,
							canceledSkill.ItemObjectId,
							canceledSkill.ItemTemplateId,
							0,
							3,
							0,
							0,
							1,
							0));
					}
					_castSpellHooks.CancelCurrentSkill(player, packet);
				},
				SendPetRequired: () => packets.Add(SmSystemMessage.SkillNotNeedPet()),
				StopProtection: () => _castSpellHooks.StopProtection(player),
				CancelUseItem: () =>
				{
					CancelUseItemForCastSpell(player);
					_castSpellHooks.CancelUseItem(player);
				},
				AuditCooldown: (skillId, delta, lastSkillId) => _castSpellHooks.AuditCooldown(player, skillId, delta, lastSkillId),
				SendSkillNotReady: () => packets.Add(SmSystemMessage.SkillNotReady()),
				UseSkill: (template, request) => _castSpellHooks.UseSkill(player, template, request)));

		foreach (var packetToSend in packets)
			await SendPacketAsync(packetToSend);

		return result;
	}

	private PlayerCastSpellSkillTemplate? ResolveCastSpellSkillTemplate(Player player, int skillId)
	{
		var hookTemplate = _castSpellHooks.GetSkillTemplate(player, skillId);
		if (hookTemplate != null)
			return hookTemplate;

		var staticTemplate = _runtimeContext?.DataManager?.StaticData.SkillTemplates.GetSkillTemplate(skillId);
		return staticTemplate == null
			? null
			: new PlayerCastSpellSkillTemplate(staticTemplate.SkillId, staticTemplate.IsPassive);
	}

	private bool IsCastSpellPetOrderSkill(Player player, int skillId)
	{
		return _castSpellHooks.IsPetOrderSkill(player, skillId)
			|| (_runtimeContext?.DataManager?.StaticData.PetSkills.IsPetOrderSkill(skillId) ?? false);
	}

	private bool HasCastSpellPetSummon(Player player)
	{
		// Java parity: CM_CASTSPELL checks player.getSummon() != null && player.getSummon().isPet().
		return _castSpellHooks.HasPetSummon(player) || player.HasPetSummon;
	}

	internal async Task<PlayerSummonCastSpellConnectionResult> HandleSummonCastSpellAsync(Player player, CmSummonCastSpell packet)
	{
		// Java parity: network/aion/clientpackets/CM_SUMMON_CASTSPELL.runImpl represented pet-summon branch.
		var castResult = _summonCastSpellService.Handle(player, packet);
		if (castResult.Status == PlayerSummonCastSpellStatus.PetRequired)
		{
			await SendPacketAsync(SmSystemMessage.SkillNotNeedPet());
			return new PlayerSummonCastSpellConnectionResult(castResult, ExecutionResult: null);
		}

		if (castResult.Status == PlayerSummonCastSpellStatus.MercenaryReady)
		{
			var staticData = _runtimeContext?.DataManager?.StaticData;
			var mercenaryPetSkills = staticData?.PetSkills;
			if (mercenaryPetSkills == null)
				return new PlayerSummonCastSpellConnectionResult(castResult, ExecutionResult: null);

			var mercenaryExecutionResult = _summonSkillExecutionService.ValidateMercenaryExecution(
				player,
				packet,
				mercenaryPetSkills,
				castResult.ResolvedTarget);
			if (staticData?.SkillTemplates != null)
			{
				mercenaryExecutionResult = mercenaryExecutionResult with
				{
					InvocationExecution = _summonSkillExecutionService.PlanInvocationExecution(
						mercenaryExecutionResult.InvocationPlan,
						staticData.SkillTemplates,
						player),
				};
			}

			return new PlayerSummonCastSpellConnectionResult(
				castResult,
				ExecutionResult: null,
				MercenaryExecutionResult: mercenaryExecutionResult);
		}

		if (castResult.Status != PlayerSummonCastSpellStatus.Executed || castResult.ExecutedOrder == null)
			return new PlayerSummonCastSpellConnectionResult(castResult, ExecutionResult: null);

		var summonStaticData = _runtimeContext?.DataManager?.StaticData;
		var petSkills = summonStaticData?.PetSkills;
		if (petSkills == null)
			return new PlayerSummonCastSpellConnectionResult(castResult, ExecutionResult: null);

		var executionResult = _summonSkillExecutionService.ValidateExecution(
			player,
			castResult.ExecutedOrder,
			petSkills,
			castResult.ResolvedTarget);
		if (summonStaticData?.SkillTemplates != null)
		{
			executionResult = executionResult with
			{
				InvocationExecution = _summonSkillExecutionService.PlanInvocationExecution(
					executionResult.InvocationPlan,
					summonStaticData.SkillTemplates,
					player),
			};
		}

		return new PlayerSummonCastSpellConnectionResult(castResult, executionResult);
	}

	private static PlayerCastingSkillSnapshot? CancelCurrentSkillForCastSpell(Player player)
	{
		// Java parity: CM_CASTSPELL spell id 0 -> PlayerController.cancelCurrentSkill(null) -> Player.setCasting(null).
		return player.ClearCastingSkill();
	}

	private void CancelUseItemForCastSpell(Player player)
	{
		// Java parity: CM_CASTSPELL.runImpl -> PlayerController.cancelUseItem clears Player.usingItem and cancels TaskId.ITEM_USE.
		var pendingItemUse = _pendingItemUse;
		if (pendingItemUse != null && pendingItemUse.Task.Cancel())
		{
			_pendingItemUse = null;
			CleanupPendingItemUse(player, pendingItemUse, canceled: true);
			return;
		}

		player.UsingItemObjectId = 0;
	}

	private async Task HandlePublicChatAsync(Player player, CmChatMessagePublic packet)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_MESSAGE_PUBLIC.runImpl normal/shout broadcast branch.
		var message = packet.Message;
		if (string.IsNullOrWhiteSpace(message))
			return;

		switch (packet.ChatType)
		{
			case 0:
			case 3:
				var chatPacket = new SmMessage(player, message, packet.ChatType);
				if (_connectionRegistry == null)
				{
					await SendPacketAsync(chatPacket);
					return;
				}

				await _connectionRegistry.BroadcastToVisiblePlayersAsync(
					player.Position,
					player.ObjectId,
					chatPacket,
					includeSourcePlayer: true,
					receiver => IsVisiblePublicChatRecipient(player, receiver));
				break;
			default:
				_logger.LogDebug(
					"Player {PlayerObjectId} sent unported public chat type {ChatType}",
					player.ObjectId,
					packet.ChatType);
				break;
		}
	}

	private static bool IsVisiblePublicChatRecipient(Player sender, Player receiver)
	{
		// Java parity: CM_CHAT_MESSAGE_PUBLIC.broadcastToPlayers skips blockers except staff.
		return sender.AccessLevel > 0
			|| receiver.AccessLevel > 0
			|| receiver.BlockedUsers.All(blockedUser => blockedUser.ObjectId != sender.ObjectId);
	}

	private async Task HandleWhisperChatAsync(Player sender, CmChatMessageWhisper packet)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_MESSAGE_WHISPER.runImpl.
		var recipientName = GetRealCharacterName(packet.RecipientName);
		if (recipientName.Length == 0 || _connectionRegistry == null || !_connectionRegistry.TryGetOnlinePlayerByName(recipientName, out var receiver) || receiver == null)
		{
			await SendPacketAsync(SmSystemMessage.NoSuchUser(recipientName));
			return;
		}

		var requiredLevel = _options.Custom.LevelToWhisper;
		var senderLevel = Math.Max(1, _runtimeContext?.DataManager?.StaticData.PlayerExperienceTable.GetLevelForExp(sender.Exp) ?? 1);
		if (senderLevel < requiredLevel && receiver.AccessLevel == 0)
		{
			await SendPacketAsync(SmSystemMessage.CantWhisperLevel(requiredLevel));
			return;
		}

		if (receiver.BlockedUsers.Any(blockedUser => blockedUser.ObjectId == sender.ObjectId))
		{
			await SendPacketAsync(SmSystemMessage.YouExcluded(receiver.Name));
			return;
		}

		if (!string.Equals(sender.Race, receiver.Race, StringComparison.OrdinalIgnoreCase)
			&& !_options.Custom.SpeakingBetweenFactions
			&& sender.AccessLevel == 0
			&& receiver.AccessLevel == 0)
		{
			await SendPacketAsync(SmSystemMessage.CantWhisperOtherRace());
			return;
		}

		if (string.IsNullOrWhiteSpace(packet.Message))
			return;

		await _connectionRegistry.SendPacketToPlayerAsync(receiver.ObjectId, new SmMessage(sender, packet.Message, 4));
	}

	private async Task HandleChatPlayerInfoAsync(Player requester, CmChatPlayerInfo packet)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_PLAYER_INFO.runImpl.
		var targetName = GetRealCharacterName(packet.PlayerName);
		if (_connectionRegistry == null || !_connectionRegistry.TryGetOnlinePlayerByName(targetName, out var target) || target == null)
		{
			await SendPacketAsync(SmSystemMessage.NoSuchUser(packet.PlayerName));
			return;
		}

		if (!WorldVisibility.IsVisibleTo(requester, target.Position))
			await SendPacketAsync(CreateChatWindowPacket(target, isGroup: false));
	}

	private async Task HandleChatGroupInfoAsync(CmChatGroupInfo packet)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_GROUP_INFO.runImpl.
		var targetName = GetRealCharacterName(packet.PlayerName);
		if (_connectionRegistry == null || !_connectionRegistry.TryGetOnlinePlayerByName(targetName, out var target) || target == null)
		{
			await SendPacketAsync(SmSystemMessage.NoSuchUser(packet.PlayerName));
			return;
		}

		await SendPacketAsync(CreateChatWindowPacket(target, isGroup: true));
	}

	private async Task HandleChatAuthAsync(Player player, CmChatAuth packet)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_AUTH.runImpl -> ChatServer.sendPlayerLoginRequest.
		if (_chatServer == null || !_chatServer.IsAuthed)
		{
			_logger.LogDebug("Ignoring CM_CHAT_AUTH for player {PlayerObjectId} because the chat-server bridge is not authenticated", player.ObjectId);
			return;
		}

		await _chatServer.SendPlayerLoginRequestAsync(
			player,
			_accountName.Length == 0 ? $"account-{_accountId}" : _accountName,
			token => SendPacketAsync(new SmChatInit(token)));
	}

	private async Task HandleMacroCreateAsync(Player player, CmMacroCreate packet)
	{
		// Java parity: network/aion/clientpackets/CM_MACRO_CREATE.runImpl -> PlayerService.addMacro.
		if (packet.MacroPosition is < 1 or > 12)
			return;

		if (_playerEnterWorldService != null)
			await _playerEnterWorldService.SaveMacroAsync(player, packet.MacroPosition, packet.MacroXml);
		else
			player.Macros = player.Macros
				.Where(macro => macro.Id != packet.MacroPosition)
				.Append(new PlayerMacro(packet.MacroPosition, packet.MacroXml))
				.OrderBy(macro => macro.Id)
				.ToArray();
		await SendPacketAsync(SmMacroResult.Created);
	}

	private async Task HandleMacroDeleteAsync(Player player, CmMacroDelete packet)
	{
		// Java parity: network/aion/clientpackets/CM_MACRO_DELETE.runImpl -> PlayerService.removeMacro.
		if (packet.MacroPosition is < 1 or > 12)
			return;

		if (_playerEnterWorldService != null)
			await _playerEnterWorldService.DeleteMacroAsync(player, packet.MacroPosition);
		else
			player.Macros = player.Macros
				.Where(macro => macro.Id != packet.MacroPosition)
				.OrderBy(macro => macro.Id)
				.ToArray();
		await SendPacketAsync(SmMacroResult.Deleted);
	}

	private async Task HandleRecipeDeleteAsync(Player player, CmRecipeDelete packet)
	{
		// Java parity: network/aion/clientpackets/CM_RECIPE_DELETE.runImpl -> RecipeList.deleteRecipe.
		if (!player.Recipes.Contains(packet.RecipeId))
			return;

		var deleted = _playerEnterWorldService == null
			? DeleteRecipeInMemory(player, packet.RecipeId)
			: await _playerEnterWorldService.DeleteRecipeAsync(player, packet.RecipeId);
		if (deleted)
			await SendPacketAsync(new SmRecipeDelete(packet.RecipeId));
	}

	private static bool DeleteRecipeInMemory(Player player, int recipeId)
	{
		var beforeCount = player.Recipes.Count;
		player.Recipes = player.Recipes.Where(existing => existing != recipeId).ToArray();
		return player.Recipes.Count != beforeCount;
	}

	internal async Task HandleDialogSelectAsync(Player player, CmDialogSelect packet)
	{
		// Java parity: network/aion/clientpackets/CM_DIALOG_SELECT.runImpl -> NpcController.onDialogSelect.
		if (player.IsTrading)
			return;

		if (packet.DialogActionId == CmDialogSelect.InstanceEntry
			&& IsBeshmundirsWalkTarget(packet.TargetObjectId))
		{
			if (player.TeamMembership != PlayerTeamMembership.Group)
			{
				await SendPacketAsync(SmSystemMessage.EnterOnlyPartyDon());
				return;
			}

			if (_playerGroupRuntime.IsLeader(player.CurrentTeamId, player))
			{
				await SendPacketAsync(new SmDialogWindow(packet.TargetObjectId, 4762));
				return;
			}

			if (!IsBeshmundirsWalkGroupMemberInInstance(player))
			{
				await SendPacketAsync(SmSystemMessage.InstanceDungeonCantEnterNotOpened());
				return;
			}

			await HandleBeshmundirsWalkMoveToInstanceAsync(player, packet.TargetObjectId);
			return;
		}

		if (IsBeshmundirsWalkDifficultySelection(packet.DialogActionId)
			&& IsBeshmundirsWalkTarget(packet.TargetObjectId))
		{
			await HandleBeshmundirsWalkDifficultySelectionAsync(player, packet.TargetObjectId, packet.DialogActionId);
			return;
		}

		if (packet.DialogActionId == CmDialogSelect.InstancePartyMatch)
		{
			var autoGroup = _findGroupConnectionClientActionCompositionPlanService
				?.ResolvePortalInstancePartyMatch(player, packet.TargetObjectId);
			if (autoGroup != null)
				await SendPacketAsync(new SmAutoGroup(autoGroup));
			await SendPacketAsync(new SmDialogWindow(packet.TargetObjectId, 0));
			return;
		}

		if (packet.DialogActionId == CmDialogSelect.OpenInstanceRecruit)
		{
			var portalPlan = _findGroupConnectionClientActionCompositionPlanService
				?.CreatePortalInstanceGroupShowPlan(player, packet.TargetObjectId);
			if (portalPlan?.EnableRegisterForInstancesIntent?.Packet != null)
				await SendPacketAsync(portalPlan.EnableRegisterForInstancesIntent.Packet);
			return;
		}

		if (packet.DialogActionId == CmDialogSelect.Select1_1
			&& _findGroupConnectionClientActionCompositionPlanService
				?.ShouldShowOpenInstanceRecruitDialog(player, packet.TargetObjectId) == true)
		{
			await SendPacketAsync(new SmDialogWindow(packet.TargetObjectId, 1182));
			return;
		}

		if (_portalEntryInteractionService != null)
		{
			var staticData = _runtimeContext?.DataManager?.StaticData;
			var portalResult = await _portalEntryInteractionService.HandleDialogSelectAsync(
				player,
				packet.TargetObjectId,
				packet.DialogActionId,
				packet.QuestId,
				_world,
				staticData?.PortalPaths,
				staticData?.PortalLocs,
				staticData?.InstanceCooltimes,
				_runtimeContext?.WorldMapStates,
				staticData?.ItemTemplates,
				SendPacketAsync,
				DateTimeOffset.Now,
				_isKnownNpc,
				(teleportPlayer, loc, cancellationToken) => TeleportSameInstancePortalAsync(teleportPlayer, loc, staticData, cancellationToken),
				(transferPlayer, preparation, cancellationToken) => QueuePortalContinueTransferAsync(
					transferPlayer,
					preparation,
					staticData,
					_runtimeContext?.WorldMapStates,
					now: DateTimeOffset.Now,
					cancellationToken: cancellationToken));
			if (portalResult.Handled)
				return;
		}

		if (packet.DialogActionId == CmDialogSelect.Recovery)
		{
			if (NpcDialogTargetingService.ValidateTargetingNpcWithFunction(player, packet.TargetObjectId, CmDialogSelect.Recovery, _world) !=
				NpcDialogTargetingResult.Valid)
			{
				return;
			}

			var recoveryResult = PlayerExperienceRecoveryService.RequestDialog(player, packet.TargetObjectId);
			if (recoveryResult.ResponsePacket != null)
				await SendPacketAsync(recoveryResult.ResponsePacket);
			if (recoveryResult.QuestionWindow != null)
				await SendPacketAsync(recoveryResult.QuestionWindow);
			return;
		}

		if (packet.DialogActionId == CmDialogSelect.CombineTask)
		{
			if (NpcDialogTargetingService.ValidateTargetingNpcWithFunction(player, packet.TargetObjectId, CmDialogSelect.CombineTask, _world) !=
				NpcDialogTargetingResult.Valid)
			{
				return;
			}

			var staticData = _runtimeContext?.DataManager?.StaticData;
			if (staticData?.SkillTemplates == null
				|| _world == null
				|| !_world.TryGetObject(packet.TargetObjectId, out var target)
				|| target is not IWorldNpcObject npc)
			{
				return;
			}

			var craftResult = new CraftSkillUpdateService().RequestLearnSkill(player, npc, staticData.SkillTemplates);
			foreach (var intent in craftResult.PacketIntents)
				await SendExchangePacketAsync(intent.RecipientObjectId, intent.Packet);
			if (craftResult.QuestionWindow != null)
				await SendPacketAsync(craftResult.QuestionWindow);
			return;
		}

		if (packet.DialogActionId is CmDialogSelect.ExtendInventory or CmDialogSelect.ExtendCharWarehouse)
		{
			if (NpcDialogTargetingService.ValidateTargetingNpcWithFunction(player, packet.TargetObjectId, packet.DialogActionId, _world) !=
				NpcDialogTargetingResult.Valid)
			{
				return;
			}

			var staticData = _runtimeContext?.DataManager?.StaticData;
			if (staticData == null
				|| _world == null
				|| !_world.TryGetObject(packet.TargetObjectId, out var target)
				|| target is not IWorldNpcObject npc)
			{
				return;
			}

			var expansionService = new StorageExpansionNpcService();
			var expansionResult = packet.DialogActionId == CmDialogSelect.ExtendInventory
				? expansionService.RequestCubeExpansion(
					player,
					npc,
					staticData.CubeExpansionTemplates.GetTemplateByNpcId(npc.TemplateId),
					_options.Custom.CubeExpansionLimit,
					_options.Custom.NpcCubeExpandsSizeLimit)
				: expansionService.RequestWarehouseExpansion(
					player,
					npc,
					staticData.WarehouseExpansionTemplates.GetTemplateByNpcId(npc.TemplateId));
			foreach (var responsePacket in expansionResult.Packets)
				await SendPacketAsync(responsePacket);
			if (expansionResult.QuestionWindow != null)
				await SendPacketAsync(expansionResult.QuestionWindow);
			return;
		}

		if (packet.DialogActionId is CmDialogSelect.Buy or CmDialogSelect.BuyAgain or CmDialogSelect.TradeIn)
		{
			var plan = CreateNonLiveTradeDialogSelectPlan(player, packet);
			if (plan != null)
				_dialogSelectPlanObserver?.Invoke(plan);
			return;
		}

		var chargeWay = packet.DialogActionId switch
		{
			CmDialogSelect.ChargeItemMulti => 1,
			CmDialogSelect.ChargeItemMulti2 => 2,
			_ => 0,
		};
		if (chargeWay == 0)
			return;

		// Full known-list NPC lookup and supportsAction validation are deferred with the NPC/dialog engine.
		if (packet.TargetObjectId == 0 || player.TargetObjectId != packet.TargetObjectId)
			return;

		await StartChargingEquippedItemsAsync(player, packet.TargetObjectId, chargeWay);
	}

	private QuestDialogNpcTargetBranchInputAssemblyPlan? CreateNonLiveTradeDialogSelectPlan(Player player, CmDialogSelect packet)
	{
		// Java parity breadcrumb: CM_DIALOG_SELECT.runImpl -> NpcController.onDialogSelect ->
		// DialogService.onDialogSelect BUY/TRADE_IN. This boundary intentionally remains non-sending
		// until SM_TRADELIST/SM_TRADE_IN_LIST bytes, no-sell messages, AI dispatch, and runtime facts are live.
		if (_world == null
			|| (_isKnownNpc?.Invoke(player, packet.TargetObjectId) == false)
			|| !_world.TryGetObject(packet.TargetObjectId, out var target)
			|| target is not IWorldNpcObject npc)
		{
			return null;
		}

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var npcTemplates = staticData?.NpcTemplates ?? new NpcTemplateTable([npc.Template]);
		var isInTalkRange = PositionUtilService.IsInNpcTalkRange(
			player.Position,
			npc.Position,
			npc.Template.TalkDistance,
			npc.Template.BoundRadius,
			player.BoundRadius);
		var tradeRuntimeFactPlan = staticData?.TradeLists == null || staticData.GoodsLists == null
			? null
			: NpcDialogTradeRuntimeFactAdapterService.CreatePlan(
				new NpcDialogTradeRuntimeFactAdapterInput(
					player.ObjectId,
					player.LegionId,
					player.LegionId != 0 ? player.LegionLevel : null,
					VendorBuyModifier: _options.Prices.VendorBuyModifier));
		var tradeListFactInput = tradeRuntimeFactPlan?.ToTradeListFactInput(npc.TemplateId);
		var limitedItemFactInput = packet.DialogActionId == CmDialogSelect.Buy
			? tradeRuntimeFactPlan?.ToLimitedItemFactInput(npc.TemplateId)
			: null;
		var repurchasePacketSnapshotPlan = packet.DialogActionId == CmDialogSelect.BuyAgain && staticData?.ItemTemplates != null
			? RepurchasePacketSnapshotPlanService.CreateDisabledPlan(
				packet.TargetObjectId,
				player.RepurchaseItems,
				staticData.ItemTemplates)
			: null;
		var repurchasePacket = packet.DialogActionId == CmDialogSelect.BuyAgain
			? repurchasePacketSnapshotPlan != null
				? repurchasePacketSnapshotPlan.Packet
				: CreateDialogRepurchasePacket(player, packet.TargetObjectId, staticData?.ItemTemplates)
			: null;

		return QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			new QuestDialogNpcTargetBranchRuntimeSnapshot(
				player.ObjectId,
				packet.TargetObjectId,
				packet.DialogActionId,
				packet.LastPage,
				packet.QuestId,
				packet.ExtendedRewardIndex,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: true,
				TargetNpcTemplate: npc.Template,
				InteractionAllowed: true,
				ControllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts(
					IsInTalkRange: isInTalkRange,
					NpcAiHandledDialogSelect: false,
					DialogServiceFacts: packet.DialogActionId == CmDialogSelect.BuyAgain
						? new NpcDialogServiceSelectFacts(NpcSupportsAction: npc.Template.SupportsDialogAction(CmDialogSelect.BuyAgain))
						: null),
				TradeRuntimeFactPlan: tradeRuntimeFactPlan,
				TradeListFactInput: tradeListFactInput,
				LimitedItemFactInput: limitedItemFactInput,
				RepurchasePacket: repurchasePacket,
				RepurchasePacketSnapshotPlan: repurchasePacketSnapshotPlan),
			npcTemplates,
			staticData?.TradeLists,
			staticData?.GoodsLists);
	}

	private static SmRepurchase CreateDialogRepurchasePacket(Player player, int targetObjectId, ItemTemplateTable? itemTemplates)
	{
		// Java parity: SM_REPURCHASE snapshots RepurchaseService.getRepurchaseItems(player.getObjectId())
		// when DialogService handles BUY_AGAIN. Missing templates are skipped here because this
		// diagnostic path must remain non-throwing until live repurchase state is owned.
		var packetItems = new List<RepurchasePacketItem>();
		if (itemTemplates != null)
		{
			foreach (var sourceItem in player.RepurchaseItems)
			{
				var template = itemTemplates.GetItemTemplate(sourceItem.Item.ItemId);
				if (template != null)
					packetItems.Add(new RepurchasePacketItem(sourceItem.Item, template, sourceItem.RepurchasePrice));
			}
		}

		return new SmRepurchase(targetObjectId, packetItems);
	}

	private async Task HandleViewPlayerDetailsAsync(Player player, CmViewPlayerDetails packet)
	{
		// Java parity: network/aion/clientpackets/CM_VIEW_PLAYER_DETAILS.runImpl.
		if (_world == null
			|| !_world.TryGetObject(packet.TargetObjectId, out var target)
			|| target is not Player targetPlayer)
		{
			return;
		}

		if (targetPlayer.Settings.DeniesViewDetails())
		{
			await SendPacketAsync(SmSystemMessage.RejectedWatch(targetPlayer.Name));
			return;
		}

		// Java parity: target.getEquipment().getEquippedItemsWithoutStigma().
		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		var equippedItems = targetPlayer.InventoryItems
			.Where(i => i.IsEquipped)
			.Select(i => (Item: i, Template: itemTemplates?.GetItemTemplate(i.ItemId)))
			.Where(pair => pair.Template != null && !pair.Template.IsStigma)
			.Select(pair => (pair.Item, pair.Template!))
			.ToList();
		await SendPacketAsync(new SmViewPlayerDetails(targetPlayer.ObjectId, equippedItems));
	}

	private async Task HandleDeleteItemAsync(Player player, CmDeleteItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_DELETE_ITEM.runImpl -> inventory.getItemByObjId -> isBreakable.
		var item = player.InventoryItems.FirstOrDefault(
			i => i.ObjectId == packet.ItemObjectId && i.Location == 0 /* cube */);
		if (item == null)
			return;

		var templates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		var template = templates?.GetItemTemplate(item.ItemId);
		if (template == null)
			return;

		if (!template.IsBreakable)
		{
			await SendPacketAsync(SmSystemMessage.UnbreakableItem(template.GetClientName()));
			return;
		}

		// Java parity: storage.delete(item, ItemDeleteType.DISCARD) removes item from inventory and persists.
		var inventoryItems = player.InventoryItems.ToList();
		inventoryItems.Remove(item);
		player.InventoryItems = [.. inventoryItems];
		if (_playerEnterWorldService != null)
			await _playerEnterWorldService.DeleteInventoryItemAsync(player, item.ObjectId);
		await SendPacketAsync(new SmDeleteItem(item.ObjectId, SmDeleteItem.DiscardDeleteType));
	}

	private async Task HandleUnwrapItemAsync(Player player, CmUnwrapItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_UNWRAP_ITEM.runImpl.
		var item = player.InventoryItems.FirstOrDefault(
			i => i.ObjectId == packet.ObjectId && i.Location == 0 /* cube */);
		if (item == null || item.PackCount <= 0)
			return;

		var templates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		var template = templates?.GetItemTemplate(item.ItemId);
		if (template == null)
			return;

		// Java parity: sendPacket(new SM_UNWRAP_ITEM(objectId, item.getPackCount())).
		await SendPacketAsync(new SmUnwrapItem(item.ObjectId, item.PackCount));

		// Java parity: item.setPackCount(item.getPackCount() * -1) — negate to mark as unwrapped.
		item.PackCount = -item.PackCount;
		if (_playerEnterWorldService != null)
			await _playerEnterWorldService.SaveInventoryItemPackCountAsync(player, item.ObjectId, item.PackCount);

		// Java parity: PacketSendUtility.sendPacket(player, new SM_INVENTORY_UPDATE_ITEM(player, item)) defaults to DEC_ITEM_USE.
		await SendPacketAsync(new SmInventoryUpdateItem(item, template, SmInventoryUpdateItem.DecreaseItemUse));
	}

	private async Task HandleSplitItemAsync(Player player, CmSplitItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_SPLIT_ITEM.runImpl -> ItemSplitService.splitItem.
		if (packet.ItemAmount <= 0)
			return;

		// Java parity: ItemSplitService.splitItem checks player.isTrading() before split.
		if (player.IsTrading)
		{
			await SendPacketAsync(SmSystemMessage.InventorySplitDuringTrade());
			return;
		}

		if (packet.SourceStorageType == 3 || packet.DestinationStorageType == 3)
		{
			// Legion warehouse splits require LegionService and addWHItemHistory; deferred.
			return;
		}

		var sourceItem = player.InventoryItems.FirstOrDefault(
			i => i.ObjectId == packet.SourceItemObjectId && i.Location == packet.SourceStorageType);
		if (sourceItem == null)
			return;

		// Java parity: ItemSplitService.splitItem — targetItem == null branch (split to empty slot).
		var targetItem = packet.DestinationItemObjectId != 0
			? player.InventoryItems.FirstOrDefault(i => i.ObjectId == packet.DestinationItemObjectId && i.Location == packet.DestinationStorageType)
			: null;

		var templates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		var template = templates?.GetItemTemplate(sourceItem.ItemId);
		if (template == null)
			return;

		// Java parity: ItemSplitService.splitItem — kinah branch (moveKinah) before general split logic.
		// Java parity: ItemTemplate.isKinah() checks itemId == ItemId.KINAH (182400001).
		const int KinahItemId = 182400001;
		if (sourceItem.ItemId == KinahItemId)
		{
			await HandleKinahMoveAsync(player, sourceItem, packet.SourceStorageType, packet.DestinationStorageType, packet.ItemAmount, templates!, template);
			return;
		}

		if (targetItem == null)
		{
			// Split into empty slot (same or cross storage).
			if (_idFactory == null)
				return;
			var newCount = packet.ItemAmount;
			var remainingCount = sourceItem.Count - newCount;
			if (sourceItem.Count < newCount || remainingCount == 0)
				return;

			// Java parity: ItemRestrictionService.isItemRestrictedTo for cross-storage.
			if (packet.SourceStorageType != packet.DestinationStorageType)
			{
				if (packet.DestinationStorageType == 1 && !template.IsStorableInWarehouse)
				{
					await SendPacketAsync(SmSystemMessage.WarehouseCantDepositItem());
					return;
				}
				if (packet.DestinationStorageType == 2 && !sourceItem.IsStorableInAccountWarehouse(template))
				{
					await SendPacketAsync(SmSystemMessage.WarehouseCantAccountDeposit());
					return;
				}
			}

			var newObjectId = _idFactory.NextId();
			var newItem = new InventoryItem
			{
				ObjectId = newObjectId,
				ItemId = sourceItem.ItemId,
				Count = newCount,
				OwnerId = player.ObjectId,
				Location = packet.DestinationStorageType,
				// Java parity: cross-storage split does NOT set slot (newItem.setEquipmentSlot only for same-storage).
				Slot = packet.SourceStorageType == packet.DestinationStorageType ? packet.SlotNumber : 0,
				TuneCount = 0,
				PersistentState = InventoryItemPersistentState.New,
			};

			sourceItem.Count = remainingCount;
			player.InventoryItems = [.. player.InventoryItems, newItem];

			if (_playerEnterWorldService != null)
			{
				var saved = await _playerEnterWorldService.SaveItemSplitMutationAsync(player, sourceItem, newItem);
				if (!saved)
				{
					// Rollback in-memory changes.
					sourceItem.Count += newCount;
					var items = player.InventoryItems.ToList();
					items.Remove(newItem);
					player.InventoryItems = [.. items];
					_idFactory.ReleaseId(newObjectId);
					return;
				}
			}

			// Java parity: sourceStorage.decreaseItemCount uses DEC_ITEM_SPLIT for same-storage, DEC_ITEM_SPLIT_MOVE for cross-storage.
			var sourceDecreaseType = packet.SourceStorageType == packet.DestinationStorageType
				? SmInventoryUpdateItem.DecreaseItemSplit
				: SmInventoryUpdateItem.DecreaseItemSplitMove;

			if (packet.SourceStorageType == 0 /* cube source */)
				await SendPacketAsync(new SmInventoryUpdateItem(sourceItem, template, sourceDecreaseType));
			else
				await SendPacketAsync(new SmWarehouseUpdateItem(sourceItem, template, packet.SourceStorageType, sourceDecreaseType));
			// Java parity: SM_CUBE_UPDATE.cubeSize after split.
			await SendPacketAsync(SmCubeUpdate.CubeSize(player));

			// Java parity: sendStorageUpdatePacket for destination storage.
			if (packet.DestinationStorageType == 0 /* cube */)
				await SendPacketAsync(SmInventoryAddItem.CreateItemCollect(newItem, template));
			else
				await SendPacketAsync(new SmWarehouseAddItem(
					packet.DestinationStorageType,
					[new SmWarehouseAddItem.WarehousePacketItem(newItem, template)],
					SmInventoryAddItem.ItemCollect));
			await SendPacketAsync(SmCubeUpdate.CubeSize(player));
		}
		else if (targetItem.ItemId == sourceItem.ItemId)
		{
			// Java parity: ItemSplitService.mergeStacks — merge split amount into existing same-item stack.
			var freeCount = template.MaxStackCount - targetItem.Count;
			if (freeCount <= 0 || sourceItem.Count < packet.ItemAmount)
				return;

			var actualMergeAmount = Math.Min(packet.ItemAmount, freeCount);
			sourceItem.Count -= actualMergeAmount;
			targetItem.Count += actualMergeAmount;

			if (_playerEnterWorldService != null)
			{
				var saved = await _playerEnterWorldService.SaveItemMergeMutationAsync(player, sourceItem, targetItem);
				if (!saved)
				{
					// Rollback in-memory changes.
					sourceItem.Count += actualMergeAmount;
					targetItem.Count -= actualMergeAmount;
					return;
				}
			}

			// Java parity: mergeStacks uses INC_ITEM_MERGE for same-storage, INC_ITEM_COLLECT for cross-storage.
			var isSameStorage = packet.SourceStorageType == packet.DestinationStorageType;
			var destIncreaseType = isSameStorage ? SmInventoryUpdateItem.IncreaseItemMerge : SmInventoryUpdateItem.IncreaseItemCollect;
			var srcDecreaseType = isSameStorage ? SmInventoryUpdateItem.DecreaseItemSplit : SmInventoryUpdateItem.DecreaseItemSplitMove;

			if (packet.DestinationStorageType == 0)
				await SendPacketAsync(new SmInventoryUpdateItem(targetItem, template, destIncreaseType));
			else
				await SendPacketAsync(new SmWarehouseUpdateItem(targetItem, template, packet.DestinationStorageType, destIncreaseType));

			if (packet.SourceStorageType == 0)
				await SendPacketAsync(new SmInventoryUpdateItem(sourceItem, template, srcDecreaseType));
			else
				await SendPacketAsync(new SmWarehouseUpdateItem(sourceItem, template, packet.SourceStorageType, srcDecreaseType));
		}
	}

	private async Task HandleKinahMoveAsync(
		Player player,
		InventoryItem sourceKinah,
		int sourceStorageType,
		int destinationStorageType,
		long moveAmount,
		ItemTemplateTable itemTemplates,
		ItemTemplateSummary kinahTemplate)
	{
		// Java parity: ItemSplitService.moveKinah — moves kinah between cube and account warehouse.
		// Only cube (0) ↔ account warehouse (2) is supported; regular/legion warehouse kinah not handled.
		if ((sourceStorageType == 0 && destinationStorageType != 2)
			|| (sourceStorageType == 2 && destinationStorageType != 0))
			return;

		if (sourceKinah.Count < moveAmount)
			return;

		const int KinahItemId = 182400001;
		var destKinah = player.InventoryItems.FirstOrDefault(
			i => i.ItemId == KinahItemId && i.Location == destinationStorageType);
		if (destKinah == null)
			return;

		// Java parity: checksum validation prevents arithmetic overflow issues.
		var newSourceCount = sourceKinah.Count - moveAmount;
		var newDestCount = destKinah.Count + moveAmount;
		if (newSourceCount + newDestCount != sourceKinah.Count + destKinah.Count)
			return;

		sourceKinah.Count = newSourceCount;
		destKinah.Count = newDestCount;

		if (_playerEnterWorldService != null)
		{
			var saved = await _playerEnterWorldService.SaveItemMergeMutationAsync(player, sourceKinah, destKinah);
			if (!saved)
			{
				// Rollback in-memory changes.
				sourceKinah.Count += moveAmount;
				destKinah.Count -= moveAmount;
				return;
			}
		}

		// Java parity: source.decreaseKinah(splitAmount, DEC_ITEM_SPLIT) — update source kinah in UI.
		if (sourceStorageType == 0 /* cube */)
			await SendPacketAsync(new SmInventoryUpdateItem(sourceKinah, kinahTemplate, SmInventoryUpdateItem.DecreaseItemSplit));
		else
			await SendPacketAsync(new SmWarehouseUpdateItem(sourceKinah, kinahTemplate, sourceStorageType, SmInventoryUpdateItem.DecreaseItemSplit));

		// Java parity: destination.increaseKinah(splitAmount, INC_KINAH_MERGE) — update dest kinah in UI.
		if (destinationStorageType == 0 /* cube */)
			await SendPacketAsync(new SmInventoryUpdateItem(destKinah, kinahTemplate, SmInventoryUpdateItem.IncreaseKinahMerge));
		else
			await SendPacketAsync(new SmWarehouseUpdateItem(destKinah, kinahTemplate, destinationStorageType, SmInventoryUpdateItem.IncreaseKinahMerge));
	}

	private async Task HandleMoveItemAsync(Player player, CmMoveItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_MOVE_ITEM.runImpl -> ItemMoveService.moveItem.
		var item = player.InventoryItems.FirstOrDefault(
			i => i.ObjectId == packet.ItemObjectId && i.Location == packet.Source);
		if (item == null)
			return;

		if (packet.Source == packet.Destination)
		{
			// Same-storage slot reordering.
			// Java parity: ItemMoveService.moveInSameStorage — updates slot and marks item dirty; no response packet.
			if (item.Slot == packet.Slot)
				return;
			item.Slot = packet.Slot;
			if (_playerEnterWorldService != null)
				await _playerEnterWorldService.SaveInventoryItemSlotAsync(player, item.ObjectId, packet.Slot);
			// No response packet: the client already updated its UI before sending this packet.
			return;
		}

		// Cross-storage move (cube ↔ regular/account warehouse).
		// Java parity: ItemMoveService.moveItem cross-storage — ItemRestrictionService checks, then remove/add.
		// Legion warehouse and trading checks remain deferred.
		if (packet.Destination == 3 || packet.Source == 3)
		{
			// Legion warehouse requires LegionService and permission checks; deferred.
			return;
		}

		var templates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		var template = templates?.GetItemTemplate(item.ItemId);
		if (template == null)
			return;

		// Java parity: ItemMoveService.moveItem — trading check before cross-storage restriction check.
		if (player.IsTrading)
		{
			// Java parity: sendItemUnlockPacket restores item in source UI; use ALL_SLOT add type for cube.
			if (item.Location == 0)
				await SendPacketAsync(SmInventoryAddItem.CreateAllSlot(item, template));
			else
				await SendPacketAsync(SmWarehouseAddItem.CreateAllSlot(item.Location, item, template));
			return;
		}

		// Java parity: ItemRestrictionService.isItemRestrictedTo — check storability for destination.
		if (packet.Destination == 1 /* regular warehouse */ && !template.IsStorableInWarehouse)
		{
			await SendPacketAsync(SmSystemMessage.WarehouseCantDepositItem());
			return;
		}
		if (packet.Destination == 2 /* account warehouse */ && !item.IsStorableInAccountWarehouse(template))
		{
			await SendPacketAsync(SmSystemMessage.WarehouseCantAccountDeposit());
			return;
		}

		// Mutate item location and slot in memory.
		var oldLocation = item.Location;
		var oldSlot = item.Slot;
		item.Location = packet.Destination;
		item.Slot = packet.Slot;

		if (_playerEnterWorldService != null)
		{
			var saved = await _playerEnterWorldService.SaveItemCrossStorageMoveMutationAsync(
				player, item.ObjectId, packet.Destination, packet.Slot);
			if (!saved)
			{
				// Rollback in-memory changes.
				item.Location = oldLocation;
				item.Slot = oldSlot;
				return;
			}
		}

		// Java parity: sendItemDeletePacket for source storage.
		if (oldLocation == 0 /* cube */)
		{
			// Java parity: SM_DELETE_ITEM(objectId, MOVE=0x14) + SM_CUBE_UPDATE for cube source.
			await SendPacketAsync(new SmDeleteItem(item.ObjectId, SmDeleteItem.MoveDeleteType));
			await SendPacketAsync(SmCubeUpdate.CubeSize(player));
		}
		else
		{
			// Java parity: SM_DELETE_WAREHOUSE_ITEM(storageTypeId, objectId, MOVE=0x14) + SM_CUBE_UPDATE for warehouse source.
			await SendPacketAsync(new SmDeleteWarehouseItem(oldLocation, item.ObjectId, SmDeleteItem.MoveDeleteType));
			await SendPacketAsync(SmCubeUpdate.CubeSize(player));
		}

		// Java parity: sendStorageUpdatePacket for destination storage.
		if (packet.Destination == 0 /* cube */)
		{
			// Java parity: SM_INVENTORY_ADD_ITEM(item, ITEM_COLLECT) + SM_CUBE_UPDATE.
			await SendPacketAsync(SmInventoryAddItem.CreateItemCollect(item, template));
			await SendPacketAsync(SmCubeUpdate.CubeSize(player));
		}
		else
		{
			// Java parity: SM_WAREHOUSE_ADD_ITEM(item, warehouseType, ITEM_COLLECT) + SM_CUBE_UPDATE.
			await SendPacketAsync(new SmWarehouseAddItem(
				packet.Destination,
				[new SmWarehouseAddItem.WarehousePacketItem(item, template)],
				SmWarehouseAddItem.AllSlot));
			await SendPacketAsync(SmCubeUpdate.CubeSize(player));
		}
	}

	private async Task HandleReplaceItemAsync(Player player, CmReplaceItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_REPLACE_ITEM.runImpl -> ItemMoveService.switchItemsInStorages.
		// Restrictions (trading/shutdown) are checked like CM_MOVE_ITEM.
		if (player.IsTrading)
			return;

		var sourceItem = player.InventoryItems.FirstOrDefault(
			i => i.ObjectId == packet.SourceItemObjectId && i.Location == packet.SourceStorageType);
		if (sourceItem == null)
			return;

		var replaceItem = player.InventoryItems.FirstOrDefault(
			i => i.ObjectId == packet.ReplaceItemObjectId && i.Location == packet.ReplaceStorageType);
		if (replaceItem == null)
			return;

		if (packet.SourceStorageType != packet.ReplaceStorageType)
		{
			// Java parity: cross-storage switch requires StorageType add/remove — deferred until cross-storage item swap is ported.
			return;
		}

		// Same-storage: swap slot values only (no storage change).
		// Java parity: ItemMoveService.switchItemsInStorages same-storage path swaps equipmentSlot, no add/remove packets needed.
		var sourceOldSlot = sourceItem.Slot;
		var replaceOldSlot = replaceItem.Slot;
		if (sourceOldSlot == replaceOldSlot)
			return;

		sourceItem.Slot = replaceOldSlot;
		replaceItem.Slot = sourceOldSlot;

		if (_playerEnterWorldService != null)
		{
			await _playerEnterWorldService.SaveInventoryItemSlotAsync(player, sourceItem.ObjectId, replaceOldSlot);
			await _playerEnterWorldService.SaveInventoryItemSlotAsync(player, replaceItem.ObjectId, sourceOldSlot);
		}
		// Java parity: same-storage switch sends no response packet; client already reordered its UI.
	}

	private async Task HandleObjectSearchAsync(Player player, CmObjectSearch packet)
	{
		// Java parity: network/aion/clientpackets/CM_OBJECT_SEARCH.runImpl -> SpawnsData.getNearestSpawnByNpcId -> SM_SHOW_NPC_ON_MAP.
		// Simplified: uses GetFirstSpawnByNpcId (current map preferred) instead of full race-aware nearest-spawn search.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null)
			return;

		var spawn = staticData.NpcSpawns.GetFirstSpawnByNpcId(player.Position.WorldId, packet.NpcId);
		if (spawn == null)
		{
			// Java parity: SM_SYSTEM_MESSAGE.STR_FIND_POS_UNKNOWN_NAME() has message id 1300747.
			await SendPacketAsync(new SmSystemMessage(1300747));
			return;
		}

		await SendPacketAsync(new SmShowNpcOnMap(player, packet.NpcId, spawn.MapId, spawn.X, spawn.Y, spawn.Z));
	}

	private async Task HandlePlayerSearchAsync(Player player, CmPlayerSearch packet)
	{
		// Java parity: network/aion/clientpackets/CM_PLAYER_SEARCH.runImpl -> World.getAllPlayers() filter -> SM_PLAYER_SEARCH.
		var levelToSearch = _options.Custom.LevelToSearch;
		if (player.Level < levelToSearch)
		{
			// Java parity: SM_SYSTEM_MESSAGE.STR_CANT_WHO_LEVEL(LEVEL_TO_SEARCH) has message id 1400341.
			await SendPacketAsync(new SmSystemMessage(1400341, levelToSearch.ToString()));
			return;
		}

		if (_connectionRegistry == null)
		{
			await SendPacketAsync(new SmPlayerSearch(Array.Empty<PlayerSearchResultRow>()));
			return;
		}

		var nameFilter = (packet.Name ?? string.Empty).Trim();
		var criteria = new PlayerSearchCriteria(
			SearcherRace: player.Race,
			SearcherIsStaff: player.AccessLevel > 0,
			NameFilter: nameFilter,
			Region: packet.Region,
			ClassMask: packet.ClassMask,
			MinLevel: packet.MinLevel,
			MaxLevel: packet.MaxLevel,
			LfgOnly: packet.LfgOnly,
			FactionsSearchMode: _options.Custom.FactionsSearchMode,
			SearchGmList: _options.Custom.SearchGmList);

		var rows = new List<PlayerSearchResultRow>();
		_connectionRegistry.ForEachOnlinePlayer(candidate =>
		{
			if (rows.Count >= PlayerSearchMatchService.MaxResults)
				return;

			var candidateInfo = new PlayerSearchCandidate(
				ObjectId: candidate.ObjectId,
				Name: candidate.Name,
				Race: candidate.Race,
				Level: candidate.Level,
				ClassId: ToPlayerClassId(candidate.PlayerClass),
				WorldId: candidate.Position.WorldId,
				IsStaff: candidate.AccessLevel > 0,
				IsLookingForGroup: candidate.IsLookingForGroup,
				// Java parity: FriendList.Status.OFFLINE == 0 (appear-offline social toggle).
				FriendStatusOffline: candidate.FriendListStatus == 0);

			if (!PlayerSearchMatchService.Matches(criteria, candidateInfo, player.ObjectId))
				return;

			// Java parity: status byte: deniedGroup ? 1 : inTeam ? 3 : lfg ? 2 : 0.
			var status = candidate.Settings.DeniesGroupRequests() ? 1
				: candidate.IsInTeam ? 3
				: candidate.IsLookingForGroup ? 2
				: 0;

			rows.Add(new PlayerSearchResultRow(
				candidate.Position.WorldId,
				candidate.Position.X,
				candidate.Position.Y,
				candidate.Position.Z,
				candidateInfo.ClassId,
				ToPlayerGenderId(candidate.Gender),
				candidate.Level,
				status,
				// Java parity: ChatUtil.toFactionPrefixedName — staff searchers see a faction glyph prefix on each name.
				PlayerSearchMatchService.ToFactionPrefixedName(criteria.SearcherIsStaff, candidate.Race, candidate.Name)));
		});

		await SendPacketAsync(new SmPlayerSearch(rows));
	}

	private static int ToPlayerClassId(string playerClass)
	{
		// Java parity: model/PlayerClass.getClassId ordinal mapping.
		return playerClass.ToUpperInvariant() switch
		{
			"WARRIOR" => 0,
			"GLADIATOR" => 1,
			"TEMPLAR" => 2,
			"SCOUT" => 3,
			"ASSASSIN" => 4,
			"RANGER" => 5,
			"MAGE" => 6,
			"SORCERER" => 7,
			"SPIRIT_MASTER" => 8,
			"PRIEST" => 9,
			"CLERIC" => 10,
			"CHANTER" => 11,
			"ENGINEER" => 12,
			"RIDER" => 13,
			"GUNNER" => 14,
			"ARTIST" => 15,
			"BARD" => 16,
			_ => 0,
		};
	}

	private static int ToPlayerGenderId(string gender)
	{
		// Java parity: model/Gender.getGenderId — MALE=0, FEMALE=1.
		return string.Equals(gender, "FEMALE", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
	}

	private async Task HandleDistributionSettingsAsync(Player player, CmDistributionSettings packet)
	{
		// Java parity: network/aion/clientpackets/CM_DISTRIBUTION_SETTINGS.runImpl -> PlayerGroupService/PlayerAllianceService.changeGroupRules.
		var lootRule = packet.LootRule switch
		{
			0 => PlayerGroupLootRuleType.FreeForAll,
			1 => PlayerGroupLootRuleType.RoundRobin,
			2 => PlayerGroupLootRuleType.Leader,
			_ => PlayerGroupLootRuleType.RoundRobin,
		};

		// Java parity: CM_DISTRIBUTION_SETTINGS.ethernalItemAbove maps to LootGroupRules(ethernalItemAbove) in Java (typo preserved).
		var newRules = new PlayerGroupLootRules(
			lootRule,
			packet.Misc,
			packet.CommonItemAbove,
			packet.SuperiorItemAbove,
			packet.HeroicItemAbove,
			packet.FabledItemAbove,
			EternalItemAbove: packet.EthernalItemAbove,
			packet.MythicItemAbove);

		if (player.TeamMembership == PlayerTeamMembership.Group)
		{
			var teamId = player.CurrentTeamId;
			if (teamId == 0 || !_playerGroupRuntime.IsLeader(teamId, player))
				return;

			var plan = _playerGroupRuntime.ChangeLootRules(teamId, newRules);
			if (plan == null)
				return;

			foreach (var intent in plan.GroupInfoBroadcasts)
				await SendGroupLeaderPacketAsync(intent.RecipientObjectId, intent.CreateGroupInfoPacket(), default);
		}
		else if (player.TeamMembership == PlayerTeamMembership.Alliance)
		{
			// Java parity: CM_DISTRIBUTION_SETTINGS.runImpl -> PlayerAllianceService.changeGroupRules for alliance leader.
			// League path remains deferred.
			var allianceId = player.CurrentTeamId;
			if (allianceId == 0 || !_playerAllianceRuntime.IsLeader(allianceId, player))
				return;

			var allianceIntents = _playerAllianceRuntime.ChangeLootRules(allianceId, newRules);
			if (allianceIntents == null)
				return;

			foreach (var intent in allianceIntents)
				await SendAllianceLeaderPacketAsync(intent.RecipientObjectId, intent.CreatePacket(), default);
		}
	}

	private void HandleCloseDialog(Player player, CmCloseDialog packet)
	{
		// Java parity: network/aion/clientpackets/CM_CLOSE_DIALOG.runImpl delegates to DialogService.onCloseDialog.
		var isNpcTarget = _world != null
			&& _world.TryGetObject(packet.TargetObjectId, out var target)
			&& target is IWorldNpcObject;
		var plan = new NpcDialogCloseSideEffectPlanService().CreatePlan(
			player,
			packet.TargetObjectId,
			isNpcTarget);
		if (plan.WouldCloseMailbox)
			player.MailboxState = PlayerMailboxState.Closed;
		// AI DIALOG_FINISH event and legion warehouse release remain non-live until AI and legion systems are enabled.
	}

	private async Task HandleShowDialogAsync(Player player, CmShowDialog packet)
	{
		// Java parity: network/aion/clientpackets/CM_SHOW_DIALOG.runImpl delegates targeted NPCs to controller.onDialogRequest.
		var sideEffects = NpcDialogSideEffectService.ApplyShowDialogSideEffects(player, packet.TargetObjectId, _world, _isKnownNpc);
		if (sideEffects.PlayerStateChanged && _connectionRegistry != null)
		{
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(
				player.Position,
				player.ObjectId,
				new SmPlayerState(player),
				includeSourcePlayer: true);
		}

		if (player.IsTrading)
			return;

		if (_riftPortalInteractionService != null)
		{
			var result = _riftPortalInteractionService.RequestDialog(player, packet.TargetObjectId);
			if (result.Requested && result.QuestionWindow != null)
			{
				await SendPacketAsync(result.QuestionWindow);
				return;
			}
			if (result.Status != RiftPortalDialogStatus.UnknownPortal)
				return;
		}

		if (_runtimeContext != null)
		{
			var kiskDialogResult = PlayerKiskDialogService.RequestDialog(
				player,
				packet.TargetObjectId,
				_world,
				_runtimeContext.Kisks,
				_isKnownNpc);
			if (kiskDialogResult.ResponsePacket != null)
			{
				await SendPacketAsync(kiskDialogResult.ResponsePacket);
				return;
			}
			if (kiskDialogResult.Handled)
				return;
		}

		var npcDialogResult = NpcDialogRequestService.RequestDialog(player, packet.TargetObjectId, _world, _isKnownNpc);
		if (npcDialogResult.ResponsePacket != null)
			await SendPacketAsync(npcDialogResult.ResponsePacket);
	}

	private async Task HandleStartLootAsync(CmStartLoot packet)
	{
		// Java parity: network/aion/clientpackets/CM_START_LOOT.runImpl dispatches DropService open/close.
		if (_activePlayer == null || _worldNpcLootService == null)
			return;

		var result = packet.Action switch
		{
			0 => _worldNpcLootService.RequestDropList(_activePlayer, packet.TargetObjectId),
			1 => _worldNpcLootService.CloseDropList(_activePlayer, packet.TargetObjectId),
			_ => WorldNpcLootResult.None(WorldNpcLootStatus.UnknownDrop),
		};

		foreach (var responsePacket in result.PlayerPackets)
			await SendPacketAsync(responsePacket);

		foreach (var broadcastPacket in result.VisiblePlayerPackets)
		{
			if (_connectionRegistry != null)
			{
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(
					_activePlayer.Position,
					_activePlayer.ObjectId,
					broadcastPacket,
					includeSourcePlayer: true);
			}
			else
			{
				await SendPacketAsync(broadcastPacket);
			}
		}
	}

	private async Task HandleLootItemAsync(CmLootItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_LOOT_ITEM.runImpl dispatches DropService.requestDropItem.
		if (_activePlayer == null || _worldNpcLootService == null)
			return;

		var result = _worldNpcLootService.RequestDropItem(
			_activePlayer,
			packet.TargetObjectId,
			packet.Index,
			_runtimeContext?.DataManager?.StaticData.ItemTemplates,
			() => _idFactory?.NextId() ?? 0,
			_runtimeContext?.DataManager?.StaticData.ItemRestrictionCleanups);

		foreach (var responsePacket in result.PlayerPackets)
			await SendPacketAsync(responsePacket);

		foreach (var broadcastPacket in result.VisiblePlayerPackets)
		{
			if (_connectionRegistry != null)
			{
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(
					_activePlayer.Position,
					_activePlayer.ObjectId,
					broadcastPacket,
					includeSourcePlayer: true);
			}
			else
			{
				await SendPacketAsync(broadcastPacket);
			}
		}
	}

	private async Task StartChargingEquippedItemsAsync(Player player, int senderObjectId, int chargeWay)
	{
		// Java parity: services/item/ItemChargeService.startChargingEquippedItems.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (itemTemplates == null)
			return;

		var chargePlans = ItemChargeService.CreateChargeAllPlans(player, player.InventoryItems, itemTemplates, chargeWay);
		if (chargePlans.Count == 0)
		{
			await SendPacketAsync(
				chargeWay == 1
					? SmSystemMessage.ItemChargeAllFailNoChargeableEquipment()
					: SmSystemMessage.ItemCharge2AllFailNoChargeableEquipment());
			return;
		}

		var payAmount = chargePlans.Sum(plan => plan.PaymentAmount);
		var pendingRequest = new PendingChargeAllRequest(
			senderObjectId,
			chargeWay,
			payAmount,
			chargePlans
				.Select(plan => new PendingChargeAllItem(
					plan.Item.ObjectId,
					plan.Item.ItemId,
					plan.Item.Charge,
					plan.TargetChargePoints,
					plan.Level))
				.ToArray());
		var questionId = GetChargeAllQuestionId(chargeWay);
		// Java parity: ItemChargeService.startChargingEquippedItems registers its
		// RequestResponseHandler in Player.getResponseRequester().putRequest before
		// sending the charge-all SM_QUESTION_WINDOW.
		if (!player.ResponseRequester.PutRequest(
			questionId,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)))
		{
			return;
		}

		player.PendingChargeAllRequest = pendingRequest;

		await SendPacketAsync(
			new SmQuestionWindow(
				questionId,
				senderObjectId,
				0,
				payAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
	}

	private async Task HandleChargeItemAsync(Player player, CmChargeItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_CHARGE_ITEM.runImpl -> services/item/ItemChargeService.chargeItems.
		if (player.TargetObjectId != packet.TargetNpcObjectId)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null || packet.ItemObjectIds.Count == 0)
			return;

		var selectedObjectIds = packet.ItemObjectIds.ToHashSet();
		var inventoryItems = player.InventoryItems.ToList();
		var completedChargeWays = new HashSet<int>();
		foreach (var selectedItem in inventoryItems
			.Where(item => selectedObjectIds.Contains(item.ObjectId) && item.Location == CubeStorageId)
			.ToArray())
		{
			var currentItem = inventoryItems.FirstOrDefault(item => item.ObjectId == selectedItem.ObjectId);
			if (currentItem == null)
				continue;

			var plan = ItemChargeService.CreateChargePlan(player, currentItem, itemTemplates, packet.ChargeLevel, ignoreRankRequirement: false, requirePayment: true);
			if (plan == null)
				continue;

			var chargedItem = CopyInventoryItem(currentItem, charge: plan.TargetChargePoints);
			InventoryItem? kinahUpdate = null;
			AbyssPointsAddPlan? abyssPointsPlan = null;
			switch (plan.ChargeWay)
			{
				case 1:
					// Java parity: ItemChargeService.processKinahPayment delegates to Storage.tryDecreaseKinah.
					var selectedItemKinahPlan = ItemChargeService.CreateKinahPaymentPlan(
						inventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId),
						plan.PaymentAmount);
					if (!selectedItemKinahPlan.Succeeded)
						continue;
					kinahUpdate = selectedItemKinahPlan.KinahItemUpdate;
					break;
				case 2:
					// Java parity: ItemChargeService.processAPPayment guards current AP before
					// delegating to AbyssPointsService.addAp for the spend.
					var selectedItemPaymentPlan = ItemChargeService.CreateAbyssPointPaymentPlan(
						player,
						plan.PaymentAmount,
						CreateAbyssPointsOptions());
					if (!selectedItemPaymentPlan.Succeeded)
						continue;
					abyssPointsPlan = selectedItemPaymentPlan.AbyssPointsPlan;
					break;
				default:
					continue;
			}

			var saved = _playerEnterWorldService == null
				|| await _playerEnterWorldService.SaveItemChargeMutationAsync(player, chargedItem, kinahUpdate, abyssPointsPlan?.UpdatedRank);
			if (!saved)
				continue;

			ReplaceInventoryItem(inventoryItems, chargedItem);
			if (kinahUpdate != null)
				ReplaceInventoryItem(inventoryItems, kinahUpdate);
			if (abyssPointsPlan?.UpdatedRank != null)
				player.AbyssRank = abyssPointsPlan.UpdatedRank;
			player.InventoryItems = inventoryItems.ToArray();

			if (kinahUpdate != null && itemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
				await SendPacketAsync(new SmInventoryUpdateItem(kinahUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
			if (abyssPointsPlan != null)
			{
				foreach (var playerPacket in abyssPointsPlan.PlayerPackets)
					await SendPacketAsync(playerPacket);
				await ApplyAbyssRankChangedSideEffectsAsync(player, abyssPointsPlan.OldRank, staticData);
			}

			if (GetChargeBarStep(currentItem.Charge) != GetChargeBarStep(chargedItem.Charge))
				await SendPacketAsync(new SmInventoryUpdateItem(chargedItem, plan.Template, SmInventoryUpdateItem.Charge));

			var itemName = plan.Template.GetClientName() ?? plan.Template.Name;
			await SendPacketAsync(
				plan.ChargeWay == 1
					? SmSystemMessage.ItemChargeSuccess(itemName, plan.Level)
					: SmSystemMessage.ItemCharge2Success(itemName, plan.Level));
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
			completedChargeWays.Add(plan.ChargeWay);
		}

		foreach (var chargeWay in completedChargeWays)
		{
			await SendPacketAsync(
				chargeWay == 1
					? SmSystemMessage.ItemChargeAllComplete()
					: SmSystemMessage.ItemCharge2AllComplete());
		}
	}

	private async Task HandleEquipItemAsync(Player player, CmEquipItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_EQUIP_ITEM.runImpl -> model/gameobjects/player/Equipment.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null)
			return;

		var change = EquipmentService.ChangeEquipment(
			player,
			packet.Action,
			packet.Slot,
			packet.ItemObjectId,
			itemTemplates,
			staticData.SkillTemplates,
			staticData.PlayerExperienceTable,
			skillTree: staticData.SkillTree,
			stigmaSlotQuestMembership: _options.Membership.StigmaSlotQuest);
		if (!change.Changed)
		{
			if (change.Failure == EquipmentChangeFailure.SoulBindRequired)
				await StartSoulBindRequestAsync(player, change);
			else
				await SendEquipFailureMessageAsync(change);
			return;
		}

		await ApplyEquipmentChangeAsync(player, change, itemTemplates, staticData);
	}

	private async Task HandleManastoneAsync(Player player, CmManastone packet)
	{
		// Java parity: network/aion/clientpackets/CM_MANASTONE.runImpl action routing.
		if (packet.ActionType == 3)
		{
			await HandleRemoveManastoneAsync(player, packet);
			return;
		}

		if (packet.ActionType == 4)
		{
			await HandleSocketGodstoneAsync(player, packet);
			return;
		}

		if (packet.ActionType == 8)
		{
			await HandleAmplifyItemAsync(player, packet);
			return;
		}

		if (packet.ActionType is not (1 or 2))
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null)
			return;

		var sourceItem = player.InventoryItems.FirstOrDefault(item => item.ObjectId == packet.StoneObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		if (sourceItem == null)
			return;

		var targetItem = player.InventoryItems.FirstOrDefault(item =>
			item.ObjectId == packet.TargetItemObjectId
			&& (packet.ActionType == 1 || item.Location == CubeStorageId));
		if (targetItem == null)
		{
			if (packet.ActionType == 2)
				await SendPacketAsync(SmSystemMessage.GiveItemOptionNoTargetItem());
			else
				await SendPacketAsync(SmSystemMessage.EnchantItemNoTargetItem());
			return;
		}

		var targetTemplate = itemTemplates.GetItemTemplate(targetItem.ItemId);
		var sourceTemplate = itemTemplates.GetItemTemplate(sourceItem.ItemId);
		if (targetTemplate?.StigmaInfo == null || sourceTemplate?.StigmaInfo == null)
		{
			if (packet.ActionType == 2)
				await HandleSocketManastoneAsync(player, packet);
			else
				await HandleEnchantItemAsync(player, packet);
			return;
		}

		var plan = StigmaService.CreateChargePlan(
			player,
			packet.TargetItemObjectId,
			packet.StoneObjectId,
			itemTemplates,
			staticData.SkillTemplates,
			staticData.SkillTree,
			staticData.PlayerExperienceTable);
		if (plan.Result != StigmaChargeResult.Success)
			return;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				targetItem.ObjectId,
				sourceItem.ObjectId,
				targetItem.ItemId,
				5000,
				0,
				0,
				0,
				1,
				0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: targetItem.ItemId,
			targetItemName: plan.ItemName,
			cancelMessage: PendingItemUseCancelMessage.Item,
			delay: TimeSpan.FromMilliseconds(5000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteStigmaChargeAsync(
					player,
					plan,
					sourceTemplate,
					targetTemplate,
					targetItem.ObjectId,
					targetItem.ItemId,
					targetItem.IsEquipped,
					staticData,
					cancellationToken);
			},
			cancelTargetObjectId: targetItem.ObjectId,
			cancelEndState: 2);
	}

	private async Task CompleteStigmaChargeAsync(
		Player player,
		StigmaChargePlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateSummary targetTemplate,
		int targetObjectId,
		int targetItemId,
		bool targetWasEquipped,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveStigmaChargeMutationAsync(
				player,
				plan.TargetItemUpdate,
				plan.DeletedTargetItemObjectId,
				plan.SourceItemUpdate,
				plan.DeletedSourceItemObjectId,
				cancellationToken);
		if (!saved)
			return;

		player.InventoryItems = plan.InventoryItems;
		if (plan.AddedSkills.Count > 0 || plan.RemovedSkills.Count > 0)
			player.Skills = plan.Skills;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				targetObjectId,
				targetItemId,
				0,
				plan.EnchantSucceeded ? 1 : 2,
				1));

		if (plan.SourceItemUpdate != null)
			await SendPacketAsync(new SmInventoryUpdateItem(
				plan.SourceItemUpdate,
				sourceTemplate,
				SmInventoryUpdateItem.DecreaseStigmaUse,
				GetGeneralInfoWarehouseRestrictionFlag(plan.SourceItemUpdate.ItemId, staticData.ItemRestrictionCleanups)));
		else if (plan.DeletedSourceItemObjectId.HasValue && plan.DeletedSourceItemObjectId != plan.DeletedTargetItemObjectId)
			await SendPacketAsync(new SmDeleteItem(plan.DeletedSourceItemObjectId.Value));

		foreach (var removedSkill in plan.RemovedSkills)
			await SendPacketAsync(new SmSkillRemove(removedSkill));
		foreach (var hiddenSkillMessage in plan.HiddenSkillDeleteMessages)
			await SendPacketAsync(SmSystemMessage.StigmaDeleteHiddenSkill(
				hiddenSkillMessage.FirstSkillName,
				hiddenSkillMessage.SkillLevel,
				hiddenSkillMessage.SecondSkillName));
		foreach (var addedSkill in plan.AddedSkills)
			await SendPacketAsync(new SmSkillList([addedSkill], addedSkill.SkillType >= 3 ? 1402891 : 1300401));

		if (plan.EnchantSucceeded)
		{
			await SendPacketAsync(SmSystemMessage.StigmaEnchantSuccess(plan.ItemName));
			if (plan.TargetItemUpdate != null)
				await SendPacketAsync(new SmInventoryUpdateItem(
					plan.TargetItemUpdate,
					targetTemplate,
					SmInventoryUpdateItem.DecreaseItemUse,
					GetGeneralInfoWarehouseRestrictionFlag(plan.TargetItemUpdate.ItemId, staticData.ItemRestrictionCleanups)));
		}
		else
		{
			if (plan.TargetItemUpdate != null)
				await SendPacketAsync(new SmInventoryUpdateItem(
					plan.TargetItemUpdate,
					targetTemplate,
					SmInventoryUpdateItem.DecreaseStigmaUse,
					GetGeneralInfoWarehouseRestrictionFlag(plan.TargetItemUpdate.ItemId, staticData.ItemRestrictionCleanups)));
			else if (plan.DeletedTargetItemObjectId.HasValue)
				await SendPacketAsync(new SmDeleteItem(plan.DeletedTargetItemObjectId.Value));
			await SendPacketAsync(SmSystemMessage.StigmaEnchantFail(plan.ItemName));
		}

		if (targetWasEquipped)
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
	}

	private async Task HandleEnchantItemAsync(Player player, CmManastone packet)
	{
		// Java parity: network/aion/clientpackets/CM_MANASTONE.runImpl actionType 1 + services/EnchantService.enchantItemAct.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null)
			return;

		var sourceItem = player.InventoryItems.FirstOrDefault(item =>
			item.ObjectId == packet.StoneObjectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
		var sourceTemplate = sourceItem == null ? null : itemTemplates.GetItemTemplate(sourceItem.ItemId);
		var plan = EnchantService.CreateEnchantItemPlan(
			player,
			packet.TargetItemObjectId,
			packet.StoneObjectId,
			itemTemplates,
			supplementObjectId: packet.SupplementObjectId,
			enchantmentStoneBaseChances: _options.Rates.EnchantmentStoneBaseChances,
			enchantmentStoneAmplifiedChances: _options.Rates.EnchantmentStoneAmplifiedChances);
		if (!plan.Succeeded)
		{
			await SendEnchantFailureMessageAsync(plan);
			return;
		}

		if (sourceTemplate == null)
			return;

		var targetTemplate = plan.TargetItemUpdate == null
			? player.InventoryItems
				.FirstOrDefault(item => item.ObjectId == packet.TargetItemObjectId)
				?.ItemId is { } targetItemId
				? itemTemplates.GetItemTemplate(targetItemId)
				: null
			: itemTemplates.GetItemTemplate(plan.TargetItemUpdate.ItemId);
		if (targetTemplate == null)
			return;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				packet.TargetItemObjectId,
				packet.StoneObjectId,
				sourceTemplate.TemplateId,
				4000,
				0,
				0,
				1,
				0,
				0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: packet.StoneObjectId,
			itemTemplateId: sourceTemplate.TemplateId,
			targetItemName: plan.ItemName,
			cancelMessage: PendingItemUseCancelMessage.EnchantItem,
			delay: TimeSpan.FromMilliseconds(4000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				if (!player.InventoryItems.Any(item => item.ObjectId == packet.TargetItemObjectId))
				{
					await SendPacketAsync(SmSystemMessage.EnchantItemNoTargetItem(), cancellationToken);
					await BroadcastItemUsageAnimationAsync(
						player,
						new SmItemUsageAnimation(
							player.ObjectId,
							packet.StoneObjectId,
							sourceTemplate.TemplateId,
							0,
							2,
							0));
					return;
				}

				await CompleteEnchantItemAsync(player, packet, plan, sourceTemplate, targetTemplate, staticData, cancellationToken);
			});
	}

	private async Task CompleteEnchantItemAsync(
		Player player,
		CmManastone packet,
		EnchantItemPlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateSummary targetTemplate,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		await CompleteEnchantItemAsync(
			player,
			packet.StoneObjectId,
			packet.TargetItemObjectId,
			plan,
			sourceTemplate,
			targetTemplate,
			staticData.ItemTemplates,
			staticData.ItemRestrictionCleanups,
			staticData.SkillTemplates,
			staticData,
			cancellationToken);
	}

	private async Task CompleteEnchantItemAsync(
		Player player,
		int stoneObjectId,
		int targetItemObjectId,
		EnchantItemPlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateSummary targetTemplate,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable itemRestrictionCleanups,
		SkillTemplateTable? skillTemplates,
		StaticData? staticData,
		CancellationToken cancellationToken)
	{
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveEnchantItemMutationAsync(
				player,
				plan.TargetItemUpdate,
				plan.DeletedTargetItemObjectId,
				plan.SourceItemUpdate,
				plan.DeletedSourceItemObjectId,
				plan.SupplementItemUpdates,
				plan.DeletedSupplementItemObjectIds,
				cancellationToken);
		if (!saved)
			return;

		player.InventoryItems = plan.InventoryItems;
		if (plan.AddedBuffSkills.Count > 0 || plan.RemovedBuffSkills.Count > 0)
			player.Skills = plan.Skills;
		foreach (var supplementUpdate in plan.SupplementItemUpdates)
		{
			var supplementTemplate = itemTemplates.GetItemTemplate(supplementUpdate.ItemId);
			if (supplementTemplate != null)
				await SendPacketAsync(new SmInventoryUpdateItem(
					supplementUpdate,
					supplementTemplate,
					SmInventoryUpdateItem.DecreaseItemUse,
					GetGeneralInfoWarehouseRestrictionFlag(supplementUpdate.ItemId, itemRestrictionCleanups)));
		}
		foreach (var deletedSupplementItemObjectId in plan.DeletedSupplementItemObjectIds)
			await SendPacketAsync(new SmDeleteItem(deletedSupplementItemObjectId, SmDeleteItem.UseDeleteType));
		await SendItemUseMutationAsync(plan.SourceItemUpdate, plan.DeletedSourceItemObjectId, sourceTemplate, itemRestrictionCleanups);

		foreach (var removedSkill in plan.RemovedBuffSkills)
			await SendPacketAsync(new SmSkillRemove(removedSkill));
		foreach (var addedSkill in plan.AddedBuffSkills)
			await SendPacketAsync(new SmSkillList([addedSkill], 1300050));

		if (plan.EnchantSucceeded)
		{
			await SendPacketAsync(SmSystemMessage.EnchantItemSucceedNew(plan.ItemName, plan.NewEnchantLevel));
			if (plan.EnchantBuffSkillId != 0)
			{
				var skillTemplate = skillTemplates?.GetSkillTemplate(plan.EnchantBuffSkillId);
				var skillName = skillTemplate?.GetClientName() ?? skillTemplate?.Name ?? plan.EnchantBuffSkillId.ToString();
				await SendPacketAsync(SmSystemMessage.ExceedSkillEnchant(plan.ItemName, plan.NewEnchantLevel, skillName));
			}

			if (_options.Custom.EnableEnchantAnnounce && _connectionRegistry != null && plan.NewEnchantLevel is 15 or 20)
			{
				var announce = plan.NewEnchantLevel == 15
					? SmSystemMessage.EnchantItemSucceeded15(player.Name, plan.ItemName)
					: SmSystemMessage.EnchantItemSucceeded20(player.Name, plan.ItemName);
				await _connectionRegistry.BroadcastToWorldAsync(
					announce,
					otherPlayer => string.Equals(otherPlayer.Race, player.Race, StringComparison.Ordinal));
			}
		}
		else
		{
			await SendPacketAsync(SmSystemMessage.EnchantItemFailed(plan.ItemName));
		}

		if (plan.TargetItemUpdate != null)
			await SendPacketAsync(new SmInventoryUpdateItem(
				plan.TargetItemUpdate,
				targetTemplate,
				updateType: 0,
				GetGeneralInfoWarehouseRestrictionFlag(plan.TargetItemUpdate.ItemId, itemRestrictionCleanups)));
		else if (plan.DeletedTargetItemObjectId.HasValue)
			await SendPacketAsync(new SmDeleteItem(plan.DeletedTargetItemObjectId.Value));

		if (plan.TargetDestroyed)
			await SendPacketAsync(SmSystemMessage.EnchantType1EnchantFail(plan.ItemName));

		if (plan.RefreshStats && staticData != null)
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				stoneObjectId,
				sourceTemplate.TemplateId,
				0,
				plan.EnchantSucceeded ? 1 : 2,
				0));
	}

	private async Task SchedulePendingItemUseAsync(
		Player player,
		int itemObjectId,
		int itemTemplateId,
		string targetItemName,
		PendingItemUseCancelMessage cancelMessage,
		TimeSpan delay,
		Func<CancellationToken, Task> completeAsync,
		int? cancelTargetObjectId = null,
		int cancelEndState = 3,
		int cancelUnknown3 = 0,
		int? removeCooldownDelayIdOnCancel = null,
		bool preserveOnEmotion = false,
		bool cancelAnimationToSelfOnly = false)
	{
		// Java parity: controllers/CreatureController.addTask(TaskId.ITEM_USE) + ThreadPoolManager.schedule.
		if (_threadPoolManager == null || delay <= TimeSpan.Zero)
		{
			await completeAsync(CancellationToken.None);
			return;
		}

		if (_pendingItemUse?.Task.Cancel() == true)
			CleanupPendingItemUse(player, _pendingItemUse, canceled: true);

		player.UsingItemObjectId = itemObjectId;
		ScheduledTask? scheduledTask = null;
		scheduledTask = _threadPoolManager.Schedule(
			async cancellationToken =>
			{
				try
				{
					if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_activePlayer, player))
						return;

					await completeAsync(cancellationToken);
				}
				finally
				{
					var pendingItemUse = _pendingItemUse;
					if (pendingItemUse != null && ReferenceEquals(pendingItemUse.Task, scheduledTask))
					{
						CleanupPendingItemUse(player, pendingItemUse, canceled: cancellationToken.IsCancellationRequested);
						_pendingItemUse = null;
					}
				}
			},
			delay);
		_pendingItemUse = new PendingItemUse(
			scheduledTask,
			itemObjectId,
			itemTemplateId,
			targetItemName,
			cancelMessage,
			cancelTargetObjectId,
			cancelEndState,
			cancelUnknown3,
			removeCooldownDelayIdOnCancel,
			preserveOnEmotion,
			cancelAnimationToSelfOnly);
	}

	private async Task CancelPendingItemUseOnMoveAsync(Player player)
	{
		// Java parity: controllers/PlayerController.onStartMove -> cancelUseItem and EnchantItemAction StartMovingListener.
		await CancelPendingItemUseAsync(player);
	}

	private async Task CancelPendingItemUseOnEmotionAsync(Player player)
	{
		// Java parity: network/aion/clientpackets/CM_EMOTION.runImpl -> PlayerController.cancelUseItem.
		// RideAction is the Java exception: emotions do not cancel getting on a mount.
		if (_pendingItemUse?.PreserveOnEmotion == true)
			return;

		await CancelPendingItemUseAsync(player);
	}

	private async Task CancelPendingItemUseAsync(Player player)
	{
		var pendingItemUse = _pendingItemUse;
		if (pendingItemUse == null || !pendingItemUse.Task.Cancel())
			return;

		_pendingItemUse = null;
		CleanupPendingItemUse(player, pendingItemUse, canceled: true);
		var cancelAnimation = CreateCancelItemUsageAnimation(player, pendingItemUse);
		if (pendingItemUse.CancelAnimationToSelfOnly)
			await SendPacketAsync(cancelAnimation);
		else
			await BroadcastItemUsageAnimationAsync(player, cancelAnimation);
		if (pendingItemUse.CancelMessage != PendingItemUseCancelMessage.None)
		{
			await SendPacketAsync(pendingItemUse.CancelMessage switch
			{
				PendingItemUseCancelMessage.EnchantItem => SmSystemMessage.EnchantItemCanceled(pendingItemUse.TargetItemName),
				PendingItemUseCancelMessage.ItemIdentify => SmSystemMessage.ItemIdentifyCanceled(pendingItemUse.TargetItemName),
				PendingItemUseCancelMessage.ItemAuthorize => SmSystemMessage.ItemAuthorizeCancel(pendingItemUse.TargetItemName),
				PendingItemUseCancelMessage.Item => SmSystemMessage.ItemCanceled(),
				PendingItemUseCancelMessage.ItemCharge => SmSystemMessage.ItemChargeCanceled(),
				PendingItemUseCancelMessage.ItemCharge2 => SmSystemMessage.ItemCharge2Canceled(),
				PendingItemUseCancelMessage.ItemReidentify => SmSystemMessage.ItemReidentifyCanceled(pendingItemUse.TargetItemName),
				PendingItemUseCancelMessage.GodstoneSocket => SmSystemMessage.GiveItemProcCancel(pendingItemUse.TargetItemName),
				PendingItemUseCancelMessage.SoulBind => SmSystemMessage.SoulBoundItemCanceled(pendingItemUse.TargetItemName),
				PendingItemUseCancelMessage.Decompose => SmSystemMessage.DecomposeItemCanceled(pendingItemUse.TargetItemName),
				_ => SmSystemMessage.GiveItemOptionCanceled(pendingItemUse.TargetItemName),
			});
		}
	}

	private static void CleanupPendingItemUse(Player player, PendingItemUse pendingItemUse, bool canceled)
	{
		// Java parity: PlayerController.cancelUseItem clears Player.usingItem; selected ItemUseObserver.abort branches also remove item cooldowns.
		if (player.UsingItemObjectId == pendingItemUse.ItemObjectId)
			player.UsingItemObjectId = 0;

		if (canceled && pendingItemUse.RemoveCooldownDelayIdOnCancel.HasValue)
			player.RemoveItemCooldown(pendingItemUse.RemoveCooldownDelayIdOnCancel.Value);
	}

	private static SmItemUsageAnimation CreateCancelItemUsageAnimation(Player player, PendingItemUse pendingItemUse)
	{
		if (pendingItemUse.CancelTargetObjectId.HasValue)
		{
			return new SmItemUsageAnimation(
				player.ObjectId,
				pendingItemUse.CancelTargetObjectId.Value,
				pendingItemUse.ItemObjectId,
				pendingItemUse.ItemTemplateId,
				0,
				pendingItemUse.CancelEndState,
				0,
				0,
				1,
				pendingItemUse.CancelUnknown3);
		}

		return new SmItemUsageAnimation(
			player.ObjectId,
			pendingItemUse.ItemObjectId,
			pendingItemUse.ItemTemplateId,
			0,
			pendingItemUse.CancelEndState,
			pendingItemUse.CancelUnknown3);
	}

	private async Task SendEnchantFailureMessageAsync(EnchantItemPlan plan)
	{
		switch (plan.Failure)
		{
			case EnchantItemFailure.NoTargetItem:
				await SendPacketAsync(SmSystemMessage.EnchantItemNoTargetItem());
				break;
			case EnchantItemFailure.CannotEnchant:
				await SendPacketAsync(SmSystemMessage.GiveItemOptionCannotBeGivenOption(plan.ItemName, plan.EnchantmentStoneName));
				break;
			case EnchantItemFailure.CannotEnchantMoreTime:
				await SendPacketAsync(SmSystemMessage.GiveItemOptionCannotBeGivenOptionMoreTime(plan.ItemName, plan.EnchantmentStoneName));
				break;
			case EnchantItemFailure.AmplifiedNeedsOmega:
				await SendPacketAsync(SmSystemMessage.ExceedCannotEnchantAmplified(plan.EnchantmentStoneName));
				break;
			case EnchantItemFailure.WrongSupplementLevel:
				await SendPacketAsync(SmSystemMessage.ItemEnchantAssistantNoRightItem());
				break;
		}
	}

	private async Task HandleSocketManastoneAsync(Player player, CmManastone packet)
	{
		// Java parity: network/aion/clientpackets/CM_MANASTONE.runImpl actionType 2 + services/EnchantService.socketManastoneAct.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null)
			return;

		var sourceItem = player.InventoryItems.FirstOrDefault(item =>
			item.ObjectId == packet.StoneObjectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
		var sourceTemplate = sourceItem == null ? null : itemTemplates.GetItemTemplate(sourceItem.ItemId);
		var plan = EnchantService.CreateSocketManastonePlan(
			player,
			packet.TargetItemObjectId,
			packet.StoneObjectId,
			packet.TargetFusedSlot,
			itemTemplates,
			supplementObjectId: packet.SupplementObjectId,
			manastoneChances: _options.Rates.ManastoneChances);
		if (!plan.Succeeded)
		{
			if (plan.Failure == ManastoneSocketFailure.NoTargetItem)
				await SendPacketAsync(SmSystemMessage.GiveItemOptionNoTargetItem());
			else if (plan.Failure == ManastoneSocketFailure.WrongSupplementLevel)
				await SendPacketAsync(SmSystemMessage.ItemEnchantAssistantNoRightItem());
			return;
		}

		if (plan.TargetItemUpdate == null || sourceTemplate == null)
			return;

		var targetTemplate = itemTemplates.GetItemTemplate(plan.TargetItemUpdate.ItemId);
		if (targetTemplate == null)
			return;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				packet.TargetItemObjectId,
				packet.StoneObjectId,
				sourceTemplate.TemplateId,
				2000,
				0,
				0,
				1,
				0,
				0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: packet.StoneObjectId,
			itemTemplateId: sourceTemplate.TemplateId,
			targetItemName: plan.ItemName,
			cancelMessage: PendingItemUseCancelMessage.ManastoneSocket,
			delay: TimeSpan.FromMilliseconds(2000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				if (!player.InventoryItems.Any(item => item.ObjectId == packet.TargetItemObjectId))
				{
					await SendPacketAsync(SmSystemMessage.EnchantItemNoTargetItem(), cancellationToken);
					await BroadcastItemUsageAnimationAsync(
						player,
						new SmItemUsageAnimation(
							player.ObjectId,
							packet.StoneObjectId,
							sourceTemplate.TemplateId,
							0,
							2,
							0));
					return;
				}

				await CompleteSocketManastoneAsync(player, packet, plan, sourceTemplate, targetTemplate, staticData, cancellationToken);
			});
	}

	private async Task CompleteSocketManastoneAsync(
		Player player,
		CmManastone packet,
		ManastoneSocketPlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateSummary targetTemplate,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		if (plan.TargetItemUpdate == null)
			return;

		var itemTemplates = staticData.ItemTemplates;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveManastoneSocketMutationAsync(
				player,
				plan.TargetItemUpdate,
				plan.AddedStone,
				plan.AddedCategory,
				plan.SourceItemUpdate,
				plan.DeletedSourceItemObjectId,
				plan.SupplementItemUpdates,
				plan.DeletedSupplementItemObjectIds,
				cancellationToken);
		if (!saved)
			return;

		player.InventoryItems = plan.InventoryItems;
		foreach (var supplementUpdate in plan.SupplementItemUpdates)
		{
			var supplementTemplate = itemTemplates.GetItemTemplate(supplementUpdate.ItemId);
			if (supplementTemplate != null)
				await SendPacketAsync(new SmInventoryUpdateItem(
					supplementUpdate,
					supplementTemplate,
					SmInventoryUpdateItem.DecreaseItemUse,
					GetGeneralInfoWarehouseRestrictionFlag(supplementUpdate.ItemId, staticData.ItemRestrictionCleanups)));
		}
		foreach (var deletedSupplementItemObjectId in plan.DeletedSupplementItemObjectIds)
			await SendPacketAsync(new SmDeleteItem(deletedSupplementItemObjectId, SmDeleteItem.UseDeleteType));
		await SendItemUseMutationAsync(plan.SourceItemUpdate, plan.DeletedSourceItemObjectId, sourceTemplate, staticData.ItemRestrictionCleanups);
		await SendPacketAsync(
			plan.SocketSucceeded
				? SmSystemMessage.GiveItemOptionSucceed(plan.ItemName)
				: SmSystemMessage.GiveItemOptionFailed(plan.ItemName));
		await SendPacketAsync(new SmInventoryUpdateItem(
			plan.TargetItemUpdate,
			targetTemplate,
			updateType: 0,
			GetGeneralInfoWarehouseRestrictionFlag(plan.TargetItemUpdate.ItemId, staticData.ItemRestrictionCleanups)));
		if (plan.RefreshStats && plan.SocketSucceeded)
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				packet.StoneObjectId,
				sourceTemplate.TemplateId,
				0,
				plan.SocketSucceeded ? 1 : 2,
				0));
	}

	private async Task HandleSocketGodstoneAsync(Player player, CmManastone packet)
	{
		// Java parity: network/aion/clientpackets/CM_MANASTONE.runImpl actionType 4 + services/item/ItemSocketService.socketGodstone.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null)
			return;
		var itemTemplates = staticData.ItemTemplates;

		var sourceItem = player.InventoryItems.FirstOrDefault(item =>
			item.ObjectId == packet.StoneObjectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
		var plan = ItemSocketService.CreateSocketGodstonePlan(
			player,
			packet.TargetItemObjectId,
			packet.StoneObjectId,
			itemTemplates);
		if (!plan.Succeeded)
		{
			await SendPacketAsync(CreateSocketGodstoneFailureMessage(plan));
			return;
		}

		var sourceTemplate = sourceItem == null ? null : itemTemplates.GetItemTemplate(sourceItem.ItemId);
		if (plan.TargetItemUpdate == null || sourceTemplate == null)
			return;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				packet.StoneObjectId,
				sourceTemplate.TemplateId,
				2000,
				0,
				0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: packet.StoneObjectId,
			itemTemplateId: sourceTemplate.TemplateId,
			targetItemName: plan.ItemName,
			cancelMessage: PendingItemUseCancelMessage.GodstoneSocket,
			delay: TimeSpan.FromMilliseconds(2000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteSocketGodstoneAsync(player, packet.StoneObjectId, plan, sourceTemplate, itemTemplates, staticData.ItemRestrictionCleanups, cancellationToken);
			});
	}

	private async Task CompleteSocketGodstoneAsync(
		Player player,
		int stoneObjectId,
		GodstoneSocketPlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups,
		CancellationToken cancellationToken)
	{
		if (plan.TargetItemUpdate == null)
			return;

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveGodstoneSocketMutationAsync(
				player,
				plan.TargetItemUpdate,
				plan.SourceItemUpdate,
				plan.DeletedSourceItemObjectId,
				cancellationToken);
		if (!saved)
			return;

		player.InventoryItems = plan.InventoryItems;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				stoneObjectId,
				sourceTemplate.TemplateId,
				0,
				1,
				0));

		await SendItemUseMutationAsync(plan.SourceItemUpdate, plan.DeletedSourceItemObjectId, sourceTemplate, itemRestrictionCleanups);

		await SendPacketAsync(SmSystemMessage.GiveItemProcEnchantedTargetItem(plan.ItemName));
		if (itemTemplates.GetItemTemplate(plan.TargetItemUpdate.ItemId) is { } targetTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(
				plan.TargetItemUpdate,
				targetTemplate,
				updateType: 0,
				GetGeneralInfoWarehouseRestrictionFlag(plan.TargetItemUpdate.ItemId, itemRestrictionCleanups)));
	}

	private static SmSystemMessage CreateSocketGodstoneFailureMessage(GodstoneSocketPlan plan)
	{
		return plan.Failure switch
		{
			GodstoneSocketFailure.NoTargetItem => SmSystemMessage.GiveItemProcNoTargetItem(),
			GodstoneSocketFailure.TargetItemEquipped => SmSystemMessage.GiveItemProcCannotGiveToEquippedItem(),
			GodstoneSocketFailure.TargetNotProcGivable => SmSystemMessage.GiveItemProcNotProcGivableItem(plan.ItemName),
			GodstoneSocketFailure.NoGodstoneItem => SmSystemMessage.GiveItemProcNoProcGiveItem(),
			_ => SmSystemMessage.GiveItemProcNoTargetItem(),
		};
	}

	private async Task HandleAmplifyItemAsync(Player player, CmManastone packet)
	{
		// Java parity: network/aion/clientpackets/CM_MANASTONE.runImpl actionType 8 + services/EnchantService.amplifyItem.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null)
			return;

		var targetItem = player.InventoryItems.FirstOrDefault(item => item.ObjectId == packet.TargetItemObjectId);
		var materialItem = player.InventoryItems.FirstOrDefault(item =>
			item.ObjectId == packet.SupplementObjectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
		var toolItem = player.InventoryItems.FirstOrDefault(item =>
			item.ObjectId == packet.StoneObjectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);

		var plan = EnchantService.CreateAmplificationPlan(
			player,
			packet.TargetItemObjectId,
			packet.SupplementObjectId,
			packet.StoneObjectId,
			itemTemplates);
		if (!plan.Succeeded)
		{
			await SendPacketAsync(CreateAmplificationFailureMessage(plan));
			return;
		}

		var targetTemplate = targetItem == null ? null : itemTemplates.GetItemTemplate(targetItem.ItemId);
		var materialTemplate = materialItem == null ? null : itemTemplates.GetItemTemplate(materialItem.ItemId);
		var toolTemplate = toolItem == null ? null : itemTemplates.GetItemTemplate(toolItem.ItemId);
		if (plan.TargetItemUpdate == null || targetTemplate == null)
			return;

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveItemAmplificationMutationAsync(
				player,
				plan.TargetItemUpdate,
				plan.MaterialItemUpdate,
				plan.DeletedMaterialItemObjectId,
				plan.ToolItemUpdate,
				plan.DeletedToolItemObjectId);
		if (!saved)
			return;

		await CompleteAmplifyItemAsync(player, plan, targetTemplate, materialTemplate, toolTemplate, staticData.ItemRestrictionCleanups);
	}

	private async Task CompleteAmplifyItemAsync(
		Player player,
		AmplificationPlan plan,
		ItemTemplateSummary targetTemplate,
		ItemTemplateSummary? materialTemplate,
		ItemTemplateSummary? toolTemplate,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: services/EnchantService.amplifyItem success packet fanout.
		var targetItemUpdate = plan.TargetItemUpdate ?? throw new InvalidOperationException("Amplification success plan requires a target item update.");
		player.InventoryItems = plan.InventoryItems;
		await SendItemUseMutationAsync(plan.MaterialItemUpdate, plan.DeletedMaterialItemObjectId, materialTemplate, itemRestrictionCleanups);
		await SendItemUseMutationAsync(plan.ToolItemUpdate, plan.DeletedToolItemObjectId, toolTemplate, itemRestrictionCleanups);
		await SendPacketAsync(SmSystemMessage.ExceedSucceed(plan.ItemName));
		await SendPacketAsync(new SmInventoryUpdateItem(
			targetItemUpdate,
			targetTemplate,
			SmInventoryUpdateItem.DecreaseItemUse,
			GetGeneralInfoWarehouseRestrictionFlag(targetItemUpdate.ItemId, itemRestrictionCleanups)));
	}

	private async Task SendItemUseMutationAsync(
		InventoryItem? itemUpdate,
		int? deletedItemObjectId,
		ItemTemplateSummary? template,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
	{
		if (itemUpdate != null && template != null)
			await SendPacketAsync(new SmInventoryUpdateItem(
				itemUpdate,
				template,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(itemUpdate.ItemId, itemRestrictionCleanups)));
		else if (deletedItemObjectId.HasValue)
			await SendPacketAsync(new SmDeleteItem(deletedItemObjectId.Value, SmDeleteItem.UseDeleteType));
	}

	private static SmSystemMessage CreateAmplificationFailureMessage(AmplificationPlan plan)
	{
		return plan.Failure switch
		{
			AmplificationFailure.AlreadyAmplified => SmSystemMessage.ExceedAlready(),
			AmplificationFailure.CannotAmplify => SmSystemMessage.ExceedCannotAmplify(plan.ItemName),
			AmplificationFailure.NeedsMaxEnchant => SmSystemMessage.ExceedNeedsMaxEnchant(),
			_ => SmSystemMessage.ExceedNoTargetItem(),
		};
	}

	private async Task HandleRemoveManastoneAsync(Player player, CmManastone packet)
	{
		// Java parity: network/aion/clientpackets/CM_MANASTONE.runImpl actionType 3 + services/item/ItemSocketService.removeManastone.
		if (packet.NpcObjectId == 0 || player.TargetObjectId != packet.NpcObjectId)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null)
			return;
		var itemTemplates = staticData.ItemTemplates;

		var plan = ItemSocketService.CreateRemoveManastonePlan(
			player,
			packet.TargetItemObjectId,
			packet.SlotNumber,
			isFusionSocket: packet.TargetFusedSlot != 1,
			itemTemplates);
		if (!plan.Succeeded)
		{
			await SendPacketAsync(CreateRemoveManastoneFailureMessage(plan));
			return;
		}

		if (plan.ItemUpdate == null || plan.KinahItemUpdate == null)
			return;

		await CompleteRemoveManastoneAsync(player, plan, itemTemplates, staticData.ItemRestrictionCleanups);
	}

	private async Task CompleteRemoveManastoneAsync(
		Player player,
		ManastoneRemovalPlan plan,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		if (plan.ItemUpdate == null || plan.KinahItemUpdate == null)
			return;

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveManastoneRemovalMutationAsync(
				player,
				plan.ItemUpdate.ObjectId,
				plan.RemovedSlot,
				plan.RemovedCategory,
				plan.KinahItemUpdate);
		if (!saved)
			return;

		player.InventoryItems = plan.InventoryItems;
		if (itemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(plan.KinahItemUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));

		await SendPacketAsync(SmSystemMessage.RemoveItemOptionSucceed(plan.ItemName));
		if (itemTemplates.GetItemTemplate(plan.ItemUpdate.ItemId) is { } itemTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(
				plan.ItemUpdate,
				itemTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(plan.ItemUpdate.ItemId, itemRestrictionCleanups)));
	}

	private static SmSystemMessage CreateRemoveManastoneFailureMessage(ManastoneRemovalPlan plan)
	{
		return plan.Failure switch
		{
			ManastoneRemovalFailure.NoTargetItem => SmSystemMessage.RemoveItemOptionNoTargetItem(),
			ManastoneRemovalFailure.NoOptionToRemove => SmSystemMessage.RemoveItemOptionNoOptionToRemove(plan.ItemName),
			ManastoneRemovalFailure.InvalidSlot => SmSystemMessage.RemoveItemOptionInvalidSlot(plan.ItemName),
			ManastoneRemovalFailure.NotEnoughKinah => SmSystemMessage.RemoveItemOptionNotEnoughGold(plan.ItemName),
			_ => SmSystemMessage.RemoveItemOptionNoTargetItem(),
		};
	}

	private async Task StartSoulBindRequestAsync(Player player, EquipmentChangeResult change)
	{
		// Java parity: model/gameobjects/player/Equipment.soulBindItem putRequest + SM_QUESTION_WINDOW.
		var pendingRequest = new PendingSoulBindRequest(change.SoulBindItemObjectId, change.SoulBindSlot, change.ItemName);
		if (!player.ResponseRequester.PutRequest(
			SmQuestionWindow.SoulBoundItemConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.SoulBind, pendingRequest)))
		{
			await SendPacketAsync(SmSystemMessage.SoulBoundCloseOtherMsgBoxAndRetry());
			return;
		}

		player.PendingSoulBindRequest = pendingRequest;
		await SendPacketAsync(new SmQuestionWindow(SmQuestionWindow.SoulBoundItemConfirm, 0, 0, change.ItemName));
	}

	private async Task ApplyEquipmentChangeAsync(
		Player player,
		EquipmentChangeResult change,
		ItemTemplateTable itemTemplates,
		StaticData staticData)
	{
		// Java parity: model/gameobjects/player/Equipment.equip/unEquip/switchHands persistence and fanout.
		var saved = _playerEnterWorldService == null
			|| change.PersistedItems.Count == 0
			|| await _playerEnterWorldService.SaveEquipmentMutationAsync(player, change.PersistedItems, change.KinahItemUpdate);
		if (!saved)
			return;

		player.InventoryItems = EquipmentService.NormalizeImmediatelySavedItems(
			change.InventoryItems,
			change.PersistedItems,
			change.KinahItemUpdate);
		if (change.PowerShardDeactivated)
		{
			// Java parity: model/gameobjects/player/Equipment.unEquipItem POWER_SHARDS branch sends SM_EMOTION to owner only.
			player.SetCreatureState(PlayerCreatureState.Powershard, enabled: false);
			await SendPacketAsync(new SmEmotion(player, EmotionType.PowershardOff));
		}
		if (change.FinalSkills.Count > 0 || change.SkillListUpdates.Count > 0 || change.SkillRemoveUpdates.Count > 0)
			player.Skills = change.FinalSkills;
		foreach (var update in change.InventoryUpdateItems)
		{
			if (itemTemplates.GetItemTemplate(update.ItemId) is { } template)
				await SendPacketAsync(new SmInventoryUpdateItem(update, template, SmInventoryUpdateItem.EquipUnequip));
		}

		if (change.KinahItemUpdate != null && itemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(change.KinahItemUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));

		foreach (var skillName in change.StigmaSkillRemoveMessages)
			await SendPacketAsync(SmSystemMessage.StigmaSkillUnavailable(skillName));
		foreach (var removedSkill in change.SkillRemoveUpdates)
			await SendPacketAsync(new SmSkillRemove(removedSkill));
		foreach (var hiddenSkillMessage in change.HiddenStigmaSkillRemoveMessages)
			await SendPacketAsync(SmSystemMessage.StigmaDeleteHiddenSkill(
				hiddenSkillMessage.FirstSkillName,
				hiddenSkillMessage.SkillLevel,
				hiddenSkillMessage.SecondSkillName));
		foreach (var addedSkill in change.SkillListUpdates)
			await SendPacketAsync(new SmSkillList([addedSkill], addedSkill.SkillType >= 3 ? 1402891 : 1300401));
		foreach (var itemName in change.RankLimitedUnequipMessages)
			await SendPacketAsync(SmSystemMessage.UnequipRankItem(itemName));

		if (change.RefreshStats)
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
		if (change.BroadcastAppearance)
		{
			var appearancePacket = new SmUpdatePlayerAppearance(player);
			if (_connectionRegistry != null)
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, appearancePacket, includeSourcePlayer: true);
			else
				await SendPacketAsync(appearancePacket);
		}
	}

	private async Task ApplyAbyssRankChangedSideEffectsAsync(Player player, int oldAbyssRank, StaticData staticData)
	{
		// Java parity: services/abyss/AbyssPointsService.onRankChanged.
		if (oldAbyssRank == player.AbyssRank.Rank)
			return;

		if (_connectionRegistry != null)
		{
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(
				player.Position,
				player.ObjectId,
				SmAbyssRankUpdate.RankChange(player));
		}

		var rankLimitChange = EquipmentService.CheckRankLimitItems(player, staticData.ItemTemplates);
		if (rankLimitChange.Changed || rankLimitChange.RankLimitedUnequipMessages.Count > 0)
			await ApplyEquipmentChangeAsync(player, rankLimitChange, staticData.ItemTemplates, staticData);

		var abyssSkillUpdate = AbyssSkillService.UpdateSkills(player, _options.Custom.TopRankingXformMinRank);
		if (abyssSkillUpdate.Changed)
		{
			player.Skills = abyssSkillUpdate.Skills;
			foreach (var removedSkill in abyssSkillUpdate.RemovedSkills)
				await SendPacketAsync(new SmSkillRemove(removedSkill));
			foreach (var addedSkill in abyssSkillUpdate.AddedSkills)
				await SendPacketAsync(new SmSkillList([addedSkill], 1300050));
			// Java parity: full passive SkillEngine effect apply/remove fanout remains a later SkillEngine slice.
		}
	}

	private async Task HandleSoulBindQuestionResponseAsync(Player player, CmQuestionResponse packet)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond, which removes
		// the Equipment.soulBindItem RequestResponseHandler before invoking accept/deny behavior.
		var pendingRequest = player.PendingSoulBindRequest;
		if (pendingRequest == null || packet.QuestionId != SmQuestionWindow.SoulBoundItemConfirm)
			return;

		var dispatch = player.ResponseRequester.Respond(packet.QuestionId, packet.Response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.SoulBind)
		{
			player.PendingSoulBindRequest = null;
			return;
		}

		var request = dispatch.Request.Payload as PendingSoulBindRequest ?? pendingRequest;
		if (request == null || packet.QuestionId != SmQuestionWindow.SoulBoundItemConfirm)
			return;

		player.PendingSoulBindRequest = null;
		if (packet.Response == 0)
		{
			await SendPacketAsync(SmSystemMessage.SoulBoundItemCanceled(request.ItemName));
			return;
		}

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null)
			return;

		var currentItem = player.InventoryItems.FirstOrDefault(item =>
			item.ObjectId == request.ItemObjectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
		if (currentItem == null)
			return;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, currentItem.ObjectId, currentItem.ItemId, 5000, 4, 0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: currentItem.ObjectId,
			itemTemplateId: currentItem.ItemId,
			targetItemName: request.ItemName,
			cancelMessage: PendingItemUseCancelMessage.SoulBind,
			delay: TimeSpan.FromMilliseconds(5000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteSoulBindAsync(player, request, itemTemplates, staticData, currentItem.ObjectId, currentItem.ItemId, cancellationToken);
			},
			cancelEndState: 8);
	}

	private async Task CompleteSoulBindAsync(
		Player player,
		PendingSoulBindRequest request,
		ItemTemplateTable itemTemplates,
		StaticData staticData,
		int itemObjectId,
		int itemId,
		CancellationToken cancellationToken)
	{
		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: request.Slot,
			itemObjectId: request.ItemObjectId,
			itemTemplates,
			staticData.SkillTemplates,
			staticData.PlayerExperienceTable,
			soulBindConfirmed: true,
			skillTree: staticData.SkillTree,
			stigmaSlotQuestMembership: _options.Membership.StigmaSlotQuest);
		if (!change.Changed || cancellationToken.IsCancellationRequested)
			return;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, itemObjectId, itemId, 0, 6, 0));
		await SendPacketAsync(SmSystemMessage.SoulBoundItemSucceed(request.ItemName));
		await ApplyEquipmentChangeAsync(player, change, itemTemplates, staticData);
	}

	private async Task SendEquipFailureMessageAsync(EquipmentChangeResult change)
	{
		// Java parity: model/gameobjects/player/Equipment.equipItem emits SM_SYSTEM_MESSAGE for validation failures.
		switch (change.Failure)
		{
			case EquipmentChangeFailure.InventoryFull:
				await SendPacketAsync(SmSystemMessage.UiInventoryFull());
				break;
			case EquipmentChangeFailure.InvalidClass:
				await SendPacketAsync(SmSystemMessage.CannotUseItemInvalidClass());
				break;
			case EquipmentChangeFailure.TooLowLevel:
				await SendPacketAsync(SmSystemMessage.CannotUseItemTooLowLevel(change.ItemName, change.RequiredLevel));
				break;
			case EquipmentChangeFailure.TooHighLevel:
				await SendPacketAsync(SmSystemMessage.CannotUseItemTooHighLevel(change.MaxLevel, change.ItemName));
				break;
			case EquipmentChangeFailure.InvalidRace:
				await SendPacketAsync(SmSystemMessage.CannotUseItemInvalidRace());
				break;
			case EquipmentChangeFailure.InvalidGender:
				await SendPacketAsync(SmSystemMessage.CannotUseItemInvalidGender());
				break;
			case EquipmentChangeFailure.InvalidRank:
				await SendPacketAsync(SmSystemMessage.CannotUseItemInvalidRank(change.RankName));
				break;
			case EquipmentChangeFailure.StigmaNotEnoughKinah:
				await SendPacketAsync(SmSystemMessage.StigmaNotEnoughMoney());
				break;
			case EquipmentChangeFailure.SoulBindInvalidStance:
				await SendPacketAsync(SmSystemMessage.SoulBoundInvalidStance(ChatUtil.L10n(change.SoulBindInvalidStanceL10nId)));
				break;
		}
	}

	private async Task HandleAppearanceAsync(Player player, CmAppearance packet)
	{
		// Java parity: network/aion/clientpackets/CM_APPEARANCE.runImpl type 2 cosmetic branch.
		if (packet.Type != 2)
		{
			// Character and legion rename coupons remain deferred until rename/world-cache services are ported.
			return;
		}

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		var sourceItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.ItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		if (sourceItem == null)
			return;

		var sourceTemplate = itemTemplates.GetItemTemplate(sourceItem.ItemId);
		if (sourceTemplate == null || sourceTemplate.CosmeticActionName.Length == 0)
			return;

		var cosmeticTemplate = staticData.CosmeticItems.GetCosmeticItemTemplate(sourceTemplate.CosmeticActionName);
		var plan = CosmeticItemService.CreatePlan(player, cosmeticTemplate);
		if (!plan.Succeeded)
		{
			await SendCosmeticItemFailureAsync(plan.Failure);
			return;
		}

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveCosmeticItemActionMutationAsync(player, plan.Appearance!, sourceItem.ObjectId);
		if (!saved)
			return;

		player.Appearance = plan.Appearance!;
		inventoryItems.RemoveAll(item => item.ObjectId == sourceItem.ObjectId);
		player.InventoryItems = inventoryItems.ToArray();
		await SendPacketAsync(new SmDeleteItem(sourceItem.ObjectId));

		// Java parity: PlayerController.onChangedPlayerAttributes refreshes known-list player attributes after appearance mutation.
		var playerInfo = new SmPlayerInfo(player, staticData.PlayerExperienceTable);
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, playerInfo, includeSourcePlayer: true);
		else
			await SendPacketAsync(playerInfo);
	}

	private async Task HandleItemRemodelAsync(Player player, CmItemRemodel packet)
	{
		// Java parity: network/aion/clientpackets/CM_ITEM_REMODEL.runImpl -> ItemRemodelService.remodelItem.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null)
			return;

		var itemTemplates = staticData.ItemTemplates;
		var inventoryItems = player.InventoryItems.ToList();
		var keepItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.KeepItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		var extractItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.ExtractItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		if (keepItem == null || extractItem == null)
			return;

		var keepTemplate = itemTemplates.GetItemTemplate(keepItem.ItemId);
		var extractTemplate = itemTemplates.GetItemTemplate(extractItem.ItemId);
		var extractSkinTemplate = itemTemplates.GetItemTemplate(extractItem.ItemSkin == 0 ? extractItem.ItemId : extractItem.ItemSkin);
		var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		var kinahTemplate = itemTemplates.GetItemTemplate(KinahItemId);
		if (keepTemplate == null || extractTemplate == null || extractSkinTemplate == null || kinahTemplate == null)
			return;

		var playerLevel = Math.Max(1, staticData.PlayerExperienceTable.GetLevelForExp(player.Exp));
		var plan = ItemRemodelService.CreateRemodelPlan(
			player,
			keepItem,
			keepTemplate,
			extractItem,
			extractTemplate,
			extractSkinTemplate,
			kinahItem,
			playerLevel);
		if (!plan.Succeeded)
		{
			await SendItemRemodelFailureAsync(plan);
			return;
		}

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveItemRemodelMutationAsync(
				player,
				plan.TargetItemUpdate!,
				plan.KinahItemUpdate!,
				plan.ExtractItemUpdate,
				plan.DeletedExtractItemObjectId);
		if (!saved)
			return;

		await CompleteItemRemodelAsync(
			player,
			inventoryItems,
			plan,
			keepTemplate,
			extractTemplate,
			kinahTemplate,
			staticData.ItemRestrictionCleanups);
	}

	private async Task CompleteItemRemodelAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		ItemRemodelPlan plan,
		ItemTemplateSummary keepTemplate,
		ItemTemplateSummary extractTemplate,
		ItemTemplateSummary kinahTemplate,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: services/item/ItemRemodelService.remodelItem success packet fanout.
		ReplaceInventoryItem(inventoryItems, plan.TargetItemUpdate!);
		ReplaceInventoryItem(inventoryItems, plan.KinahItemUpdate!);
		await SendPacketAsync(new SmInventoryUpdateItem(plan.KinahItemUpdate!, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
		if (plan.DeletedExtractItemObjectId.HasValue)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == plan.DeletedExtractItemObjectId.Value);
			await SendPacketAsync(new SmDeleteItem(plan.DeletedExtractItemObjectId.Value, SmDeleteItem.UseDeleteType));
		}
		else if (plan.ExtractItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, plan.ExtractItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(
				plan.ExtractItemUpdate,
				extractTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(plan.ExtractItemUpdate.ItemId, itemRestrictionCleanups)));
		}

		player.InventoryItems = inventoryItems.ToArray();
		await SendPacketAsync(new SmInventoryUpdateItem(
			plan.TargetItemUpdate!,
			keepTemplate,
			SmInventoryUpdateItem.DecreaseItemUse,
			GetGeneralInfoWarehouseRestrictionFlag(plan.TargetItemUpdate!.ItemId, itemRestrictionCleanups)));
		await SendPacketAsync(SmSystemMessage.ChangeItemSkinSucceed(keepTemplate.GetClientName() ?? keepTemplate.Name));
	}

	private async Task SendItemRemodelFailureAsync(ItemRemodelPlan plan)
	{
		var itemName = plan.FailureItem?.GetClientName() ?? plan.FailureItem?.Name ?? string.Empty;
		var otherItemName = plan.FailureOtherItem?.GetClientName() ?? plan.FailureOtherItem?.Name ?? string.Empty;
		switch (plan.Failure)
		{
			case ItemRemodelFailure.LevelLimit:
				await SendPacketAsync(SmSystemMessage.ChangeItemSkinPcLevelLimit());
				break;
			case ItemRemodelFailure.OppositeRequirement:
				await SendPacketAsync(SmSystemMessage.CantChangeSkinOppositeRequirement(itemName, otherItemName));
				break;
			case ItemRemodelFailure.NotEnoughKinah:
				await SendPacketAsync(SmSystemMessage.ChangeItemSkinNotEnoughGold(itemName));
				break;
			case ItemRemodelFailure.NotSkinnedItem:
				await SendPacketAsync(new SmMessage("That item does not have a remodeled skin to remove."));
				break;
			case ItemRemodelFailure.NotCompatible:
				await SendPacketAsync(SmSystemMessage.ChangeItemSkinNotCompatible(itemName, otherItemName));
				break;
			case ItemRemodelFailure.NotSkinChangeable:
				await SendPacketAsync(SmSystemMessage.ChangeItemSkinNotSkinChangeable(itemName));
				break;
			case ItemRemodelFailure.CannotRemoveSkinItem:
				await SendPacketAsync(SmSystemMessage.ChangeItemSkinCannotRemoveSkinItem(itemName));
				break;
		}
	}

	private async Task SendCosmeticItemFailureAsync(CosmeticItemFailure failure)
	{
		switch (failure)
		{
			case CosmeticItemFailure.InvalidRace:
				await SendPacketAsync(SmSystemMessage.CannotUseItemInvalidRace());
				break;
			case CosmeticItemFailure.InvalidGender:
				await SendPacketAsync(SmSystemMessage.CannotUseItemInvalidGender());
				break;
			case CosmeticItemFailure.Ride:
				await SendPacketAsync(SmSystemMessage.ItemRestrictionRide());
				break;
		}
	}

	private async Task HandleCompositeStonesAsync(Player player, CmCompositeStones packet)
	{
		// Java parity: network/aion/clientpackets/CM_COMPOSITE_STONES.runImpl.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		var toolItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.ToolItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		var firstItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.FirstItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		var secondItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.SecondItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		if (toolItem == null || firstItem == null || secondItem == null)
			return;

		var toolTemplate = itemTemplates.GetItemTemplate(toolItem.ItemId);
		var firstTemplate = itemTemplates.GetItemTemplate(firstItem.ItemId);
		var secondTemplate = itemTemplates.GetItemTemplate(secondItem.ItemId);
		if (toolTemplate == null || firstTemplate == null || secondTemplate == null)
			return;

		var validation = CompositionService.CanAct(toolTemplate, firstItem, firstTemplate, secondItem, secondTemplate);
		if (!validation.Succeeded)
			return;

		await CancelPendingItemUseAsync(player);
		await SendPacketAsync(
			new SmItemUsageAnimation(
				player.ObjectId,
				toolItem.ObjectId,
				toolItem.ItemId,
				CompositionService.UsageDelayMilliseconds,
				0,
				0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: toolItem.ObjectId,
			itemTemplateId: toolItem.ItemId,
			targetItemName: toolTemplate.GetClientName() ?? toolTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.None,
			delay: TimeSpan.FromMilliseconds(CompositionService.UsageDelayMilliseconds),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteCompositeStonesAsync(
					player,
					toolItem,
					firstItem.ItemId,
					firstTemplate.Level,
					secondItem.ItemId,
					secondTemplate.Level,
					staticData,
					cancellationToken);
			},
			cancelEndState: 2,
			cancelAnimationToSelfOnly: true);
	}

	private async Task CompleteCompositeStonesAsync(
		Player player,
		InventoryItem toolItem,
		int firstItemId,
		int firstItemLevel,
		int secondItemId,
		int secondItemLevel,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		// Java parity: CompositionAction delayed task consumes captured item ids without a second canAct pass.
		var inventoryItems = player.InventoryItems.ToList();
		var mutationPlan = CompositionService.CreateMutationPlan(
			player,
			inventoryItems,
			toolItem.ItemId,
			firstItemId,
			firstItemLevel,
			secondItemId,
			secondItemLevel,
			staticData.ItemTemplates,
			RandomInclusive,
			() => _idFactory?.NextId() ?? 0);

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveCompositeStoneActionMutationAsync(
				player,
				mutationPlan.UpdatedConsumedItems,
				mutationPlan.DeletedConsumedObjectIds,
				mutationPlan.UpdatedRewardItems,
				mutationPlan.AddedRewardItems,
				cancellationToken);
		if (!saved)
			return;

		await SendCompositionConsumedItemPacketsAsync(
			player,
			inventoryItems,
			mutationPlan.ConsumedItemMutations,
			staticData.ItemTemplates,
			staticData.ItemRestrictionCleanups);
		ApplyConsumedAndRewardInventoryMutation(inventoryItems, mutationPlan);
		player.InventoryItems = inventoryItems.ToArray();
		if (mutationPlan.RewardItemId != 0 && staticData.ItemTemplates.GetItemTemplate(mutationPlan.RewardItemId) is { } rewardTemplate)
		{
			RegisterCompositionExpirableAddedItems(player, mutationPlan.AddedRewardItems, rewardTemplate);
			if (HasRewardMutation(mutationPlan.UpdatedRewardItems, mutationPlan.AddedRewardItems))
				await SendCompositionRewardPacketsAsync(player, mutationPlan, rewardTemplate, staticData.ItemRestrictionCleanups);
		}

		if (!mutationPlan.RewardSucceeded && mutationPlan.RewardInventoryFull)
			await SendPacketAsync(SmSystemMessage.DiceInventoryError());
		await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, toolItem.ObjectId, toolItem.ItemId, 0, 1, 0));
	}

	private async Task HandleTuneAsync(Player player, CmTune packet)
	{
		// Java parity: network/aion/clientpackets/CM_TUNE.runImpl.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		var itemRandomBonuses = staticData?.ItemRandomBonuses;
		if (staticData == null || itemTemplates == null || itemRandomBonuses == null)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		var targetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.ItemObjectId);
		var tuningScrollItem = packet.TuningScrollObjectId == 0
			? null
			: inventoryItems.FirstOrDefault(item => item.ObjectId == packet.TuningScrollObjectId);
		var targetTemplate = targetItem == null ? null : itemTemplates.GetItemTemplate(targetItem.ItemId);
		var tuningScrollTemplate = tuningScrollItem == null ? null : itemTemplates.GetItemTemplate(tuningScrollItem.ItemId);
		var targetItemName = targetTemplate?.GetClientName() ?? targetTemplate?.Name ?? string.Empty;
		var tuningScrollName = tuningScrollTemplate?.GetClientName() ?? tuningScrollTemplate?.Name ?? string.Empty;
		var plan = CmTuneRuntimePlanService.CreatePlan(
			targetItem,
			targetTemplate,
			packet.TuningScrollObjectId,
			tuningScrollItem,
			tuningScrollTemplate,
			tuningScrollName,
			targetItemName);

		switch (plan.Status)
		{
			case CmTuneRuntimePlanStatus.NoTargetItem:
			case CmTuneRuntimePlanStatus.MissingTuningScroll:
			case CmTuneRuntimePlanStatus.MissingTuningAction:
				return;
			case CmTuneRuntimePlanStatus.IdentifyTargetItem:
				if (targetItem == null || targetTemplate == null)
					return;

				await ScheduleIdentifyItemAsync(player, inventoryItems, targetItem, targetTemplate, itemRandomBonuses, staticData.ItemRestrictionCleanups);
				return;
			case CmTuneRuntimePlanStatus.AuditAlreadyIdentifiedWithoutScroll:
				if (plan.AuditMessage != null)
				{
					_logger.LogWarning(
						"Player {PlayerName} ({PlayerObjectId}) {AuditMessage}",
						player.Name,
						player.ObjectId,
						plan.AuditMessage);
				}
				return;
			case CmTuneRuntimePlanStatus.GuardBlocked:
				if (plan.GuardPlan?.DenialMessage != null)
					await SendPacketAsync(plan.GuardPlan.DenialMessage);
				return;
			case CmTuneRuntimePlanStatus.ExecuteTuning:
				if (plan.ResolvedAction == null)
					return;

				await ScheduleTuningActionAsync(
					player,
					inventoryItems,
					plan.ResolvedAction,
					itemRandomBonuses,
					staticData.ItemRestrictionCleanups);
				return;
			default:
				return;
		}
	}

	private async Task ScheduleIdentifyItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem targetItem,
		ItemTemplateSummary targetTemplate,
		ItemRandomBonusTable itemRandomBonuses,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		var targetItemName = targetTemplate.GetClientName() ?? targetTemplate.Name;
		var startPlan = IdentifyItemExecutionPlanService.CreateStartPlan(player.ObjectId, targetItem.ObjectId, targetItem.ItemId);
		await CancelPendingItemUseAsync(player);
		await BroadcastItemUsageAnimationAsync(player, startPlan.BroadcastPacket);
		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: targetItem.ObjectId,
			itemTemplateId: targetItem.ItemId,
			targetItemName: targetItemName,
			cancelMessage: PendingItemUseCancelMessage.ItemIdentify,
			delay: TimeSpan.FromMilliseconds(startPlan.DelayMilliseconds),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteIdentifyItemAsync(
					player,
					inventoryItems,
					targetItem.ObjectId,
					targetTemplate,
					itemRandomBonuses,
					itemRestrictionCleanups);
			},
			cancelEndState: 11);
	}

	private async Task CompleteIdentifyItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		int targetItemObjectId,
		ItemTemplateSummary targetTemplate,
		ItemRandomBonusTable itemRandomBonuses,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		var targetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == targetItemObjectId);
		if (targetItem == null)
			return;

		var targetItemName = targetTemplate.GetClientName() ?? targetTemplate.Name;
		var completionPlan = IdentifyItemExecutionPlanService.CreateCompletionPlan(
			targetItem,
			targetTemplate,
			player.ObjectId,
			itemRandomBonuses,
			targetItemName);
		ReplaceInventoryItem(inventoryItems, completionPlan.TargetItemUpdate);
		player.InventoryItems = inventoryItems.ToArray();
		await BroadcastItemUsageAnimationAsync(player, completionPlan.BroadcastPacket);
		await SendPacketAsync(new SmInventoryUpdateItem(
			completionPlan.TargetItemUpdate,
			targetTemplate,
			SmInventoryUpdateItem.DecreaseItemUse,
			GetGeneralInfoWarehouseRestrictionFlag(completionPlan.TargetItemUpdate.ItemId, itemRestrictionCleanups)));
		await SendPacketAsync(completionPlan.SuccessMessage);
	}

	private async Task ScheduleTuningActionAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		CmTuneResolvedTuningAction action,
		ItemRandomBonusTable itemRandomBonuses,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		var startPlan = TuningActionExecutionPlanService.CreateStartPlan(player.ObjectId, action.TuningScrollItem.ObjectId, action.TuningScrollItem.ItemId);
		var removeCooldownDelayIdOnCancel = AddItemCooldownIfNeeded(player, action.TuningScrollTemplate, removeOnCancel: true);
		await CancelPendingItemUseAsync(player);
		await BroadcastItemUsageAnimationAsync(player, startPlan.BroadcastPacket);
		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: action.TuningScrollItem.ObjectId,
			itemTemplateId: action.TuningScrollItem.ItemId,
			targetItemName: action.TargetTemplate.GetClientName() ?? action.TargetTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.ItemReidentify,
			delay: TimeSpan.FromMilliseconds(startPlan.DelayMilliseconds),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteTuningActionAsync(
					player,
					inventoryItems,
					action,
					itemRandomBonuses,
					itemRestrictionCleanups,
					cancellationToken);
			},
			cancelEndState: 14,
			removeCooldownDelayIdOnCancel: removeCooldownDelayIdOnCancel);
	}

	private async Task CompleteTuningActionAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		CmTuneResolvedTuningAction action,
		ItemRandomBonusTable itemRandomBonuses,
		ItemRestrictionCleanupTable? itemRestrictionCleanups,
		CancellationToken cancellationToken)
	{
		var tuningScrollItem = inventoryItems.FirstOrDefault(item => item.ObjectId == action.TuningScrollItem.ObjectId);
		if (tuningScrollItem == null)
			return;

		var targetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == action.TargetItem.ObjectId);
		if (targetItem == null)
			return;

		var completionPlan = TuningActionExecutionPlanService.CreateCompletionPlan(
			targetItem,
			action.TargetTemplate,
			action.TargetTemplate.OptionSlotBonus,
			action.TargetTemplate.MaxEnchantBonus,
			player.ObjectId,
			tuningScrollItem.ObjectId,
			tuningScrollItem.ItemId,
			action.ShouldNotReduceTuneCount,
			scrollConsumptionSucceeded: true,
			itemRandomBonuses,
			action.TargetTemplate.GetClientName() ?? action.TargetTemplate.Name);
		await BroadcastItemUsageAnimationAsync(player, completionPlan.BroadcastPacket);
		var sourceItemUpdate = tuningScrollItem.Count > 1 ? CopyInventoryItem(tuningScrollItem, count: tuningScrollItem.Count - 1) : null;
		int? deletedSourceObjectId = tuningScrollItem.Count <= 1 ? tuningScrollItem.ObjectId : null;
		await ApplySourceItemMutationAsync(
			player,
			inventoryItems,
			action.TuningScrollTemplate,
			sourceItemUpdate,
			deletedSourceObjectId,
			itemRestrictionCleanups);
		if (completionPlan.Status != TuningActionCompletionPlanStatus.Planned || completionPlan.TargetItemUpdate == null)
			return;

		ReplaceInventoryItem(inventoryItems, completionPlan.TargetItemUpdate);
		player.InventoryItems = inventoryItems.ToArray();
		if (completionPlan.ResultPacket != null)
			await SendPacketAsync(completionPlan.ResultPacket);
		if (completionPlan.SuccessMessage != null)
			await SendPacketAsync(completionPlan.SuccessMessage);
	}

	private async Task HandleTuneResultAsync(Player player, CmTuneResult packet)
	{
		// Java parity: network/aion/clientpackets/CM_TUNE_RESULT.runImpl.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		var targetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.ItemObjectId);
		var targetTemplate = targetItem == null ? null : itemTemplates.GetItemTemplate(targetItem.ItemId);
		var targetItemName = targetTemplate?.GetClientName() ?? targetTemplate?.Name ?? string.Empty;
		var plan = CmTuneResultPlanService.CreatePlan(targetItem, targetTemplate, packet.HasAccepted, targetItemName);
		if (plan.Status == CmTuneResultPlanStatus.NoTargetItem || plan.ResultingTargetItem == null || targetTemplate == null)
			return;

		if (plan.AuditMessage != null)
		{
			_logger.LogWarning(
				"Player {PlayerName} ({PlayerObjectId}) {AuditMessage}",
				player.Name,
				player.ObjectId,
				plan.AuditMessage);
		}

		ReplaceInventoryItem(inventoryItems, plan.ResultingTargetItem);
		player.InventoryItems = inventoryItems.ToArray();
		if (plan.ResponseMessage != null)
			await SendPacketAsync(plan.ResponseMessage);
		await SendPacketAsync(new SmInventoryUpdateItem(
			plan.ResultingTargetItem,
			targetTemplate,
			SmInventoryUpdateItem.DecreaseItemUse,
			GetGeneralInfoWarehouseRestrictionFlag(plan.ResultingTargetItem.ItemId, staticData.ItemRestrictionCleanups)));
	}

	private Task HandleCraftAsync(Player? player, CmCraft packet)
	{
		// Java parity: network/aion/clientpackets/CM_CRAFT.runImpl guard shell.
		var hasPlayer = player != null;
		var isPlayerSpawned = player?.IsOnline == true;
		var isShuttingDownSoon = _isShuttingDownSoon();
		var targetExists = true;
		var targetIsInRange = true;
		var targetTemplateMatches = true;
		IWorldNpcObject? target = null;

		if (hasPlayer && isPlayerSpawned && !isShuttingDownSoon && packet.UnknownByte != CmCraftRuntimePlanService.MorphSubstancesMarker)
		{
			target = ResolveCraftTarget(packet.TargetObjectId);
			targetExists = target != null;
			targetIsInRange = target != null && IsInCraftTargetRange(player!, target);
			targetTemplateMatches = target != null && target.TemplateId == packet.TargetTemplateId;
		}

		var plan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer,
			isPlayerSpawned,
			isShuttingDownSoon,
			packet.UnknownByte,
			packet.RecipeId,
			packet.TargetObjectId,
			packet.CraftType,
			packet.MaterialsData,
			targetExists,
			targetIsInRange,
			targetTemplateMatches);
		_cmCraftRuntimePlanObserver?.Invoke(plan);
		ObserveCraftStartCompositionPlan(player, packet, plan, target, targetIsInRange);

		if (plan.Status == CmCraftRuntimePlanStatus.StartCrafting)
		{
			_logger.LogDebug(
				"Deferred CM_CRAFT startCrafting for player {PlayerObjectId}, recipe {RecipeId}, target {TargetObjectId}; CraftService.startCrafting runtime is not ported yet",
				player?.ObjectId,
				packet.RecipeId,
				packet.TargetObjectId);
		}

		return Task.CompletedTask;
	}

	private void ObserveCraftStartCompositionPlan(
		Player? player,
		CmCraft packet,
		CmCraftRuntimePlan runtimePlan,
		IWorldNpcObject? target,
		bool targetIsWithinToolRange)
	{
		if (_cmCraftStartCompositionPlanObserver == null)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var recipeTemplate = staticData?.RecipeTemplates.GetRecipeTemplateById(packet.RecipeId);
		var productTemplate = recipeTemplate == null ? null : staticData?.ItemTemplates.GetItemTemplate(recipeTemplate.ProductId);
		var craftService = new CraftService(
			resourceStats: null!,
			staticData?.ItemTemplates,
			staticData?.SkillTemplates);
		var compositionPlan = CmCraftStartCompositionPlanService.CreatePlan(
			runtimePlan,
			craftService,
			player,
			recipeTemplate,
			productTemplate,
			target,
			targetIsStaticObject: target?.Template.Type == "STATIC",
			targetIsWithinToolRange,
			hasCraftingTaskInProgress: false);
		_cmCraftStartCompositionPlanObserver.Invoke(compositionPlan);
	}

	private void HandleBuyItem(Player? player, CmBuyItem packet)
	{
		if (_cmBuyItemHandlerCompositionPlanObserver == null && _cmBuyItemSideEffectOutcomePlanObserver == null)
			return;

		var targetKind = ResolveBuyItemTargetKind(player, packet.SellerObjectId);
		var npcTradeFunctionFacts = ResolveBuyItemNpcTradeFunctionFacts(player, packet, targetKind);
		var sellActionFacts = ResolveBuyItemSellActionFacts(player, packet, targetKind, npcTradeFunctionFacts);
		var sellToShopPlan = ResolveBuyItemSellToShopPlan(player, packet, targetKind, sellActionFacts);
		var sellForApToShopPlan = ResolveBuyItemSellForApToShopPlan(player, packet, sellActionFacts);
		var buyFromShopTradeTemplate = ResolveBuyItemBuyFromShopTradeTemplate(player, packet, targetKind);
		var buyTransactionPlan = ResolveBuyItemBuyTransactionPlan(player, packet, targetKind, buyFromShopTradeTemplate);
		var privateStoreItems = ResolveBuyItemPrivateStoreItems(player, packet, targetKind);
		var privateStorePurchasePlan = ResolveBuyItemPrivateStorePurchasePlan(player, packet, targetKind, privateStoreItems);
		var repurchasableItemObjectIds = ResolveBuyItemRepurchasableItemObjectIds(player, packet, targetKind);
		var repurchasePlan = ResolveBuyItemRepurchasePlan(player, packet, targetKind, repurchasableItemObjectIds);
		var repurchaseStateSnapshots = ResolveBuyItemRepurchaseStateSnapshots(player, packet, targetKind);
		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: player != null,
				TargetKind: targetKind,
				NpcCanBuy: npcTradeFunctionFacts?.NpcCanBuy ?? true,
				NpcCanPurchase: npcTradeFunctionFacts?.NpcCanPurchase ?? false,
				NpcCanSell: npcTradeFunctionFacts?.NpcCanSell ?? true,
				PurchaseTemplate: sellActionFacts?.PurchaseTemplate,
				SellTemplate: buyFromShopTradeTemplate,
				BuyTransactionPlan: buyTransactionPlan,
				SellToShopPlan: sellToShopPlan,
				SellForApToShopPlan: sellForApToShopPlan,
				RepurchasableItemObjectIds: repurchasableItemObjectIds,
				RepurchasePlan: repurchasePlan,
				PrivateStoreItems: privateStoreItems,
				PrivateStorePurchasePlan: privateStorePurchasePlan));
		_cmBuyItemHandlerCompositionPlanObserver?.Invoke(plan);
		_cmBuyItemSideEffectOutcomePlanObserver?.Invoke(CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(
			plan,
			player?.ObjectId,
			repurchaseStateSnapshots));
	}

	private async Task HandleFindGroupAsync(CmFindGroup findGroup)
	{
		// Java parity: CM_FIND_GROUP action 0/4 show-list and action 2/6 mutation-post
		// branches use direct sends to the active player; action 1/5 removal branches
		// use PacketSendUtility.broadcastToWorld(packet, p -> p.getRace() == race);
		// action 3/7 update branches mutate state without packet side effects;
		// action 8/9/10/13/15/17 instance-group branches use direct sends when
		// the Java branch composes a packet; action 11 sends directly to a
		// non-active online recruiter resolved by playerOrTeamId; action 12
		// sends decline whispers or dispatches group/alliance invite requests.
		if (_activePlayer == null || findGroup.Action is not (0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 15 or 17))
			return;

		var plan = CreateDisabledFindGroupBoundaryPlan(findGroup, (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		if (plan?.Status != FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects)
		{
			return;
		}

		if (plan.InvitePlan != null && findGroup.Action != 12)
			return;

		if (plan.IntentPlan.WorldBroadcastIntents.Count != 0 && _connectionRegistry == null)
			return;

		foreach (var intent in plan.IntentPlan.DirectPacketIntents)
		{
			if (intent.RecipientObjectId == _activePlayer.ObjectId)
				continue;

			if (findGroup.Action is not (11 or 12) || _connectionRegistry == null)
				return;
		}

		foreach (var intent in plan.IntentPlan.DirectPacketIntents)
		{
			if (intent.RecipientObjectId == _activePlayer.ObjectId)
				await SendPacketAsync(intent.Packet);
			else
				await _connectionRegistry!.SendPacketToPlayerAsync(intent.RecipientObjectId, intent.Packet);
		}

		if (findGroup.Action == 12 && plan.InvitePlan != null)
			await SendFindGroupInstanceInvitePlanAsync(plan.InvitePlan);

		foreach (var intent in plan.IntentPlan.WorldBroadcastIntents)
		{
			if (intent is null)
				continue;

			await _connectionRegistry!.BroadcastToWorldAsync(
				intent.Packet,
				player => string.Equals(player.Race, intent.Race, StringComparison.Ordinal));
		}
	}

	private async Task SendFindGroupInstanceInvitePlanAsync(
		FindGroupInstanceApplicationInviteDispatchPlan invitePlan,
		CancellationToken cancellationToken = default)
	{
		// Java parity: FindGroupService.sendInstanceApplicationResult invokes
		// PlayerGroupService.inviteToGroup or PlayerAllianceService.inviteToAlliance.
		if (invitePlan.GroupInviteRequest != null)
		{
			await SendGroupInvitePacketAsync(
				invitePlan.GroupInviteRequest.Request.InviterObjectId,
				invitePlan.GroupInviteRequest.InviterMessage,
				cancellationToken);
			if (invitePlan.GroupInviteRequest.QuestionWindow != null && invitePlan.InviteIntent != null)
				await SendGroupInvitePacketAsync(
					invitePlan.InviteIntent.InvitedObjectId,
					invitePlan.GroupInviteRequest.QuestionWindow,
					cancellationToken);
		}

		if (invitePlan.AllianceInviteRequest != null)
		{
			if (invitePlan.AllianceInviteRequest.RejectionMessage != null && invitePlan.InviteIntent != null)
				await SendGroupInvitePacketAsync(
					invitePlan.InviteIntent.InviterObjectId,
					invitePlan.AllianceInviteRequest.RejectionMessage,
					cancellationToken);

			foreach (var message in invitePlan.AllianceInviteRequest.RequesterMessages)
			{
				if (invitePlan.InviteIntent != null)
					await SendGroupInvitePacketAsync(invitePlan.InviteIntent.InviterObjectId, message, cancellationToken);
			}

			if (invitePlan.AllianceInviteRequest.QuestionWindow != null && invitePlan.AllianceInviteRequest.Request != null)
				await SendGroupInvitePacketAsync(
					invitePlan.AllianceInviteRequest.Request.RequestTargetObjectId,
					invitePlan.AllianceInviteRequest.QuestionWindow,
					cancellationToken);
		}
	}

	private bool IsBeshmundirsWalkTarget(int targetObjectId)
	{
		// Java parity: data/handlers/ai/instance/beshmundirTemple/BeshmundirsWalkAI.onDialogSelect.
		return _world != null
			&& _world.TryGetObject(targetObjectId, out var gameObject)
			&& gameObject is IWorldNpcObject npc
			&& string.Equals(npc.AiName, "beshmundirswalk", StringComparison.Ordinal);
	}

	private bool IsBeshmundirsWalkGroupMemberInInstance(Player player)
	{
		// Java parity: BeshmundirsWalkAI.isAGroupMemberInInstance checks group members for world 300170000.
		return _playerGroupRuntime.GetMemberObjectIds(player.CurrentTeamId)
			.Select(memberObjectId => _playerGroupRuntime.GetMember(player.CurrentTeamId, memberObjectId)?.Player)
			.Any(member => member?.Position.WorldId == 300170000);
	}

	private static bool IsBeshmundirsWalkDifficultySelection(int dialogActionId)
	{
		return dialogActionId is CmDialogSelect.SelectNone1 or CmDialogSelect.SelectNone2;
	}

	private async Task HandleBeshmundirsWalkDifficultySelectionAsync(Player player, int targetObjectId, int dialogActionId)
	{
		// Java parity: BeshmundirsWalkAI SELECT_NONE_1/2 registers
		// SM_QUESTION_WINDOW.STR_INSTANCE_DUNGEON_WITH_DIFFICULTY_ENTER_CONFIRM, then reopens dialog 4762.
		var pathL10nId = dialogActionId == CmDialogSelect.SelectNone1 ? 902051 : 902052;
		if (player.ResponseRequester.PutRequest(
			SmQuestionWindow.InstanceDungeonWithDifficultyEnterConfirm,
			new QuestionResponseRequest(
				targetObjectId,
				QuestionResponseRequestKind.BeshmundirDifficultyEnter,
				new PendingBeshmundirDifficultyEnterRequest(
					targetObjectId,
					dialogActionId,
					pathL10nId,
					DifficultyId: 2))))
		{
			await SendPacketAsync(new SmQuestionWindow(
				SmQuestionWindow.InstanceDungeonWithDifficultyEnterConfirm,
				targetObjectId,
				rangeOrCooldownSeconds: 5,
				"300170000",
				ChatUtil.L10n(pathL10nId)!));
		}

		await SendPacketAsync(new SmDialogWindow(targetObjectId, 4762));
	}

	private async Task HandleBeshmundirsWalkMoveToInstanceAsync(Player player, int targetObjectId, byte difficultyId = 0)
	{
		// Java parity: BeshmundirsWalkAI.moveToInstance resolves DataManager.PORTAL2_DATA.getPortalUsePath(getNpcId(), player)
		// and then calls PortalService.port(portalPath, player, getOwner(), difficult).
		if (_playerEnterWorldService == null
			|| _runtimeContext?.DataManager?.StaticData is not { } staticData
			|| _world == null
			|| !_world.TryGetObject(targetObjectId, out var gameObject)
			|| gameObject is not IWorldNpcObject npc)
		{
			return;
		}

		var portalPath = staticData.PortalPaths.GetPortalUsePath(npc.TemplateId, player.Race);
		if (portalPath == null)
			return;

		var now = DateTimeOffset.Now;
		var preparation = await _playerEnterWorldService.PreparePortalEntryAsync(
			player,
			portalPath,
			staticData.PortalLocs,
			staticData.InstanceCooltimes,
			_runtimeContext.WorldMapStates,
			staticData.ItemTemplates,
			now,
			npc.ObjectId,
			npcIsDialogNpc: npc.Template.IsDialogNpc);
		if (preparation.Status == PortalEntryPreparationStatus.ValidationRejected)
		{
			if (preparation.EntryPlan.FailurePacket != null)
				await SendPacketAsync(preparation.EntryPlan.FailurePacket);
			return;
		}

		if (preparation.Status is not PortalEntryPreparationStatus.Ready
			and not PortalEntryPreparationStatus.UnsupportedTeamPortal)
		{
			return;
		}

		foreach (var packet in preparation.Packets)
			await SendPacketAsync(packet);

		await QueuePortalContinueTransferAsync(
			player,
			ApplyBeshmundirDifficulty(preparation, difficultyId),
			staticData,
			_runtimeContext.WorldMapStates,
			staticData.InstanceCooltimes,
			now);
	}

	private static PortalEntryPreparationResult ApplyBeshmundirDifficulty(
		PortalEntryPreparationResult preparation,
		byte difficultyId)
	{
		if (difficultyId == 0)
			return preparation;

		return preparation with
		{
			EntryPlan = preparation.EntryPlan with
			{
				DifficultyId = difficultyId,
				TeamPlan = preparation.EntryPlan.TeamPlan == null
					? null
					: preparation.EntryPlan.TeamPlan with { DifficultyId = difficultyId },
			},
		};
	}

	private async Task HandleGroupDataExchangeAsync(CmGroupDataExchange packet)
	{
		if (_groupDataExchangeHandlerCompositionPlanObserver == null)
			return;

		var plan = await GroupDataExchangeHandlerCompositionPlanService.CreateDisabledPlanAsync(
			_activePlayer,
			packet.Action,
			packet.GroupType,
			packet.Unknown2,
			packet.Data,
			_playerGroupRuntime,
			_playerAllianceRuntime,
			_playerLeagueRuntime,
			_connectionRegistry);
		_groupDataExchangeHandlerCompositionPlanObserver.Invoke(plan);
	}

	private static PrivateStoreCreatePlan CreatePrivateStoreCreatePlan(CmPrivateStore packet, Player player)
	{
		// Java parity: CM_PRIVATE_STORE.runImpl -> closePrivateStore/createStoreWithItems.
		// Handler diagnostics hydrate the disabled planner from current player snapshots only.
		var context = new PrivateStoreCreatePlayerContext(
			player.ObjectId,
			(int)player.CreatureState,
			IsPrivateStoreOpen(player),
			player.IsFlying(),
			player.Movement.Mask != MovementMask.Immediate,
			player.IsInState(PlayerCreatureState.WeaponEquipped),
			player.IsTrading,
			player.IsInRideMode,
			player.VisualState != PlayerVisualStates.Visible && player.VisualState != PlayerVisualStates.Blinking,
			IsDead(player),
			player.IsInState(PlayerCreatureState.Chair));

		var itemContexts = player.InventoryItems
			.GroupBy(item => item.ObjectId)
			.ToDictionary(
				group => group.Key,
				group =>
				{
					var item = group.First();
					return new PrivateStoreCreateItemContext(
						item.ObjectId,
						item.ItemId,
						item.Count,
						ItemExistsAndIdMatches: true,
						ItemIsPackCountAboveZeroOrTradeable: item.PackCount > 0,
						item.IsEquipped);
				});

		return PrivateStoreCreatePlanService.CreateDisabledPlan(packet, context, itemContexts);
	}

	private static PrivateStoreNameOpenCompositionPlan CreatePrivateStoreNameOpenPlan(CmPrivateStoreName packet, Player player)
	{
		// Java parity: CM_PRIVATE_STORE_NAME.runImpl -> PrivateStoreService.openPrivateStore(activePlayer, name).
		// Live store-message mutation and SM_PRIVATE_STORE_NAME fanout remain deferred.
		var context = new PrivateStoreNameOpenCompositionContext(player.ObjectId, IsPrivateStoreOpen(player));
		return PrivateStoreNameOpenCompositionPlanService.CreateDisabledPlan(packet, context);
	}

	private static bool IsPrivateStoreOpen(Player player)
	{
		return player.IsInState(PlayerCreatureState.PrivateShop) || player.PrivateStoreItems.Count > 0;
	}

	private static bool IsDead(Player player)
	{
		return player.LifeStats?.CurrentHp <= 0 || player.CreatureState == PlayerCreatureState.Dead;
	}

	private static IReadOnlySet<int> ResolveBuyItemRepurchasableItemObjectIds(
		Player? player,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind)
	{
		if (player == null
			|| targetKind != CmBuyItemRunTargetKind.Npc
			|| packet.TradeActionId != CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId)
			return new HashSet<int>();

		// Java parity: RepurchaseList.addRepurchaseItem filters each requested
		// object id through RepurchaseService.canRepurchase(player, itemObjectId).
		return player.RepurchaseItems.Select(item => item.Item.ObjectId).ToHashSet();
	}

	private static IReadOnlyList<RepurchaseStateSnapshot>? ResolveBuyItemRepurchaseStateSnapshots(
		Player? player,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind)
	{
		if (player == null
			|| targetKind != CmBuyItemRunTargetKind.Npc
			|| packet.TradeActionId != CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId)
			return null;

		// Java parity: RepurchaseService.repurchaseFromShop mutates the current
		// set returned from the service map; this records the supplied facts only.
		return
		[
			new RepurchaseStateSnapshot(
				player.ObjectId,
				player.RepurchaseItems,
				"CM_BUY_ITEM action 2 supplied Player.RepurchaseItems snapshot for disabled RepurchaseService.repurchaseFromShop outcome"),
		];
	}

	private RepurchasePlan? ResolveBuyItemRepurchasePlan(
		Player? player,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind,
		IReadOnlySet<int> repurchasableItemObjectIds)
	{
		if (player == null
			|| targetKind != CmBuyItemRunTargetKind.Npc
			|| packet.TradeActionId != CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId
			|| repurchasableItemObjectIds.Count == 0)
			return null;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = _buyItemItemTemplates ?? staticData?.ItemTemplates;
		if (itemTemplates == null)
			return null;

		var readItems = packet.Items
			.Select(item => new CmBuyItemReadItem(item.ItemObjectId, item.Count))
			.ToList();
		if (packet.AuditItem is { } auditItem)
			readItems.Add(new CmBuyItemReadItem(auditItem.ItemObjectId, auditItem.Count));

		var readPlan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			packet.SellerObjectId,
			packet.TradeActionId,
			packet.Amount,
			readItems,
			repurchasableItemObjectIds);
		if (readPlan.Status != CmBuyItemRepurchaseReadPlanStatus.PlanCreated)
			return null;

		// Java parity: RepurchaseService.repurchaseFromShop runs after CM_BUY_ITEM
		// target and npc.canBuy gates. This remains a disabled diagnostic payload.
		return RepurchasePlanService.CreatePlan(
			CanTrade(player),
			player,
			player.InventoryItems,
			readPlan.RepurchaseItemObjectIds,
			player.RepurchaseItems,
			itemTemplates,
			_buyItemDiagnosticObjectIdProvider ?? (() => 0));
	}

	private IReadOnlyList<PrivateStoreListedItemSummary>? ResolveBuyItemPrivateStoreItems(
		Player? player,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind)
	{
		if (player == null
			|| targetKind != CmBuyItemRunTargetKind.Player
			|| packet.TradeActionId != 0
			|| _world == null
			|| !_world.TryGetObject(packet.SellerObjectId, out var gameObject)
			|| gameObject is not Player seller)
			return null;

		// Java parity: PrivateStoreService.getBoughtItems snapshots
		// seller.getStore().getSoldItems().values() into an array, relying on
		// LinkedHashMap insertion order for packet index lookup.
		return seller.PrivateStoreItems;
	}

	private PrivateStorePurchasePlan? ResolveBuyItemPrivateStorePurchasePlan(
		Player? buyer,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind,
		IReadOnlyList<PrivateStoreListedItemSummary>? privateStoreItems)
	{
		if (buyer == null
			|| targetKind != CmBuyItemRunTargetKind.Player
			|| packet.TradeActionId != 0
			|| privateStoreItems == null
			|| _world == null
			|| !_world.TryGetObject(packet.SellerObjectId, out var gameObject)
			|| gameObject is not Player seller)
			return null;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = _buyItemItemTemplates ?? staticData?.ItemTemplates;
		if (itemTemplates == null)
			return null;

		var boughtItemsPlan = PrivateStoreBoughtItemsPlanService.CreatePlan(packet.Items, privateStoreItems);
		if (boughtItemsPlan.Status != PrivateStoreBoughtItemsPlanStatus.PlanCreated)
			return null;

		return PrivateStorePurchasePlanService.CreatePlan(
			seller.IsOnline,
			buyer.IsOnline,
			string.Equals(seller.Race, buyer.Race, StringComparison.OrdinalIgnoreCase),
			buyer,
			seller,
			buyer.InventoryItems,
			seller.InventoryItems,
			boughtItemsPlan.BoughtItems,
			CreateRemainingPrivateStoreItemObjectIds(privateStoreItems, boughtItemsPlan.BoughtItems),
			itemTemplates,
			_buyItemDiagnosticObjectIdProvider ?? (() => 0));
	}

	private static IReadOnlyList<int> CreateRemainingPrivateStoreItemObjectIds(
		IReadOnlyList<PrivateStoreListedItemSummary> storeItems,
		IReadOnlyList<PrivateStorePurchaseItemRequest> boughtItems)
	{
		var boughtCountsByObjectId = new Dictionary<int, long>();
		foreach (var boughtItem in boughtItems)
			boughtCountsByObjectId[boughtItem.ItemObjectId] = boughtCountsByObjectId.GetValueOrDefault(boughtItem.ItemObjectId) + boughtItem.Count;

		return storeItems
			.Where(storeItem => storeItem.Count - boughtCountsByObjectId.GetValueOrDefault(storeItem.ItemObjectId) > 0)
			.Select(storeItem => storeItem.ItemObjectId)
			.ToArray();
	}

	private CmBuyItemSellActionFactAdapterPlan? ResolveBuyItemSellActionFacts(
		Player? player,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind,
		CmBuyItemNpcTradeFunctionFactAdapterPlan? npcTradeFunctionFacts)
	{
		if (player == null
			|| targetKind != CmBuyItemRunTargetKind.Npc
			|| packet.TradeActionId != CmBuyItemSellToShopCompositionPlanService.SellToShopTradeActionId
			|| npcTradeFunctionFacts == null)
			return null;

		var tradeLists = _buyItemTradeLists ?? _runtimeContext?.DataManager?.StaticData.TradeLists;
		if (tradeLists == null)
			return null;

		return CmBuyItemSellActionFactAdapterService.CreatePlan(
			new CmBuyItemSellActionFactAdapterInput(
				NpcId: npcTradeFunctionFacts.NpcId,
				NpcCanBuy: npcTradeFunctionFacts.NpcCanBuy,
				NpcCanPurchase: npcTradeFunctionFacts.NpcCanPurchase),
			tradeLists);
	}

	private CmBuyItemNpcTradeFunctionFactAdapterPlan? ResolveBuyItemNpcTradeFunctionFacts(
		Player? player,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind)
	{
		if (player == null
			|| targetKind != CmBuyItemRunTargetKind.Npc
			|| _world == null
			|| !_world.TryGetObject(packet.SellerObjectId, out var gameObject)
			|| gameObject is not IWorldNpcObject npc)
			return null;

		var tradeLists = _buyItemTradeLists ?? _runtimeContext?.DataManager?.StaticData.TradeLists;
		if (tradeLists == null)
			return null;

		// Java parity: Npc.canSell/canBuy/canPurchase combine TalkInfo
		// func_dialogs with TradeListData presence before CM_BUY_ITEM dispatch.
		return CmBuyItemNpcTradeFunctionFactAdapterService.CreatePlan(npc.Template, tradeLists);
	}

	private TradeSellToShopPlan? ResolveBuyItemSellToShopPlan(
		Player? player,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind,
		CmBuyItemSellActionFactAdapterPlan? sellActionFacts)
	{
		if (player == null
			|| targetKind != CmBuyItemRunTargetKind.Npc
			|| sellActionFacts?.DispatchesAbyssApSell == true
			|| packet.TradeActionId != CmBuyItemSellToShopCompositionPlanService.SellToShopTradeActionId)
			return null;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = _buyItemItemTemplates ?? staticData?.ItemTemplates;
		var goodsLists = _buyItemGoodsLists ?? staticData?.GoodsLists;
		if (itemTemplates == null)
			return null;

		// Java parity: TradeService.performSellToShop reads the item object id,
		// item sellable mask, and PlayerLimitService.updateSellLimit before it
		// mutates inventory. This diagnostic keeps those facts non-live.
		var sellModifier = PricesService.GetVendorSellModifier(_options.Prices);
		var sellLimitLookup = SellLimitLookupService.CreatePlan(player.Level);
		var remainingSellLimit = _buyItemCurrentSellLimit ?? sellLimitLookup.BaseLimit ?? 0;
		var tradeItems = new List<TradeSellToShopItemRequest>();
		foreach (var packetItem in packet.Items)
		{
			var inventoryItem = player.InventoryItems.FirstOrDefault(item => item.ObjectId == packetItem.ItemObjectId);
			var template = inventoryItem == null ? null : itemTemplates.GetItemTemplate(inventoryItem.ItemId);
			var isSellable = sellActionFacts?.PurchaseTemplate != null
				|| template == null
				|| IsItemTemplateSellable(template);
			long? sellLimitAdjustedCount = null;
			if (template != null)
			{
				var sellReward = sellActionFacts?.PurchaseTemplate == null
					? PricesService.GetSellReward(template.Price, sellModifier)
					: (long)(template.Price * sellActionFacts.PurchaseTemplate.BuyPriceRate / 100D);
				var sellLimitPlan = PlayerSellLimitPlanService.CreatePlan(
					_options.Custom.LimitsEnabled,
					_options.Custom.LimitsEnableDynamicCap,
					sellReward,
					packetItem.Count,
					remainingSellLimit);
				sellLimitAdjustedCount = sellLimitPlan.UseCount;
				remainingSellLimit = sellLimitPlan.RemainingLimitAfter;
			}

			tradeItems.Add(new TradeSellToShopItemRequest(
				packetItem.ItemObjectId,
				packetItem.Count,
				isSellable,
				sellLimitAdjustedCount));
		}

		return TradeSellToShopPlanService.CreatePlan(
			CanTrade(player),
			player,
			player.InventoryItems,
			tradeItems,
			itemTemplates,
			sellActionFacts?.PurchaseTemplate,
			goodsLists,
			sellModifier,
			_buyItemDiagnosticObjectIdProvider ?? (() => 0));
	}

	private TradeSellForApToShopPlan? ResolveBuyItemSellForApToShopPlan(
		Player? player,
		CmBuyItem packet,
		CmBuyItemSellActionFactAdapterPlan? sellActionFacts)
	{
		if (player == null
			|| sellActionFacts?.DispatchesAbyssApSell != true
			|| sellActionFacts.PurchaseTemplate == null)
			return null;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = _buyItemItemTemplates ?? staticData?.ItemTemplates;
		var goodsLists = _buyItemGoodsLists ?? staticData?.GoodsLists;
		if (itemTemplates == null || goodsLists == null)
			return null;

		// Java parity: TradeService.performSellForAPToShop consumes inventory by
		// object id and only awards AP when decreaseByObjectId succeeds. This
		// diagnostic plan models that success/failure without mutating inventory.
		return TradeSellForApToShopPlanService.CreatePlan(
			_options.Custom.SellingApItemsEnabled,
			CanTrade(player),
			player.InventoryItems,
			packet.Items.Select(item => new TradeSellForApToShopItemRequest(
				item.ItemObjectId,
				item.Count,
				InventoryDecreaseSucceeds: player.InventoryItems.FirstOrDefault(inventoryItem => inventoryItem.ObjectId == item.ItemObjectId)?.Count >= item.Count)).ToArray(),
			itemTemplates,
			sellActionFacts.PurchaseTemplate,
			goodsLists);
	}

	private TradeListTemplateSummary? ResolveBuyItemBuyFromShopTradeTemplate(
		Player? player,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind)
	{
		if (player == null
			|| targetKind != CmBuyItemRunTargetKind.Npc
			|| !IsBuyFromShopTradeAction(packet.TradeActionId)
			|| _world == null
			|| !_world.TryGetObject(packet.SellerObjectId, out var gameObject)
			|| gameObject is not IWorldNpcObject npc)
			return null;

		var tradeLists = _buyItemTradeLists ?? _runtimeContext?.DataManager?.StaticData.TradeLists;
		return tradeLists?.GetTradeListTemplate(npc.TemplateId);
	}

	private TradeBuyTransactionPlan? ResolveBuyItemBuyTransactionPlan(
		Player? player,
		CmBuyItem packet,
		CmBuyItemRunTargetKind targetKind,
		TradeListTemplateSummary? tradeTemplate)
	{
		if (player == null
			|| targetKind != CmBuyItemRunTargetKind.Npc
			|| tradeTemplate == null
			|| !IsBuyFromShopTradeAction(packet.TradeActionId))
			return null;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = _buyItemItemTemplates ?? staticData?.ItemTemplates;
		var goodsLists = _buyItemGoodsLists ?? staticData?.GoodsLists;
		if (itemTemplates == null || goodsLists == null)
			return null;

		// Java parity: TradeService.performBuyTransaction receives packet item
		// ids/counts through TradeList, then validates those ids against the NPC
		// trade goods lists before any live inventory/AP/Kinah mutation.
		var allowedGoodsItemIds = CreateBuyItemAllowedGoodsItemIds(tradeTemplate, goodsLists);
		var limitedItemFacts = NpcDialogLimitedItemFactAdapterService.CreatePlan(
			new NpcDialogLimitedItemFactAdapterInput(tradeTemplate.NpcId, player.ObjectId),
			new TradeListTable([tradeTemplate], Array.Empty<TradeListTemplateSummary>(), Array.Empty<TradeListTemplateSummary>()),
			goodsLists);
		var priceSnapshot = PricesService.CreateSnapshot(player.Race, _options.Prices, _buyItemPriceInfluenceRates);
		var tradeItems = packet.Items
			.Select(item =>
			{
				var template = itemTemplates.GetItemTemplate(item.ItemObjectId);
				return new TradeBuyTransactionItemRequest(
					item.ItemObjectId,
					item.Count,
					template == null ? 0 : PricesService.GetBuyPrice(template.Price, player.Race, _options.Prices, _buyItemPriceInfluenceRates),
					template?.RequiredAbyssPoints ?? 0,
					template?.AcquisitionType ?? string.Empty,
					template?.AcquisitionItemId ?? 0,
					template?.AcquisitionItemCount ?? 0,
					IsAllowedByNpcGoodsList: allowedGoodsItemIds.Contains(item.ItemObjectId),
					LimitedItemCanBuy: CanBuyLimitedItem(limitedItemFacts.LimitedItems, item.ItemObjectId, item.Count));
			})
			.ToArray();

		return TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: tradeItems,
				TradeTemplate: tradeTemplate,
				UseKinah: ShouldUseKinahForBuyTransaction(tradeTemplate.NpcType),
				PlayerCanTrade: CanTrade(player),
				AvailableKinah: GetInventoryItemCount(player.InventoryItems, InventoryItemFactory.KinahItemId),
				CurrentAbyssPoints: player.AbyssRank.Ap,
				FreeSlots: InventoryCapacity.GetFreeCubeSlots(player, itemTemplates),
				AvailableRequiredItems: CreateInventoryItemCountByItemId(player.InventoryItems),
				VendorBuyModifier: PricesService.GetVendorBuyModifier(_options.Prices),
				PriceSnapshot: priceSnapshot));
	}

	private static IReadOnlySet<int> CreateBuyItemAllowedGoodsItemIds(
		TradeListTemplateSummary tradeTemplate,
		GoodsListTable goodsLists)
	{
		var allowed = new HashSet<int>();
		foreach (var goodsListId in tradeTemplate.GoodsListIds)
		{
			var goodsList = goodsLists.GetGoodsListById(goodsListId);
			if (goodsList == null)
				continue;
			foreach (var item in goodsList.ItemSummaries)
				allowed.Add(item.Id);
		}
		return allowed;
	}

	private static bool CanBuyLimitedItem(
		IReadOnlyList<NpcDialogLimitedItemFact> limitedItems,
		int itemId,
		long requestedCount)
	{
		var limitedItem = limitedItems.FirstOrDefault(item => item.ItemId == itemId);
		if (limitedItem == null)
			return true;

		// Java parity: TradeService.canBuyLimitItem consults the live LimitedItem
		// created by LimitedItemTradeService.start before cost subtraction.
		if (limitedItem.SellLimit > 0 && limitedItem.SellLimit - requestedCount < 0)
			return false;
		if (limitedItem.BuyLimit > 0 && limitedItem.PlayerBuyCount + requestedCount > limitedItem.BuyLimit)
			return false;
		return true;
	}

	private static IReadOnlyDictionary<int, long> CreateInventoryItemCountByItemId(IReadOnlyList<InventoryItem> inventoryItems)
	{
		var counts = new Dictionary<int, long>();
		foreach (var item in inventoryItems.Where(item => item.Location == 0 && !item.IsEquipped))
			counts[item.ItemId] = counts.GetValueOrDefault(item.ItemId) + item.Count;
		return counts;
	}

	private static long GetInventoryItemCount(IReadOnlyList<InventoryItem> inventoryItems, int itemId)
	{
		return inventoryItems
			.Where(item => item.Location == 0 && !item.IsEquipped && item.ItemId == itemId)
			.Sum(item => item.Count);
	}

	private static bool ShouldUseKinahForBuyTransaction(string npcType)
	{
		return npcType is "NORMAL" or "ABYSS_KINAH";
	}

	private static bool IsBuyFromShopTradeAction(int tradeActionId)
	{
		return tradeActionId is 13 or 14 or 15 or 16;
	}

	private static bool IsItemTemplateSellable(ItemTemplateSummary template)
	{
		const int SellableMask = 1 << 2;
		return (template.Mask & SellableMask) == SellableMask;
	}

	private CmBuyItemRunTargetKind ResolveBuyItemTargetKind(Player? player, int sellerObjectId)
	{
		// Java resolves player.getKnownList().getObject(sellerObjId). When no known-list
		// fact resolver is available, the observer path keeps reporting the existing
		// world-object-only approximation as non-live diagnostic evidence.
		var worldObject = _world?.TryGetObject(sellerObjectId, out var gameObject) == true
			? gameObject
			: null;
		bool? isKnownByPlayer = player == null || _buyItemKnownObjectResolver == null
			? null
			: _buyItemKnownObjectResolver(player, sellerObjectId, worldObject);
		var factPlan = CmBuyItemKnownListTargetFactAdapterService.CreatePlan(
			player,
			sellerObjectId,
			worldObject,
			isKnownByPlayer);
		return factPlan.TargetKind;
	}

	private IWorldNpcObject? ResolveCraftTarget(int targetObjectId)
	{
		// Java resolves player.getKnownList().getObject(targetObjId). The current C# world object
		// model does not have StaticObject yet, so live dispatch uses available world-visible
		// object metadata only for the CM_CRAFT pre-start guard.
		return _world != null
			&& _world.TryGetObject(targetObjectId, out var gameObject)
			&& gameObject is IWorldNpcObject target
				? target
				: null;
	}

	private static bool IsInCraftTargetRange(Player player, IWorldNpcObject target)
	{
		if (player.Position.WorldId != target.Position.WorldId || player.Position.InstanceId != target.Position.InstanceId)
			return false;

		return PositionUtilService.IsInRange(
			player.Position.X,
			player.Position.Y,
			player.Position.Z,
			target.Position.X,
			target.Position.Y,
			target.Position.Z,
			10);
	}

	private async Task SendConsumedItemPacketsAsync(
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<InventoryItem> updatedConsumedItems,
		IReadOnlyList<int> deletedConsumedObjectIds,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
	{
		foreach (var updatedItem in updatedConsumedItems)
		{
			var template = itemTemplates.GetItemTemplate(updatedItem.ItemId);
			if (template != null)
				await SendPacketAsync(
					new SmInventoryUpdateItem(
						updatedItem,
						template,
						SmInventoryUpdateItem.DecreaseItemUse,
						GetGeneralInfoWarehouseRestrictionFlag(updatedItem.ItemId, itemRestrictionCleanups)));
		}

		foreach (var deletedObjectId in deletedConsumedObjectIds)
		{
			if (inventoryItems.Any(item => item.ObjectId == deletedObjectId))
				await SendPacketAsync(new SmDeleteItem(deletedObjectId, SmDeleteItem.UseDeleteType));
		}
	}

	private async Task SendCompositionConsumedItemPacketsAsync(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<CompositionConsumedItemMutation> consumedMutations,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: model/templates/item/actions/CompositionAction consumes tool, first stone, then second stone,
		// and Storage.decreaseByItemId sends each update/delete packet immediately.
		var projectedCubeCount = inventoryItems.Count;
		foreach (var mutation in consumedMutations)
		{
			if (mutation.UpdatedItem != null)
			{
				var template = itemTemplates.GetItemTemplate(mutation.UpdatedItem.ItemId);
				if (template != null)
					await SendPacketAsync(
						new SmInventoryUpdateItem(
							mutation.UpdatedItem,
							template,
							SmInventoryUpdateItem.DecreaseItemUse,
							GetGeneralInfoWarehouseRestrictionFlag(mutation.UpdatedItem.ItemId, itemRestrictionCleanups)));
			}
			else if (mutation.Deleted && inventoryItems.Any(item => item.ObjectId == mutation.ObjectId))
			{
				projectedCubeCount--;
				await SendPacketAsync(new SmDeleteItem(mutation.ObjectId, SmDeleteItem.UseDeleteType));
				await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
			}
		}
	}

	private static void ApplyConsumedAndRewardInventoryMutation(List<InventoryItem> inventoryItems, CompositionMutationPlan mutationPlan)
	{
		foreach (var updatedConsumedItem in mutationPlan.UpdatedConsumedItems)
			ReplaceInventoryItem(inventoryItems, updatedConsumedItem);
		foreach (var deletedConsumedObjectId in mutationPlan.DeletedConsumedObjectIds)
			inventoryItems.RemoveAll(item => item.ObjectId == deletedConsumedObjectId);
		foreach (var updatedReward in mutationPlan.UpdatedRewardItems)
			ReplaceInventoryItem(inventoryItems, updatedReward);
		inventoryItems.AddRange(mutationPlan.AddedRewardItems);
	}

	private void RegisterCompositionExpirableAddedItems(Player player, IReadOnlyList<InventoryItem> addedRewardItems, ItemTemplateSummary rewardTemplate)
	{
		// Java parity: services/item/ItemService.addNonStackableItem registers newly created expirable items.
		if (rewardTemplate.MaxStackCount > 1)
			return;

		foreach (var addedRewardItem in addedRewardItems)
			_expirableTaskService?.RegisterInventoryItem(player, addedRewardItem);
	}

	private async Task SendCompositionRewardPacketsAsync(
		Player player,
		CompositionMutationPlan mutationPlan,
		ItemTemplateSummary rewardTemplate,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		foreach (var updatedReward in mutationPlan.UpdatedRewardItems)
			await SendPacketAsync(
				new SmInventoryUpdateItem(
					updatedReward,
					rewardTemplate,
					SmInventoryUpdateItem.IncreaseItemCollect,
					GetGeneralInfoWarehouseRestrictionFlag(updatedReward.ItemId, itemRestrictionCleanups)));
		var projectedCubeCount = player.InventoryItems.Count - mutationPlan.AddedRewardItems.Count;
		foreach (var addedReward in mutationPlan.AddedRewardItems)
		{
			await SendPacketAsync(
				SmInventoryAddItem.CreateItemCollect(
					addedReward,
					rewardTemplate,
					GetGeneralInfoWarehouseRestrictionFlag(addedReward.ItemId, itemRestrictionCleanups)));
			projectedCubeCount++;
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
		}
	}

	private static int RandomInclusive(int min, int max)
	{
		return Random.Shared.Next(min, max + 1);
	}

	internal Task<ItemPurificationHandlerPlan?> HandleItemPurificationAsync(
		Player player,
		CmItemPurification packet,
		ItemPurificationTable? itemPurificationsOverride = null,
		ItemTemplateTable? itemTemplatesOverride = null,
		int targetObjectId = 0,
		int? rerolledRandomBonusId = null,
		ItemRandomBonusTable? itemRandomBonusesOverride = null,
		Func<double>? randomBonusRoll = null)
	{
		// Java parity: network/aion/clientpackets/CM_ITEM_PURIFICATION.runImpl uses the active player,
		// resolves the base item by upgradedItemObjectId, and ignores packet player/material object ids.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemPurifications = itemPurificationsOverride ?? staticData?.ItemPurifications;
		var itemTemplates = itemTemplatesOverride ?? staticData?.ItemTemplates;
		var itemRandomBonuses = itemRandomBonusesOverride ?? staticData?.ItemRandomBonuses;
		if (itemPurifications == null || itemTemplates == null)
			return Task.FromResult<ItemPurificationHandlerPlan?>(null);

		var baseItem = player.InventoryItems.FirstOrDefault(item => item.ObjectId == packet.BaseItemObjectId);
		var handlerPlan = CreateItemPurificationHandlerPlan(
			player,
			baseItem,
			itemPurifications,
			itemTemplates,
			packet.ResultItemId,
			targetObjectId,
			rerolledRandomBonusId,
			itemRandomBonuses,
			randomBonusRoll);
		if (targetObjectId != 0
			|| handlerPlan.Application.Status != ItemPurificationApplicationPlanStatus.NeedsTargetObjectIdAllocation
			|| handlerPlan.Application.RequiresRandomBonusSelection
			|| _idFactory == null)
		{
			return Task.FromResult<ItemPurificationHandlerPlan?>(handlerPlan);
		}

		// Java parity: ItemPurificationService.upgradeItem calls ItemFactory.newItem, whose constructor path
		// allocates the target item object id through IDFactory before storage mutation and packet fanout.
		var allocatedTargetObjectId = _idFactory.NextId();
		var allocatedPlan = CreateItemPurificationHandlerPlan(
			player,
			baseItem,
			itemPurifications,
			itemTemplates,
			packet.ResultItemId,
			allocatedTargetObjectId,
			rerolledRandomBonusId,
			itemRandomBonuses,
			randomBonusRoll);
		if (!allocatedPlan.Application.Succeeded)
			_idFactory.ReleaseId(allocatedTargetObjectId);

		return Task.FromResult<ItemPurificationHandlerPlan?>(allocatedPlan);
	}

	internal async Task<ItemPurificationLiveExecutionResult?> HandleItemPurificationLiveExecutionAsync(
		Player player,
		CmItemPurification packet,
		int npcExpands,
		int questExpands,
		int itemExpands,
		ItemPurificationTable? itemPurificationsOverride = null,
		ItemTemplateTable? itemTemplatesOverride = null,
		int targetObjectId = 0,
		int? rerolledRandomBonusId = null,
		ItemRandomBonusTable? itemRandomBonusesOverride = null,
		Func<double>? randomBonusRoll = null,
		IGameClientConnectionRegistry? connectionRegistryOverride = null,
		AbyssPointsAddOptions? abyssPointsOptions = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: this is an explicit opt-in bridge toward CM_ITEM_PURIFICATION.runImpl.
		// The normal handler path remains plan-only until persistence, quest hooks, and AP side
		// effects are wired deliberately.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = itemTemplatesOverride ?? staticData?.ItemTemplates;
		if (itemTemplates == null)
			return null;

		var handlerPlan = await HandleItemPurificationAsync(
			player,
			packet,
			itemPurificationsOverride,
			itemTemplates,
			targetObjectId,
			rerolledRandomBonusId,
			itemRandomBonusesOverride,
			randomBonusRoll);
		if (handlerPlan == null)
			return null;

		return await ItemPurificationLiveExecutionService.ExecuteAsync(
			player.ObjectId,
			player,
			handlerPlan,
			itemTemplates,
			npcExpands,
			questExpands,
			itemExpands,
			connectionRegistryOverride ?? _connectionRegistry,
			abyssPointsOptions,
			_options.Custom.TopRankingXformMinRank,
			staticData?.ItemRestrictionCleanups,
			questMutationNotifier: null,
			cancellationToken);
	}

	internal async Task<ItemPurificationPersistentLiveExecutionResult?> HandleItemPurificationPersistentLiveExecutionAsync(
		Player player,
		CmItemPurification packet,
		int npcExpands,
		int questExpands,
		int itemExpands,
		IPlayerEnterWorldRepository? repository,
		ItemPurificationTable? itemPurificationsOverride = null,
		ItemTemplateTable? itemTemplatesOverride = null,
		int targetObjectId = 0,
		int? rerolledRandomBonusId = null,
		ItemRandomBonusTable? itemRandomBonusesOverride = null,
		Func<double>? randomBonusRoll = null,
		IGameClientConnectionRegistry? connectionRegistryOverride = null,
		AbyssPointsAddOptions? abyssPointsOptions = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: explicit test/caller opt-in for the final CM_ITEM_PURIFICATION.runImpl
		// mutation+packet+persistence chain. Normal packet dispatch remains plan-only.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = itemTemplatesOverride ?? staticData?.ItemTemplates;
		if (itemTemplates == null)
			return null;

		var handlerPlan = await HandleItemPurificationAsync(
			player,
			packet,
			itemPurificationsOverride,
			itemTemplates,
			targetObjectId,
			rerolledRandomBonusId,
			itemRandomBonusesOverride,
			randomBonusRoll);
		if (handlerPlan == null)
			return null;

		return await ItemPurificationPersistentLiveExecutionService.ExecuteAsync(
			player.ObjectId,
			player,
			handlerPlan,
			itemTemplates,
			npcExpands,
			questExpands,
			itemExpands,
			connectionRegistryOverride ?? _connectionRegistry,
			repository,
			abyssPointsOptions,
			_options.Custom.TopRankingXformMinRank,
			staticData?.ItemRestrictionCleanups,
			cancellationToken);
	}

	private static ItemPurificationHandlerPlan CreateItemPurificationHandlerPlan(
		Player player,
		InventoryItem? baseItem,
		ItemPurificationTable itemPurifications,
		ItemTemplateTable itemTemplates,
		int resultItemId,
		int targetObjectId,
		int? rerolledRandomBonusId,
		ItemRandomBonusTable? itemRandomBonuses,
		Func<double>? randomBonusRoll)
	{
		var workflow = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			itemPurifications,
			itemTemplates,
			resultItemId,
			targetObjectId,
			rerolledRandomBonusId,
			itemRandomBonuses,
			randomBonusRoll);
		var application = ItemPurificationApplicationPlanService.CreateApplicationPlan(workflow);
		var sourceTemplate = baseItem == null ? null : itemTemplates.GetItemTemplate(baseItem.ItemId);
		var targetTemplate = application.TargetItem == null ? null : itemTemplates.GetItemTemplate(application.TargetItem.ItemId);
		var packetPlan = ItemPurificationPacketPlanService.CreatePacketPlan(
			application,
			sourceTemplate?.GetClientName() ?? sourceTemplate?.Name ?? string.Empty,
			targetTemplate?.GetClientName() ?? targetTemplate?.Name ?? string.Empty);
		return new ItemPurificationHandlerPlan(workflow, application, packetPlan);
	}

	internal async Task HandleUseItemAsync(Player player, CmUseItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_USE_ITEM.runImpl. Implemented item actions are routed by template action metadata.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		var itemRandomBonuses = staticData?.ItemRandomBonuses;
		if (staticData == null || itemTemplates == null || itemRandomBonuses == null)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		var sourceItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.SourceItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		if (sourceItem == null)
			return;

		var sourceTemplate = itemTemplates.GetItemTemplate(sourceItem.ItemId);
		if (sourceTemplate == null)
			return;

		if (sourceTemplate.RideNpcId > 0)
		{
			await HandleRideUseItemAsync(player, sourceItem, sourceTemplate, staticData);
			return;
		}

		if (sourceTemplate.ToyPetSpawnNpcId > 0)
		{
			await HandleToyPetSpawnUseItemAsync(player, sourceItem, sourceTemplate, staticData);
			return;
		}

		if (sourceTemplate.CraftLearnRecipeId > 0)
		{
			await HandleCraftLearnUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate, staticData);
			return;
		}

		if (sourceTemplate.SkillLearnAction != null)
		{
			await HandleSkillLearnUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate, staticData);
			return;
		}

		if (sourceTemplate.QuestStartQuestId > 0)
		{
			await HandleQuestStartUseItemAsync(player, sourceItem, sourceTemplate, staticData);
			return;
		}

		if (sourceTemplate.HasTitleAddAction)
		{
			await HandleTitleAddUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate, staticData);
			return;
		}

		if (sourceTemplate.HasEmotionLearnAction)
		{
			await HandleEmotionLearnUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate);
			return;
		}

		if (sourceTemplate.ExpandInventoryAction != null)
		{
			await HandleInventoryExpansionUseItemAsync(
				player,
				inventoryItems,
				sourceItem,
				sourceTemplate,
				itemTemplates,
				staticData.ItemRestrictionCleanups);
			return;
		}

		if (sourceTemplate.DyeAction != null)
		{
			await HandleDyeUseItemAsync(
				player,
				inventoryItems,
				sourceItem,
				sourceTemplate,
				packet.TargetItemObjectId,
				itemTemplates,
				staticData.ItemRestrictionCleanups);
			return;
		}

		if (sourceTemplate.AnimationAction != null)
		{
			await HandleAnimationAddUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate);
			return;
		}

		if (sourceTemplate.AssemblyItemId != 0)
		{
			await HandleAssemblyUseItemAsync(player, sourceItem, sourceTemplate, staticData);
			return;
		}

		if (sourceTemplate.ExpExtractAction != null)
		{
			await HandleExpExtractUseItemAsync(player, sourceItem, sourceTemplate, staticData);
			return;
		}

		if (sourceTemplate.ApExtractAction != null)
		{
			await HandleApExtractUseItemAsync(player, sourceItem, sourceTemplate, packet.TargetItemObjectId, staticData);
			return;
		}

		if (sourceTemplate.HasExtractAction)
		{
			await HandleExtractUseItemAsync(player, sourceItem, sourceTemplate, packet.TargetItemObjectId, staticData);
			return;
		}

		if (sourceTemplate.HasDecomposeAction)
		{
			await HandleDecomposeUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate, staticData);
			return;
		}

		if (sourceTemplate.PolishSetId > 0 && packet.Type == 2)
		{
			var targetItem = packet.TargetItemObjectId == 0
				? null
				: inventoryItems.FirstOrDefault(item => item.ObjectId == packet.TargetItemObjectId);
			var polishPlan = IdianPolishService.CreatePolishPlan(sourceItem, targetItem, itemTemplates, itemRandomBonuses);
			switch (polishPlan.Result)
			{
				case IdianPolishResult.Success:
					await ScheduleIdianPolishAsync(player, inventoryItems, sourceItem, sourceTemplate, polishPlan, staticData, success: true);
					break;
				case IdianPolishResult.NoRandomBonus:
					await ScheduleIdianPolishAsync(player, inventoryItems, sourceItem, sourceTemplate, polishPlan, staticData, success: false);
					break;
				case IdianPolishResult.WrongLevel:
					await SendPacketAsync(SmSystemMessage.PolishWrongLevel());
					break;
				case IdianPolishResult.NeedIdentify:
					await SendPacketAsync(SmSystemMessage.PolishNeedIdentify());
					break;
			}
			return;
		}

		if (sourceTemplate.HasTamperingAction && packet.Type == 2)
		{
			await HandleTamperingUseItemAsync(
				player,
				sourceItem,
				sourceTemplate,
				packet.TargetItemObjectId,
				staticData);
			return;
		}

		if (sourceTemplate.ChargeActionMaxLevel > 0)
			await HandleChargeUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate, staticData);
	}

	private async Task HandleQuestStartUseItemAsync(
		Player player,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/templates/item/actions/QuestStartAction.canAct + act, then QuestService.startQuest.
		var questId = sourceTemplate.QuestStartQuestId;
		if (questId <= 0 || _playerEnterWorldService == null)
			return;

		if (!staticData.NearbyQuestTemplates.TryGetQuest(questId, out var questTemplate) || questTemplate == null)
			return;

		var existingQuest = player.Quests.FirstOrDefault(quest => quest.QuestId == questId);
		var isNewQuestState = existingQuest == null;
		if (existingQuest != null && !string.Equals(existingQuest.Status, "COMPLETE", StringComparison.Ordinal))
		{
			await SendPacketAsync(SmSystemMessage.QuestAcquireErrorWorkingQuest(), cancellationToken);
			return;
		}

		var startConditions = NearbyQuestStartConditionService.CheckNearbyStartConditions(
			player,
			questId,
			staticData.NearbyQuestTemplates,
			DateTimeOffset.Now);
		if (!startConditions.CanStart)
		{
			if (existingQuest != null && startConditions.Failure is NearbyQuestStartConditionFailure.RepeatCount or NearbyQuestStartConditionFailure.RepeatTiming)
				await SendPacketAsync(SmSystemMessage.QuestAcquireErrorNoneRepeatable(questTemplate.Name), cancellationToken);
			else if (CreateQuestStartConditionFailureMessage(startConditions.Failure, questTemplate) is { } failureMessage)
				await SendPacketAsync(failureMessage, cancellationToken);
			return;
		}

		if (!questTemplate.IsNoCount
			&& !CanStartNormalQuest(player, staticData.NearbyQuestTemplates)
			&& !HasPermission(player, _options.Membership.QuestLimitDisabled))
		{
			await SendPacketAsync(SmSystemMessage.QuestAcquireErrorMaxNormal(), cancellationToken);
			return;
		}

		var finalQuestState = existingQuest == null
			? new PlayerQuestState(questId, "START", 0, 0, 0)
			: existingQuest with { Status = "START" };
		if (!await _playerEnterWorldService.PersistQuestStartAsync(player, finalQuestState, isNewQuestState, cancellationToken))
			return;

		var questStates = player.Quests.ToList();
		var existingIndex = questStates.FindIndex(quest => quest.QuestId == questId);
		if (existingIndex >= 0)
			questStates[existingIndex] = finalQuestState;
		else
			questStates.Add(finalQuestState);
		player.Quests = questStates.ToArray();

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId));
		await SendPacketAsync(SmQuestAction.Add(finalQuestState), cancellationToken);
		await SendNearbyQuestRefreshAsync(player, cancellationToken);
	}

	private bool CanStartNormalQuest(Player player, NearbyQuestTemplateTable questTemplates)
	{
		// Java parity: QuestService.checkQuestListSize uses QuestStateList.getNormalQuests().size() + 1
		// where normal means category QUEST and status neither COMPLETE nor LOCKED.
		return player.Quests.Count(quest =>
			questTemplates.TryGetQuest(quest.QuestId, out var template)
			&& template != null
			&& string.Equals(template.QuestCategory, "QUEST", StringComparison.Ordinal)
			&& !string.Equals(quest.Status, "COMPLETE", StringComparison.Ordinal)
			&& !string.Equals(quest.Status, "LOCKED", StringComparison.Ordinal)) + 1 <= _options.Custom.BasicQuestSizeLimit;
	}

	private static bool HasPermission(Player player, byte permissionLevel)
	{
		// Java parity: model/gameobjects/player/Player.hasPermission.
		return player.AccountMembership >= permissionLevel;
	}

	private static SmSystemMessage? CreateQuestStartConditionFailureMessage(
		NearbyQuestStartConditionFailure failure,
		NearbyQuestTemplateSummary template)
	{
		// Java parity: QuestService.checkStartConditions warn=true emits these fixed system messages
		// before dialog start mutates quest state.
		return failure switch
		{
			NearbyQuestStartConditionFailure.Race => SmSystemMessage.QuestAcquireErrorRace(),
			NearbyQuestStartConditionFailure.MinLevel => SmSystemMessage.QuestAcquireErrorMinLevel(template.MinLevelPermitted),
			NearbyQuestStartConditionFailure.MaxLevel => SmSystemMessage.QuestAcquireErrorMaxLevel(template.MaxLevelPermitted),
			NearbyQuestStartConditionFailure.Class => SmSystemMessage.QuestAcquireErrorClass(),
			NearbyQuestStartConditionFailure.Gender => SmSystemMessage.QuestAcquireErrorGender(),
			_ => null,
		};
	}

	private async Task HandleAssemblyUseItemAsync(
		Player player,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/AssemblyItemAction.canAct + act.
		var validation = AssemblyItemService.CanAct(player, sourceTemplate, staticData.AssemblyItems);
		if (!validation.Succeeded)
			return;

		var removeCooldownDelayIdOnCancel = AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: true);
		await CancelPendingItemUseAsync(player);
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				sourceItem.ObjectId,
				sourceItem.ItemId,
				AssemblyItemService.UsageDelayMilliseconds,
				0,
				0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: sourceTemplate.GetClientName() ?? sourceTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.Item,
			delay: TimeSpan.FromMilliseconds(AssemblyItemService.UsageDelayMilliseconds),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteAssemblyUseItemAsync(player, sourceItem.ObjectId, sourceTemplate, staticData, cancellationToken);
			},
			cancelEndState: 2,
			removeCooldownDelayIdOnCancel: removeCooldownDelayIdOnCancel);
	}

	private async Task CompleteAssemblyUseItemAsync(
		Player player,
		int sourceItemObjectId,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		var inventoryItems = player.InventoryItems.ToList();
		var sourceItem = inventoryItems.FirstOrDefault(item => item.ObjectId == sourceItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		if (sourceItem == null)
		{
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItemObjectId, sourceTemplate.TemplateId, 0, 2, 0));
			return;
		}

		var assemblyItem = sourceTemplate.AssemblyItemId == 0
			? null
			: staticData.AssemblyItems.GetAssemblyItem(sourceTemplate.AssemblyItemId);
		if (assemblyItem == null)
		{
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 2, 0));
			return;
		}

		var rewardTemplate = staticData.ItemTemplates.GetItemTemplate(assemblyItem.ItemId);
		if (rewardTemplate == null)
		{
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 2, 0));
			return;
		}

		var mutationPlan = AssemblyItemService.CreateMutationPlan(
			player,
			inventoryItems,
			assemblyItem,
			rewardTemplate,
			staticData.ItemTemplates,
			() => _idFactory?.NextId() ?? 0);
		if (!mutationPlan.Succeeded)
		{
			// Java parity: AssemblyItemAction.act returns silently after already-decreased
			// parts when a later part disappears before the delayed completion runs.
			if (mutationPlan.UpdatedPartItems.Count == 0 && mutationPlan.DeletedPartObjectIds.Count == 0)
				return;

			var partialSaved = _playerEnterWorldService == null
				|| await _playerEnterWorldService.SaveAssemblyItemActionMutationAsync(
					player,
					mutationPlan.UpdatedPartItems,
					mutationPlan.DeletedPartObjectIds,
					Array.Empty<InventoryItem>(),
					Array.Empty<InventoryItem>(),
					cancellationToken);
			if (!partialSaved)
				return;

			await SendAssemblyConsumedPartPacketsAsync(
				inventoryItems,
				mutationPlan,
				staticData.ItemTemplates,
				staticData.ItemRestrictionCleanups);
			ApplyAssemblyInventoryMutation(inventoryItems, mutationPlan);
			player.InventoryItems = inventoryItems.ToArray();
			return;
		}

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveAssemblyItemActionMutationAsync(
				player,
				mutationPlan.UpdatedPartItems,
				mutationPlan.DeletedPartObjectIds,
				mutationPlan.UpdatedRewardItems,
				mutationPlan.AddedRewardItems,
				cancellationToken);
		if (!saved)
			return;

		await SendAssemblyConsumedPartPacketsAsync(
			inventoryItems,
			mutationPlan,
			staticData.ItemTemplates,
			staticData.ItemRestrictionCleanups);
		ApplyAssemblyInventoryMutation(inventoryItems, mutationPlan);
		player.InventoryItems = inventoryItems.ToArray();
		RegisterAssemblyExpirableAddedItems(player, mutationPlan.AddedRewardItems, rewardTemplate);

		await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 0));
		await SendPacketAsync(SmSystemMessage.AssemblyItemSucceeded());
		if (HasRewardMutation(mutationPlan.UpdatedRewardItems, mutationPlan.AddedRewardItems))
			await SendAssemblyRewardPacketsAsync(player, mutationPlan, rewardTemplate, staticData.ItemRestrictionCleanups);
		if (!mutationPlan.RewardSucceeded && mutationPlan.RewardInventoryFull)
			await SendPacketAsync(SmSystemMessage.DiceInventoryError());
	}

	private async Task SendAssemblyConsumedPartPacketsAsync(
		IReadOnlyList<InventoryItem> inventoryItems,
		AssemblyItemMutationPlan mutationPlan,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		foreach (var updatedPart in mutationPlan.UpdatedPartItems)
		{
			var template = itemTemplates.GetItemTemplate(updatedPart.ItemId);
			if (template != null)
				await SendPacketAsync(new SmInventoryUpdateItem(
					updatedPart,
					template,
					SmInventoryUpdateItem.DecreaseItemUse,
					GetGeneralInfoWarehouseRestrictionFlag(updatedPart.ItemId, itemRestrictionCleanups)));
		}

		foreach (var deletedPartObjectId in mutationPlan.DeletedPartObjectIds)
		{
			if (inventoryItems.Any(item => item.ObjectId == deletedPartObjectId))
				await SendPacketAsync(new SmDeleteItem(deletedPartObjectId, SmDeleteItem.UseDeleteType));
		}
	}

	private static void ApplyAssemblyInventoryMutation(List<InventoryItem> inventoryItems, AssemblyItemMutationPlan mutationPlan)
	{
		foreach (var updatedPart in mutationPlan.UpdatedPartItems)
			ReplaceInventoryItem(inventoryItems, updatedPart);
		foreach (var deletedPartObjectId in mutationPlan.DeletedPartObjectIds)
			inventoryItems.RemoveAll(item => item.ObjectId == deletedPartObjectId);
		foreach (var updatedReward in mutationPlan.UpdatedRewardItems)
			ReplaceInventoryItem(inventoryItems, updatedReward);
		inventoryItems.AddRange(mutationPlan.AddedRewardItems);
	}

	private void RegisterAssemblyExpirableAddedItems(Player player, IReadOnlyList<InventoryItem> addedRewardItems, ItemTemplateSummary rewardTemplate)
	{
		// Java parity: services/item/ItemService.addNonStackableItem registers newly created expirable items.
		if (rewardTemplate.MaxStackCount > 1)
			return;

		foreach (var addedRewardItem in addedRewardItems)
			_expirableTaskService?.RegisterInventoryItem(player, addedRewardItem);
	}

	private async Task SendAssemblyRewardPacketsAsync(
		Player player,
		AssemblyItemMutationPlan mutationPlan,
		ItemTemplateSummary rewardTemplate,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		foreach (var updatedReward in mutationPlan.UpdatedRewardItems)
			await SendPacketAsync(
				new SmInventoryUpdateItem(
					updatedReward,
					rewardTemplate,
					SmInventoryUpdateItem.IncreaseItemCollect,
					GetGeneralInfoWarehouseRestrictionFlag(updatedReward.ItemId, itemRestrictionCleanups)));
		var projectedCubeCount = player.InventoryItems.Count - mutationPlan.AddedRewardItems.Count;
		foreach (var addedReward in mutationPlan.AddedRewardItems)
		{
			await SendPacketAsync(
				SmInventoryAddItem.CreateItemCollect(
					addedReward,
					rewardTemplate,
					GetGeneralInfoWarehouseRestrictionFlag(addedReward.ItemId, itemRestrictionCleanups)));
			projectedCubeCount++;
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
		}
	}

	private async Task HandleExpExtractUseItemAsync(
		Player player,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/ExpExtractAction.canAct + act.
		var validation = ExpExtractService.Validate(player, sourceTemplate, staticData);
		if (!validation.Succeeded)
		{
			await SendExpExtractFailureAsync(validation.Failure);
			return;
		}

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		await CancelPendingItemUseAsync(player);
		await SendPacketAsync(
			new SmItemUsageAnimation(
				player.ObjectId,
				sourceItem.ObjectId,
				sourceItem.ItemId,
				ExpExtractService.UsageDelayMilliseconds,
				0,
				0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: sourceTemplate.GetClientName() ?? sourceTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.Decompose,
			delay: TimeSpan.FromMilliseconds(ExpExtractService.UsageDelayMilliseconds),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteExpExtractUseItemAsync(player, sourceTemplate, staticData, cancellationToken);
			},
			cancelEndState: 2,
			cancelAnimationToSelfOnly: true);
	}

	private async Task CompleteExpExtractUseItemAsync(
		Player player,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		var validation = ExpExtractService.Validate(player, sourceTemplate, staticData);
		if (!validation.Succeeded || validation.RewardTemplate == null)
		{
			await SendExpExtractFailureAsync(validation.Failure);
			await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, player.UsingItemObjectId, sourceTemplate.TemplateId, 0, 2, 0));
			return;
		}

		var inventoryItems = player.InventoryItems.ToList();
		var mutationPlan = ExpExtractService.CreateMutationPlan(
			player,
			inventoryItems,
			sourceTemplate,
			validation,
			staticData.ItemTemplates,
			() => _idFactory?.NextId() ?? 0);
		if (!mutationPlan.Succeeded)
		{
			await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, player.UsingItemObjectId, sourceTemplate.TemplateId, 0, 2, 0));
			return;
		}

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveExpExtractActionMutationAsync(
				player,
				validation.NewExp,
				mutationPlan.SourceItemUpdate,
				mutationPlan.DeletedSourceItemObjectId,
				mutationPlan.UpdatedRewardItems,
				mutationPlan.AddedRewardItems,
				cancellationToken);
		if (!saved)
			return;

		await ApplyExpExtractSourceMutationAsync(
			inventoryItems,
			sourceTemplate,
			mutationPlan.SourceItemUpdate,
			mutationPlan.DeletedSourceItemObjectId,
			staticData.ItemRestrictionCleanups);
		player.Exp = validation.NewExp;
		await SendPacketAsync(new SmStatUpdateExp(player, staticData.PlayerExperienceTable));
		ApplyExpExtractRewardMutation(inventoryItems, mutationPlan);
		player.InventoryItems = inventoryItems.ToArray();
		RegisterExpExtractExpirableAddedItems(player, mutationPlan.AddedRewardItems, validation.RewardTemplate);

		if (HasRewardMutation(mutationPlan.UpdatedRewardItems, mutationPlan.AddedRewardItems))
			await SendExpExtractRewardPacketsAsync(player, mutationPlan, validation.RewardTemplate, staticData.ItemRestrictionCleanups);
		if (!mutationPlan.RewardSucceeded && mutationPlan.RewardInventoryFull)
			await SendPacketAsync(SmSystemMessage.DiceInventoryError());

		await SendPacketAsync(
			SmSystemMessage.ExpExtractionUse(
				sourceTemplate.GetClientName() ?? sourceTemplate.Name,
				validation.RequiredExp,
				validation.RewardTemplate.GetClientName() ?? validation.RewardTemplate.Name));
		await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, player.UsingItemObjectId, sourceTemplate.TemplateId, 0, 1, 0));
	}

	private async Task SendExpExtractFailureAsync(ExpExtractFailure failure)
	{
		switch (failure)
		{
			case ExpExtractFailure.InventoryFull:
				await SendPacketAsync(SmSystemMessage.DecompressInventoryFull());
				break;
			case ExpExtractFailure.NotEnoughExp:
				await SendPacketAsync(SmSystemMessage.ExpExtractionUseNotEnoughExp());
				break;
		}
	}

	private async Task ApplyExpExtractSourceMutationAsync(
		List<InventoryItem> inventoryItems,
		ItemTemplateSummary sourceTemplate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		if (deletedSourceItemObjectId.HasValue)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == deletedSourceItemObjectId.Value);
			await SendPacketAsync(new SmDeleteItem(deletedSourceItemObjectId.Value, SmDeleteItem.UseDeleteType));
		}
		else if (sourceItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, sourceItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(
				sourceItemUpdate,
				sourceTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(sourceItemUpdate.ItemId, itemRestrictionCleanups)));
		}
	}

	private static void ApplyExpExtractRewardMutation(List<InventoryItem> inventoryItems, ExpExtractMutationPlan mutationPlan)
	{
		foreach (var updatedReward in mutationPlan.UpdatedRewardItems)
			ReplaceInventoryItem(inventoryItems, updatedReward);
		inventoryItems.AddRange(mutationPlan.AddedRewardItems);
	}

	private void RegisterExpExtractExpirableAddedItems(Player player, IReadOnlyList<InventoryItem> addedRewardItems, ItemTemplateSummary rewardTemplate)
	{
		// Java parity: services/item/ItemService.addNonStackableItem registers newly created expirable items.
		if (rewardTemplate.MaxStackCount > 1)
			return;

		foreach (var addedRewardItem in addedRewardItems)
			_expirableTaskService?.RegisterInventoryItem(player, addedRewardItem);
	}

	private async Task SendExpExtractRewardPacketsAsync(
		Player player,
		ExpExtractMutationPlan mutationPlan,
		ItemTemplateSummary rewardTemplate,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		foreach (var updatedReward in mutationPlan.UpdatedRewardItems)
			await SendPacketAsync(
				new SmInventoryUpdateItem(
					updatedReward,
					rewardTemplate,
					SmInventoryUpdateItem.IncreaseItemCollect,
					GetGeneralInfoWarehouseRestrictionFlag(updatedReward.ItemId, itemRestrictionCleanups)));
		var projectedCubeCount = player.InventoryItems.Count - mutationPlan.AddedRewardItems.Count;
		foreach (var addedReward in mutationPlan.AddedRewardItems)
		{
			await SendPacketAsync(
				SmInventoryAddItem.CreateItemCollect(
					addedReward,
					rewardTemplate,
					GetGeneralInfoWarehouseRestrictionFlag(addedReward.ItemId, itemRestrictionCleanups)));
			projectedCubeCount++;
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
		}
	}

	private async Task HandleApExtractUseItemAsync(
		Player player,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		int targetItemObjectId,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/ApExtractAction.canAct + act.
		var plan = ApExtractService.CreateMutationPlan(
			player,
			sourceItem.ObjectId,
			targetItemObjectId,
			staticData.ItemTemplates,
			CreateAbyssPointsOptions());
		if (!plan.Succeeded || plan.AbyssPointsPlan?.UpdatedRank == null)
			return;

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveApExtractActionMutationAsync(player, plan);
		if (!saved)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		await SendApExtractConsumedItemPacketsAsync(player, inventoryItems, plan, sourceTemplate, staticData.ItemRestrictionCleanups);
		ApplyApExtractInventoryMutation(inventoryItems, plan);
		player.InventoryItems = inventoryItems.ToArray();
		player.AbyssRank = plan.AbyssPointsPlan.UpdatedRank;
		foreach (var packet in plan.AbyssPointsPlan.PlayerPackets)
			await SendPacketAsync(packet);
		await ApplyAbyssRankChangedSideEffectsAsync(player, plan.AbyssPointsPlan.OldRank, staticData);
	}

	private async Task SendApExtractConsumedItemPacketsAsync(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		ApExtractPlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		var projectedCubeCount = inventoryItems.Count;
		if (inventoryItems.Any(item => item.ObjectId == plan.DeletedTargetItemObjectId))
		{
			projectedCubeCount--;
			await SendPacketAsync(new SmDeleteItem(plan.DeletedTargetItemObjectId));
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
		}

		if (plan.SourceItemUpdate != null)
		{
			await SendPacketAsync(new SmInventoryUpdateItem(
				plan.SourceItemUpdate,
				sourceTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(plan.SourceItemUpdate.ItemId, itemRestrictionCleanups)));
		}
		else if (plan.DeletedSourceItemObjectId.HasValue && inventoryItems.Any(item => item.ObjectId == plan.DeletedSourceItemObjectId.Value))
		{
			projectedCubeCount--;
			await SendPacketAsync(new SmDeleteItem(plan.DeletedSourceItemObjectId.Value, SmDeleteItem.UseDeleteType));
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
		}
	}

	private static void ApplyApExtractInventoryMutation(List<InventoryItem> inventoryItems, ApExtractPlan plan)
	{
		inventoryItems.RemoveAll(item => item.ObjectId == plan.DeletedTargetItemObjectId);
		if (plan.SourceItemUpdate != null)
			ReplaceInventoryItem(inventoryItems, plan.SourceItemUpdate);
		if (plan.DeletedSourceItemObjectId.HasValue)
			inventoryItems.RemoveAll(item => item.ObjectId == plan.DeletedSourceItemObjectId.Value);
	}

	private async Task HandleExtractUseItemAsync(
		Player player,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		int targetItemObjectId,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/ExtractAction.canAct + act.
		var inventoryItems = player.InventoryItems.ToList();
		var targetItem = targetItemObjectId == 0
			? null
			: inventoryItems.FirstOrDefault(item => item.ObjectId == targetItemObjectId);
		if (targetItem == null)
		{
			await SendPacketAsync(SmSystemMessage.DecomposeItemNoTarget());
			return;
		}

		var targetTemplate = staticData.ItemTemplates.GetItemTemplate(targetItem.ItemId);
		var targetItemName = targetTemplate?.GetClientName()
			?? targetTemplate?.Name
			?? targetItem.ItemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
		if (targetTemplate == null || (!targetTemplate.IsArmor && !targetTemplate.IsWeapon))
		{
			await SendPacketAsync(SmSystemMessage.DecomposeItemCannotDecompose(targetItemName));
			return;
		}

		if (targetItem.IsEquipped)
		{
			await SendPacketAsync(SmSystemMessage.DecomposeEquippedItemCannotDecompose());
			return;
		}

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		await CancelPendingItemUseAsync(player);
		await SendPacketAsync(
			new SmItemUsageAnimation(
				player.ObjectId,
				sourceItem.ObjectId,
				sourceItem.ItemId,
				5000,
				0,
				0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: targetItemName,
			cancelMessage: PendingItemUseCancelMessage.Decompose,
			delay: TimeSpan.FromMilliseconds(5000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteExtractUseItemAsync(player, sourceItem.ObjectId, sourceTemplate, targetItem.ObjectId, staticData, cancellationToken);
			},
			cancelEndState: 2,
			cancelAnimationToSelfOnly: true);
	}

	private async Task CompleteExtractUseItemAsync(
		Player player,
		int sourceItemObjectId,
		ItemTemplateSummary sourceTemplate,
		int targetItemObjectId,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		var inventoryItems = player.InventoryItems.ToList();
		var plan = EnchantService.CreateBreakItemPlan(
			player,
			targetItemObjectId,
			sourceItemObjectId,
			staticData.ItemTemplates,
			() => _idFactory?.NextId() ?? 0,
			RandomInclusive);
		if (!plan.Succeeded)
		{
			await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, sourceItemObjectId, sourceTemplate.TemplateId, 0, 2, 0));
			return;
		}

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveBreakItemActionMutationAsync(player, plan, cancellationToken);
		if (!saved)
			return;

		await SendExtractConsumedItemPacketsAsync(player, inventoryItems, plan, sourceTemplate, staticData.ItemRestrictionCleanups);
		ApplyBreakItemInventoryMutation(inventoryItems, plan);
		player.InventoryItems = inventoryItems.ToArray();

		if (staticData.ItemTemplates.GetItemTemplate(plan.RewardItemId) is { } rewardTemplate)
		{
			RegisterExtractExpirableAddedItems(player, plan.AddedRewardItems, rewardTemplate);
			if (HasRewardMutation(plan.UpdatedRewardItems, plan.AddedRewardItems))
				await SendExtractRewardPacketsAsync(player, plan, rewardTemplate, staticData.ItemRestrictionCleanups);
		}

		if (!plan.RewardSucceeded && plan.RewardInventoryFull)
			await SendPacketAsync(SmSystemMessage.DiceInventoryError());
		await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, sourceItemObjectId, sourceTemplate.TemplateId, 0, 1, 0));
	}

	private async Task SendExtractConsumedItemPacketsAsync(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		BreakItemPlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		var projectedCubeCount = inventoryItems.Count;
		if (inventoryItems.Any(item => item.ObjectId == plan.DeletedTargetItemObjectId))
		{
			projectedCubeCount--;
			await SendPacketAsync(new SmDeleteItem(plan.DeletedTargetItemObjectId));
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
		}

		if (plan.SourceItemUpdate != null)
		{
			await SendPacketAsync(new SmInventoryUpdateItem(
				plan.SourceItemUpdate,
				sourceTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(plan.SourceItemUpdate.ItemId, itemRestrictionCleanups)));
		}
		else if (plan.DeletedSourceItemObjectId.HasValue && inventoryItems.Any(item => item.ObjectId == plan.DeletedSourceItemObjectId.Value))
		{
			projectedCubeCount--;
			await SendPacketAsync(new SmDeleteItem(plan.DeletedSourceItemObjectId.Value, SmDeleteItem.UseDeleteType));
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
		}
	}

	private static void ApplyBreakItemInventoryMutation(List<InventoryItem> inventoryItems, BreakItemPlan plan)
	{
		inventoryItems.RemoveAll(item => item.ObjectId == plan.DeletedTargetItemObjectId);
		if (plan.SourceItemUpdate != null)
			ReplaceInventoryItem(inventoryItems, plan.SourceItemUpdate);
		if (plan.DeletedSourceItemObjectId.HasValue)
			inventoryItems.RemoveAll(item => item.ObjectId == plan.DeletedSourceItemObjectId.Value);
		foreach (var updatedReward in plan.UpdatedRewardItems)
			ReplaceInventoryItem(inventoryItems, updatedReward);
		inventoryItems.AddRange(plan.AddedRewardItems);
	}

	private void RegisterExtractExpirableAddedItems(Player player, IReadOnlyList<InventoryItem> addedRewardItems, ItemTemplateSummary rewardTemplate)
	{
		// Java parity: services/item/ItemService.addNonStackableItem registers newly created expirable items.
		if (rewardTemplate.MaxStackCount > 1)
			return;

		foreach (var addedRewardItem in addedRewardItems)
			_expirableTaskService?.RegisterInventoryItem(player, addedRewardItem);
	}

	private async Task SendExtractRewardPacketsAsync(
		Player player,
		BreakItemPlan plan,
		ItemTemplateSummary rewardTemplate,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		foreach (var updatedReward in plan.UpdatedRewardItems)
			await SendPacketAsync(
				new SmInventoryUpdateItem(
					updatedReward,
					rewardTemplate,
					SmInventoryUpdateItem.IncreaseItemCollect,
					GetGeneralInfoWarehouseRestrictionFlag(updatedReward.ItemId, itemRestrictionCleanups)));
		var projectedCubeCount = player.InventoryItems.Count - plan.AddedRewardItems.Count;
		foreach (var addedReward in plan.AddedRewardItems)
		{
			await SendPacketAsync(
				SmInventoryAddItem.CreateItemCollect(
					addedReward,
					rewardTemplate,
					GetGeneralInfoWarehouseRestrictionFlag(addedReward.ItemId, itemRestrictionCleanups)));
			projectedCubeCount++;
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
		}
	}

	private static bool HasRewardMutation(IReadOnlyCollection<InventoryItem> updatedItems, IReadOnlyCollection<InventoryItem> addedItems)
	{
		return updatedItems.Count > 0 || addedItems.Count > 0;
	}

	private async Task HandleDecomposeUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/DecomposeAction.canAct + act.
		var canAct = DecomposeService.CanAct(player, sourceItem, staticData);
		if (!canAct.Succeeded)
		{
			await SendDecomposeFailureAsync(canAct.Failure, sourceTemplate);
			return;
		}

		if (canAct.IsSelectable)
		{
			var selectableItems = DecomposeService.GetSelectableItems(player, staticData.DecomposableItems, sourceItem.ItemId);
			if (selectableItems == null)
				return;

			AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
			await CancelPendingItemUseAsync(player);
			await SendPacketAsync(new SmFirstShowDecomposable(sourceItem.ObjectId, selectableItems));
			return;
		}

		var rewardPlan = DecomposeService.CreateNormalRewardPlan(player, sourceItem, sourceTemplate, staticData);
		if (!rewardPlan.Succeeded)
		{
			await SendDecomposeFailureAsync(rewardPlan.Failure, sourceTemplate);
			return;
		}

		var removeCooldownDelayIdOnCancel = AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: true);
		await CancelPendingItemUseAsync(player);
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				sourceItem.ObjectId,
				sourceItem.ItemId,
				DecomposeService.UsageDelayMilliseconds,
				0,
				0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: sourceTemplate.GetClientName() ?? sourceTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.Decompose,
			delay: TimeSpan.FromMilliseconds(DecomposeService.UsageDelayMilliseconds),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteDecomposeUseItemAsync(player, sourceItem.ObjectId, sourceTemplate, rewardPlan, staticData, cancellationToken);
			},
			cancelEndState: 2,
			removeCooldownDelayIdOnCancel: removeCooldownDelayIdOnCancel);
	}

	private async Task CompleteDecomposeUseItemAsync(
		Player player,
		int sourceItemObjectId,
		ItemTemplateSummary sourceTemplate,
		DecomposeRewardPlan rewardPlan,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		var inventoryItems = player.InventoryItems.ToList();
		var sourceItem = inventoryItems.FirstOrDefault(item => item.ObjectId == sourceItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		if (sourceItem == null)
		{
			await SendPacketAsync(SmSystemMessage.DecomposeItemNoTarget());
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItemObjectId, sourceTemplate.TemplateId, 0, 2, 0));
			return;
		}

		var canAct = DecomposeService.CanAct(player, sourceItem, staticData);
		if (!canAct.Succeeded)
		{
			await SendDecomposeFailureAsync(canAct.Failure, sourceTemplate);
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 2, 0));
			return;
		}

		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var rewardBaseItems = inventoryItems.ToList();
		ApplySourceInventoryMutation(rewardBaseItems, sourceItemUpdate, deletedSourceObjectId);
		var rewardInventoryPlan = CreateDecomposeRewardInventoryPlan(player, rewardBaseItems, rewardPlan.Rewards, staticData.ItemTemplates);
		if (rewardInventoryPlan == null)
			return;

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveDecomposeActionMutationAsync(
				player,
				rewardInventoryPlan.UpdatedItems,
				rewardInventoryPlan.AddedItems,
				sourceItemUpdate,
				deletedSourceObjectId,
				cancellationToken);
		if (!saved)
			return;

		await ApplySourceItemMutationAsync(
			player,
			inventoryItems,
			sourceTemplate,
			sourceItemUpdate,
			deletedSourceObjectId,
			staticData.ItemRestrictionCleanups);
		ApplyRewardInventoryMutation(inventoryItems, rewardInventoryPlan);
		player.InventoryItems = inventoryItems.ToArray();
		RegisterExpirableAddedItems(player, rewardInventoryPlan.Packets);
		await SendPacketAsync(SmSystemMessage.DecomposeItemSucceed(sourceTemplate.GetClientName() ?? sourceTemplate.Name));
		await SendDecomposeRewardItemsAsync(player, rewardInventoryPlan.Packets, staticData.ItemRestrictionCleanups);
		await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 0));
	}

	private async Task HandleSelectDecomposableAsync(Player player, CmSelectDecomposable packet)
	{
		// Java parity: network/aion/clientpackets/CM_SELECT_DECOMPOSABLE.runImpl.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		var sourceItem = inventoryItems.FirstOrDefault(item => item.ObjectId == packet.ObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		if (sourceItem == null)
			return;

		var sourceTemplate = staticData.ItemTemplates.GetItemTemplate(sourceItem.ItemId);
		if (sourceTemplate == null)
			return;

		var rewardPlan = DecomposeService.CreateSelectableRewardPlan(
			player,
			staticData.DecomposableItems,
			sourceItem.ItemId,
			packet.Index);
		if (!rewardPlan.Succeeded)
			return;

		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var rewardBaseItems = inventoryItems.ToList();
		ApplySourceInventoryMutation(rewardBaseItems, sourceItemUpdate, deletedSourceObjectId);
		var rewardInventoryPlan = CreateDecomposeRewardInventoryPlan(player, rewardBaseItems, rewardPlan.Rewards, staticData.ItemTemplates);
		if (rewardInventoryPlan == null)
			return;

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveDecomposeActionMutationAsync(
				player,
				rewardInventoryPlan.UpdatedItems,
				rewardInventoryPlan.AddedItems,
				sourceItemUpdate,
				deletedSourceObjectId);
		if (!saved)
			return;

		await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 1));
		await SendPacketAsync(SmSystemMessage.UncompressCompressedItemSucceeded(sourceTemplate.GetClientName() ?? sourceTemplate.Name));
		await ApplySourceItemMutationAsync(
			player,
			inventoryItems,
			sourceTemplate,
			sourceItemUpdate,
			deletedSourceObjectId,
			staticData.ItemRestrictionCleanups);
		await SendPacketAsync(new SmSecondaryShowDecomposable(sourceItem.ObjectId, Array.Empty<ResultedItemSummary>()));
		ApplyRewardInventoryMutation(inventoryItems, rewardInventoryPlan);
		player.InventoryItems = inventoryItems.ToArray();
		RegisterExpirableAddedItems(player, rewardInventoryPlan.Packets);
		await SendDecomposeRewardItemsAsync(player, rewardInventoryPlan.Packets, staticData.ItemRestrictionCleanups);
	}

	private DecomposeRewardInventoryPlan? CreateDecomposeRewardInventoryPlan(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<DecomposeReward> rewards,
		ItemTemplateTable itemTemplates)
	{
		var workingItems = inventoryItems.ToList();
		var updatedItemsByObjectId = new Dictionary<int, InventoryItem>();
		var addedItems = new List<InventoryItem>();
		var packets = new List<DecomposeRewardPacket>();
		foreach (var reward in rewards)
		{
			var rewardTemplate = itemTemplates.GetItemTemplate(reward.ItemId);
			if (rewardTemplate == null)
				return null;

			var addPlan = InventoryAddService.CreateAddItemPlan(
				player,
				workingItems,
				rewardTemplate,
				reward.Count,
				() => _idFactory?.NextId() ?? 0,
				allowInventoryOverflow: true);
			if (!addPlan.Succeeded)
				return null;

			foreach (var updatedItem in addPlan.UpdatedItems)
			{
				updatedItemsByObjectId[updatedItem.ObjectId] = updatedItem;
				ReplaceInventoryItem(workingItems, updatedItem);
				packets.Add(new DecomposeRewardPacket(updatedItem, rewardTemplate, IsNewItem: false));
			}

			foreach (var addedItem in addPlan.AddedItems)
			{
				addedItems.Add(addedItem);
				workingItems.Add(addedItem);
				packets.Add(new DecomposeRewardPacket(addedItem, rewardTemplate, IsNewItem: true));
			}
		}

		return new DecomposeRewardInventoryPlan(updatedItemsByObjectId.Values.ToArray(), addedItems, packets);
	}

	private static void ApplyRewardInventoryMutation(List<InventoryItem> inventoryItems, DecomposeRewardInventoryPlan rewardInventoryPlan)
	{
		foreach (var updatedItem in rewardInventoryPlan.UpdatedItems)
			ReplaceInventoryItem(inventoryItems, updatedItem);
		inventoryItems.AddRange(rewardInventoryPlan.AddedItems);
	}

	private void RegisterExpirableAddedItems(Player player, IReadOnlyList<DecomposeRewardPacket> rewardPackets)
	{
		// Java parity: services/item/ItemService.addNonStackableItem registers newly created expirable items.
		foreach (var rewardPacket in rewardPackets.Where(packet => packet is { IsNewItem: true, Template.MaxStackCount: <= 1 }))
			_expirableTaskService?.RegisterInventoryItem(player, rewardPacket.Item);
	}

	private async Task SendDecomposeRewardItemsAsync(
		Player player,
		IReadOnlyList<DecomposeRewardPacket> rewardPackets,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		var newItemCount = rewardPackets.Count(packet => packet.IsNewItem);
		var projectedCubeCount = player.InventoryItems.Count - newItemCount;
		foreach (var rewardPacket in rewardPackets)
		{
			var cleanupSealFlag = GetGeneralInfoWarehouseRestrictionFlag(rewardPacket.Item.ItemId, itemRestrictionCleanups);
			if (rewardPacket.IsNewItem)
			{
				await SendPacketAsync(
					SmInventoryAddItem.CreateDecomposable(
					[
						new SmInventoryAddItem.InventoryPacketItem(rewardPacket.Item, rewardPacket.Template, cleanupSealFlag),
					]));
				projectedCubeCount++;
				await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(projectedCubeCount, player.NpcExpands, player.QuestExpands, player.ItemExpands));
			}
			else
			{
				await SendPacketAsync(
					new SmInventoryUpdateItem(
						rewardPacket.Item,
						rewardPacket.Template,
						SmInventoryUpdateItem.IncreaseItemCollect,
						cleanupSealFlag));
			}
		}
	}

	private async Task SendDecomposeFailureAsync(DecomposeFailure failure, ItemTemplateSummary sourceTemplate)
	{
		var itemName = sourceTemplate.GetClientName() ?? sourceTemplate.Name;
		switch (failure)
		{
			case DecomposeFailure.CannotDecompose:
				await SendPacketAsync(SmSystemMessage.DecomposeItemCannotDecompose(itemName));
				break;
			case DecomposeFailure.InventoryFull:
				await SendPacketAsync(SmSystemMessage.DecomposeItemInventoryFull());
				break;
			case DecomposeFailure.Failed:
				await SendPacketAsync(SmSystemMessage.DecomposeItemFailed(itemName));
				break;
		}
	}

	private async Task ApplySourceItemMutationAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		ItemTemplateSummary sourceTemplate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceObjectId,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		if (deletedSourceObjectId.HasValue)
		{
			ApplySourceInventoryMutation(inventoryItems, sourceItemUpdate, deletedSourceObjectId);
			player.InventoryItems = inventoryItems.ToArray();
			await SendPacketAsync(new SmDeleteItem(deletedSourceObjectId.Value, SmDeleteItem.UseDeleteType));
			await SendPacketAsync(SmCubeUpdate.CubeSize(player));
		}
		else if (sourceItemUpdate != null)
		{
			ApplySourceInventoryMutation(inventoryItems, sourceItemUpdate, deletedSourceObjectId);
			player.InventoryItems = inventoryItems.ToArray();
			await SendPacketAsync(new SmInventoryUpdateItem(
				sourceItemUpdate,
				sourceTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(sourceItemUpdate.ItemId, itemRestrictionCleanups)));
		}
	}

	private static void ApplySourceInventoryMutation(
		List<InventoryItem> inventoryItems,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceObjectId)
	{
		if (deletedSourceObjectId.HasValue)
			inventoryItems.RemoveAll(item => item.ObjectId == deletedSourceObjectId.Value);
		else if (sourceItemUpdate != null)
			ReplaceInventoryItem(inventoryItems, sourceItemUpdate);
	}

	private async Task HandleCraftLearnUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/CraftLearnAction.canAct + act.
		var validation = CraftLearnService.ValidateNewRecipe(player, sourceTemplate.CraftLearnRecipeId, staticData);
		if (!validation.Succeeded)
		{
			await SendCraftLearnFailureAsync(validation);
			return;
		}

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveCraftLearnActionMutationAsync(
				player,
				validation.RecipeTemplate!.RecipeId,
				sourceItemUpdate,
				deletedSourceObjectId);
		if (!saved)
			return;

		var recipeTemplate = validation.RecipeTemplate!;
		if (deletedSourceObjectId.HasValue)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == deletedSourceObjectId.Value);
			await SendPacketAsync(new SmDeleteItem(deletedSourceObjectId.Value, SmDeleteItem.UseDeleteType));
		}
		else if (sourceItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, sourceItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(
				sourceItemUpdate,
				sourceTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(sourceItemUpdate.ItemId, staticData.ItemRestrictionCleanups)));
		}

		player.InventoryItems = inventoryItems.ToArray();
		player.Recipes = player.Recipes
			.Append(recipeTemplate.RecipeId)
			.Distinct()
			.Order()
			.ToArray();
		await SendPacketAsync(new SmLearnRecipe(recipeTemplate.RecipeId));
		await SendPacketAsync(SmSystemMessage.CraftRecipeLearn(recipeTemplate.RecipeId, player.Name));
		await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 1));
	}

	private async Task HandleSkillLearnUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/SkillLearnAction.canAct + act.
		var plan = SkillLearnService.CreateSkillBookPlan(player, sourceTemplate, staticData);
		if (!plan.Succeeded)
			return;

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		await CancelPendingItemUseAsync(player);
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 1));

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveSkillLearnActionMutationAsync(
				player,
				plan.PersistedSkills,
				sourceItemUpdate: null,
				deletedSourceItemObjectId: sourceItem.ObjectId);
		if (!saved)
			return;

		player.Skills = plan.Skills;
		foreach (var packet in plan.Packets)
			await SendPacketAsync(new SmSkillList([packet.Skill], packet.MessageId));

		await DeleteDirectSourceItemAsync(player, inventoryItems, sourceItem.ObjectId);
	}

	private async Task HandleTitleAddUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/TitleAddAction.canAct + act.
		var canAct = TitleAddService.ValidateCanAct(player, sourceTemplate.TitleAddTitleId);
		if (!canAct.Succeeded)
		{
			await SendTitleAddFailureAsync(canAct);
			return;
		}

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 1));

		var validation = TitleAddService.CreateTitle(
			player,
			sourceTemplate.TitleAddTitleId,
			sourceTemplate.TitleAddMinutes,
			sourceTemplate.HasTitleAddMinutes,
			staticData.TitleTemplates,
			DateTimeOffset.UtcNow);
		if (!validation.Succeeded)
		{
			await SendTitleAddFailureAsync(validation);
			return;
		}

		var title = validation.Title!;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveTitleAddActionMutationAsync(
				player,
				title,
				sourceItemUpdate: null,
				deletedSourceItemObjectId: sourceItem.ObjectId);
		if (!saved)
			return;

		player.Titles = player.Titles
			.Where(existing => existing.Id != title.Id)
			.Append(title)
			.ToArray();
		_expirableTaskService?.RegisterTitle(player, title);
		await SendPacketAsync(SmSystemMessage.CashTitle(ChatUtil.L10n(validation.TitleTemplate!.NameId)));
		await SendPacketAsync(new SmTitleInfo(player.Titles));

		await DeleteDirectSourceItemAsync(player, inventoryItems, sourceItem.ObjectId);
	}

	private async Task SendTitleAddFailureAsync(TitleAddValidation validation)
	{
		switch (validation.Failure)
		{
			case TitleAddFailure.InvalidItem:
				await SendPacketAsync(SmSystemMessage.ItemColorError());
				break;
			case TitleAddFailure.AlreadyKnown:
				await SendPacketAsync(SmSystemMessage.TooltipLearnedTitle());
				break;
			case TitleAddFailure.InvalidRace:
				await SendPacketAsync(new SmMessage("This title is not available for your race."));
				break;
		}
	}

	private async Task HandleEmotionLearnUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate)
	{
		// Java parity: model/templates/item/actions/EmotionLearnAction.canAct + act.
		var validation = EmotionLearnService.ValidateNewEmotion(
			player,
			sourceTemplate.EmotionLearnId,
			sourceTemplate.EmotionLearnMinutes,
			DateTimeOffset.UtcNow);
		if (!validation.Succeeded)
		{
			await SendEmotionLearnFailureAsync(validation.Failure);
			return;
		}

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		var emotion = validation.Emotion!;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveEmotionLearnActionMutationAsync(
				player,
				emotion,
				sourceItemUpdate: null,
				deletedSourceItemObjectId: sourceItem.ObjectId);
		if (!saved)
			return;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 1));

		player.Emotions = player.Emotions
			.Where(existing => existing.Id != emotion.Id)
			.Append(emotion)
			.ToArray();
		_expirableTaskService?.RegisterEmotion(player, emotion);
		await SendPacketAsync(new SmEmotionList(1, [emotion]));

		await DeleteDirectSourceItemAsync(player, inventoryItems, sourceItem.ObjectId);
	}

	private async Task DeleteDirectSourceItemAsync(Player player, List<InventoryItem> inventoryItems, int sourceItemObjectId)
	{
		// Java parity: SkillLearnAction/TitleAddAction/EmotionLearnAction use Inventory.delete(item),
		// which sends the default delete mask and a cube-size refresh, not DEC_ITEM_USE.
		inventoryItems.RemoveAll(item => item.ObjectId == sourceItemObjectId);
		player.InventoryItems = inventoryItems.ToArray();
		await SendPacketAsync(new SmDeleteItem(sourceItemObjectId));
		await SendPacketAsync(SmCubeUpdate.CubeSize(player));
	}

	private async Task SendEmotionLearnFailureAsync(EmotionLearnFailure failure)
	{
		switch (failure)
		{
			case EmotionLearnFailure.InvalidItem:
				await SendPacketAsync(SmSystemMessage.ItemColorError());
				break;
			case EmotionLearnFailure.AlreadyKnown:
				await SendPacketAsync(SmSystemMessage.TooltipLearnedEmotion());
				break;
		}
	}

	private async Task HandleInventoryExpansionUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: model/templates/item/actions/ExpandInventoryAction.canAct + act.
		var plan = InventoryExpansionService.CreatePlan(
			player,
			sourceTemplate.ExpandInventoryAction,
			_options.Custom.CubeExpansionLimit);
		if (!plan.Succeeded)
		{
			await SendInventoryExpansionFailureAsync(plan.Failure);
			return;
		}

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveInventoryExpansionMutationAsync(
				player,
				plan.NewItemExpands,
				plan.NewWarehouseBonusExpands,
				sourceItemUpdate,
				deletedSourceObjectId);
		if (!saved)
			return;

		if (deletedSourceObjectId.HasValue)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == deletedSourceObjectId.Value);
			await SendPacketAsync(new SmDeleteItem(deletedSourceObjectId.Value, SmDeleteItem.UseDeleteType));
		}
		else if (sourceItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, sourceItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(
				sourceItemUpdate,
				sourceTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(sourceItemUpdate.ItemId, itemRestrictionCleanups)));
		}

		player.InventoryItems = inventoryItems.ToArray();
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 1));

		player.ItemExpands = plan.NewItemExpands;
		player.WarehouseBonusExpands = plan.NewWarehouseBonusExpands;
		switch (plan.Storage)
		{
			case InventoryExpansionStorage.Cube:
				await SendPacketAsync(SmSystemMessage.InventorySizeExtended(InventoryExpansionService.CubeSlotsPerExpansion));
				await SendPacketAsync(SmCubeUpdate.CubeSize(player));
				break;
			case InventoryExpansionStorage.Warehouse:
				await SendPacketAsync(SmSystemMessage.WarehouseSizeExtended(InventoryExpansionService.WarehouseSlotsPerExpansion));
				foreach (var packet in SmWarehouseInfo.CreateRegularWarehouseUpdatePackets(player, itemTemplates))
					await SendPacketAsync(packet);
				break;
		}
	}

	private async Task SendInventoryExpansionFailureAsync(InventoryExpansionFailure failure)
	{
		switch (failure)
		{
			case InventoryExpansionFailure.CubeCannotExpand:
				await SendPacketAsync(SmSystemMessage.InventoryCantExtendMore());
				break;
			case InventoryExpansionFailure.WarehouseCannotExpand:
				await SendPacketAsync(SmSystemMessage.WarehouseCantExtendMore());
				break;
		}
	}

	private async Task HandleDyeUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		int targetItemObjectId,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: model/templates/item/actions/DyeAction.canAct + dyeItem item branch.
		var targetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == targetItemObjectId);
		var targetTemplate = targetItem == null ? null : itemTemplates.GetItemTemplate(targetItem.ItemId);
		var targetSkinTemplate = targetItem == null
			? null
			: itemTemplates.GetItemTemplate(targetItem.ItemSkin == 0 ? targetItem.ItemId : targetItem.ItemSkin);
		var plan = DyeService.CreateItemDyePlan(targetItem, targetSkinTemplate, sourceTemplate.DyeAction, DateTimeOffset.UtcNow);
		if (!plan.Succeeded)
		{
			if (plan.Failure == DyeFailure.InvalidTarget)
				await SendPacketAsync(SmSystemMessage.ItemColorError());
			return;
		}

		if (targetTemplate == null)
			return;

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		var targetItemUpdate = CopyInventoryItem(
			targetItem!,
			color: plan.Color,
			setColor: true,
			colorExpires: plan.ColorExpires);
		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveDyeItemActionMutationAsync(
				player,
				targetItemUpdate,
				sourceItemUpdate,
				deletedSourceObjectId);
		if (!saved)
			return;

		if (deletedSourceObjectId.HasValue)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == deletedSourceObjectId.Value);
			await SendPacketAsync(new SmDeleteItem(deletedSourceObjectId.Value, SmDeleteItem.UseDeleteType));
		}
		else if (sourceItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, sourceItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(
				sourceItemUpdate,
				sourceTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(sourceItemUpdate.ItemId, itemRestrictionCleanups)));
		}

		ReplaceInventoryItem(inventoryItems, targetItemUpdate);
		player.InventoryItems = inventoryItems.ToArray();
		if (targetItemUpdate.Color == null)
		{
			await SendPacketAsync(SmSystemMessage.ItemColorRemoveSucceed(targetTemplate.GetClientName() ?? targetTemplate.Name));
		}
		else
		{
			await SendPacketAsync(SmSystemMessage.ItemColorChangeSucceed(
				targetTemplate.GetClientName() ?? targetTemplate.Name,
				sourceTemplate.GetClientName() ?? sourceTemplate.Name));
		}

		if (targetItemUpdate.IsEquipped)
		{
			var appearancePacket = new SmUpdatePlayerAppearance(player);
			if (_connectionRegistry != null)
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, appearancePacket, includeSourcePlayer: true);
			else
				await SendPacketAsync(appearancePacket);
		}

		await SendPacketAsync(new SmInventoryUpdateItem(
			targetItemUpdate,
			targetTemplate,
			SmInventoryUpdateItem.DecreaseItemUse,
			GetGeneralInfoWarehouseRestrictionFlag(targetItemUpdate.ItemId, itemRestrictionCleanups)));
	}

	private async Task HandleAnimationAddUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate)
	{
		// Java parity: model/templates/item/actions/AnimationAddAction.canAct + delayed act.
		if (sourceTemplate.AnimationAction?.MotionIds.Count == 0)
		{
			await SendPacketAsync(SmSystemMessage.ItemColorError());
			return;
		}

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		await CancelPendingItemUseAsync(player);
		await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 1000, 0, 0));
		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: sourceTemplate.GetClientName() ?? sourceTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.Item,
			delay: TimeSpan.FromMilliseconds(1000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteAnimationAddUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate);
			});
	}

	private async Task CompleteAnimationAddUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate)
	{
		var plan = MotionLearnService.CreatePlan(player, sourceTemplate.AnimationAction, DateTimeOffset.UtcNow);
		if (!plan.Succeeded)
			return;

		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveAnimationAddActionMutationAsync(
				player,
				plan.AddedMotions,
				plan.DeactivatedMotionIds,
				sourceItemUpdate,
				deletedSourceObjectId);
		if (!saved)
			return;

		if (deletedSourceObjectId.HasValue)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == deletedSourceObjectId.Value);
			await SendPacketAsync(new SmDeleteItem(deletedSourceObjectId.Value, SmDeleteItem.UseDeleteType));
		}
		else if (sourceItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, sourceItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(sourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}

		player.InventoryItems = inventoryItems.ToArray();
		player.Motions = plan.Motions;
		foreach (var motion in plan.AddedMotions)
			_expirableTaskService?.RegisterMotion(player, motion);
		var now = DateTimeOffset.UtcNow;
		foreach (var motion in plan.AddedMotions)
			await SendPacketAsync(new SmMotion(motion.Id, motion.SecondsUntilExpiration(now)));

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 0));
		if (_connectionRegistry != null)
		{
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(
				player.Position,
				player.ObjectId,
				new SmMotion(player.ObjectId, player.Motions),
				includeSourcePlayer: false);
		}
	}

	private async Task SendCraftLearnFailureAsync(CraftLearnValidation validation)
	{
		switch (validation.Failure)
		{
			case CraftLearnFailure.RecipeListFull:
				await SendPacketAsync(new SmMessage("You are unable to have more than 1600 recipes at the same time."));
				break;
			case CraftLearnFailure.MissingRecipe:
				await SendPacketAsync(SmSystemMessage.RecipeItemCannotUseNoRecipe());
				break;
			case CraftLearnFailure.InvalidRace:
				await SendPacketAsync(SmSystemMessage.CraftRecipeRaceCheck());
				break;
			case CraftLearnFailure.AlreadyKnown:
				await SendPacketAsync(SmSystemMessage.CraftRecipeLearnedAlready());
				break;
			case CraftLearnFailure.MissingSkill:
				await SendPacketAsync(SmSystemMessage.CraftRecipeCantLearnSkill(validation.SkillName));
				break;
			case CraftLearnFailure.SkillPointTooLow:
				await SendPacketAsync(SmSystemMessage.CraftRecipeCantLearnSkillPoint());
				break;
		}
	}

	private async Task HandleRideUseItemAsync(
		Player player,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/RideAction.canAct + act.
		if (player.IsInRideMode)
		{
			AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
			await DismountRideAsync(player);
			return;
		}

		var restriction = PlayerRideRestrictionService.ValidateStartRide(
			player,
			_options.Custom.EnableRideRestriction,
			staticData.WorldMaps,
			_runtimeContext?.WorldMapStates);
		if (!restriction.CanRide)
		{
			await SendPacketAsync(SmSystemMessage.CannotRideInvalidLocation());
			return;
		}

		var rideInfo = staticData.RideInfos.GetRideInfo(sourceTemplate.RideNpcId);
		if (rideInfo == null)
			return;

		if (player.IsInState(PlayerCreatureState.Resting))
		{
			await SendPacketAsync(SmSystemMessage.CannotRide(ChatUtil.L10n(1400057)));
			return;
		}

		if (player.IsInAnyAbnormalState(PlayerAbnormalState.DismountRide))
		{
			await SendPacketAsync(SmSystemMessage.CannotRideAbnormalState());
			return;
		}

		var removeCooldownDelayIdOnCancel = AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: true);
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 3000, 0, 0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: sourceTemplate.GetClientName() ?? sourceTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.Item,
			delay: TimeSpan.FromMilliseconds(3000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteRideUseItemAsync(player, sourceItem, rideInfo);
			},
			cancelEndState: 3,
			removeCooldownDelayIdOnCancel: removeCooldownDelayIdOnCancel,
			preserveOnEmotion: true);
	}

	private async Task CompleteRideUseItemAsync(Player player, InventoryItem sourceItem, RideInfoSummary rideInfo)
	{
		// Java parity: RideAction delayed TaskId.ITEM_USE completion. QuestEngine.rideAction remains future quest-engine work.
		player.MountRide(ToPlayerRideInfo(rideInfo));
		await BroadcastEmotionAsync(player, new SmEmotion(player, EmotionType.ChangeSpeed, 0, 0));
		await BroadcastEmotionAsync(player, new SmEmotion(player, EmotionType.Ride, 0, rideInfo.NpcId));
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 1));
	}

	private async Task HandleToyPetSpawnUseItemAsync(
		Player player,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/ToyPetSpawnAction.canAct guard order.
		var restriction = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			player,
			_options.Custom.EnableKiskRestriction,
			_runtimeContext?.Kisks.HaveKisk(player.ObjectId) ?? false,
			staticData.WorldMaps,
			_runtimeContext?.WorldMapStates);

		switch (restriction.Status)
		{
			case PlayerKiskSpawnRestrictionStatus.Flying:
				await SendPacketAsync(SmSystemMessage.CannotUseBindstoneItemWhileFlying());
				break;
			case PlayerKiskSpawnRestrictionStatus.Instance:
				await SendPacketAsync(SmSystemMessage.CannotRegisterBindstoneFarFromNpc());
				break;
			case PlayerKiskSpawnRestrictionStatus.AlreadyInstalled:
				await SendPacketAsync(SmSystemMessage.BindstoneAlreadyInstalled());
				break;
			case PlayerKiskSpawnRestrictionStatus.InvalidLocation:
				await SendPacketAsync(SmSystemMessage.CannotUseItemInvalidLocation());
				break;
		}

		if (!restriction.CanSpawn)
			return;

		var kiskTemplate = staticData.NpcTemplates.GetNpcTemplate(sourceTemplate.ToyPetSpawnNpcId);
		if (kiskTemplate == null)
			return;

		var removeCooldownDelayIdOnCancel = AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: true);
		await CancelPendingItemUseAsync(player);
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 10000, 0, 0));
		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: sourceTemplate.GetClientName() ?? sourceTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.Item,
			delay: TimeSpan.FromMilliseconds(10000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteToyPetSpawnUseItemAsync(player, sourceItem.ObjectId, sourceTemplate, kiskTemplate, cancellationToken);
			},
			cancelEndState: 2,
			removeCooldownDelayIdOnCancel: removeCooldownDelayIdOnCancel);
	}

	internal async Task CompleteToyPetSpawnUseItemAsync(
		Player player,
		int sourceItemObjectId,
		ItemTemplateSummary sourceTemplate,
		NpcTemplateSummary kiskTemplate,
		CancellationToken cancellationToken)
	{
		// Java parity: ToyPetSpawnAction.act delayed task -> decreaseByObjectId, spawnKisk, KiskService.regKisk.
		if (_idFactory == null)
		{
			_logger.LogWarning("Cannot spawn kisk for player {PlayerObjectId}; IDFactory is unavailable", player.ObjectId);
			return;
		}

		var inventoryItems = player.InventoryItems.ToList();
		var sourceItem = inventoryItems.FirstOrDefault(item =>
			item.ObjectId == sourceItemObjectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
		if (sourceItem == null)
		{
			// Java parity: ToyPetSpawnAction sends the success end animation before decreaseByObjectId can fail.
			await BroadcastItemUsageAnimationAsync(
				player,
				new SmItemUsageAnimation(player.ObjectId, sourceItemObjectId, sourceTemplate.TemplateId, 0, 1, 1));
			return;
		}

		// Java parity: ToyPetSpawnAction's delayed task broadcasts the success end animation before
		// inventory.decreaseByObjectId, spawnKisk, and KiskService.regKisk side effects.
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 1));

		var kiskObjectId = _idFactory.NextId();
		var plan = PlayerKiskSpawnService.CreatePlan(player, sourceItem, kiskTemplate, kiskObjectId);
		var addedToWorld = false;
		if (_world != null)
		{
			addedToWorld = _world.TryAddObject(plan.Kisk.ObjectId, plan.Kisk);
			if (!addedToWorld)
			{
				_idFactory.ReleaseId(kiskObjectId);
				return;
			}

			RevalidateKiskCreaturePvpZones(plan.Kisk);
		}

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveItemUseSourceMutationAsync(
				player,
				plan.SourceItemUpdate,
				plan.DeletedSourceItemObjectId,
				cancellationToken);
		if (!saved)
		{
			if (addedToWorld && _world?.TryRemoveObject(plan.Kisk.ObjectId, out _) == true)
				ClearKiskCreaturePvpZones(plan.Kisk.ObjectId);
			_idFactory.ReleaseId(kiskObjectId);
			return;
		}

		if (plan.DeletedSourceItemObjectId.HasValue)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == plan.DeletedSourceItemObjectId.Value);
			await SendPacketAsync(new SmDeleteItem(plan.DeletedSourceItemObjectId.Value, SmDeleteItem.UseDeleteType));
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(inventoryItems.Count, player.NpcExpands, player.QuestExpands, player.ItemExpands));
		}
		else if (plan.SourceItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, plan.SourceItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(plan.SourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}

		player.InventoryItems = inventoryItems.ToArray();
		_runtimeContext?.Kisks.RegisterKisk(plan.RuntimeState);
		if (_connectionRegistry != null)
			await _connectionRegistry.RefreshNpcVisibilityAsync([plan.Kisk]);
		else
			await SendPacketAsync(new SmNpcInfo(plan.Kisk));

		ScheduleKiskLifetimeDespawn(plan.RuntimeState);
		await RequestOrBindPlayerToKiskAsync(player, plan.RuntimeState);
	}

	private void RevalidateKiskCreaturePvpZones(WorldNpc kisk)
	{
		// Java parity: ToyPetSpawnAction.spawnKisk -> VisibleObjectSpawner.spawnKisk -> World.spawn -> MapRegion.revalidateZones.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		CreaturePvpZoneRevalidationService.Revalidate(
			kisk.ObjectId,
			kisk.Position,
			staticData?.CreaturePvpZones,
			_creaturePvpZoneCounterService);
	}

	private void ClearKiskCreaturePvpZones(int objectId)
	{
		// Java parity: failed kisk spawn cleanup despawns the kisk NPC and leaves its zone memberships.
		_creaturePvpZoneCounterService?.ClearCounters(objectId);
	}

	private void ScheduleKiskLifetimeDespawn(PlayerKiskRuntimeState kisk)
	{
		// Java parity: model/gameobjects/Kisk schedules KiskLifeTask for the remaining lifetime.
		if (_threadPoolManager == null)
			return;

		var delay = TimeSpan.FromSeconds(kisk.GetRemainingLifetimeSeconds(DateTimeOffset.UtcNow));
		var task = _threadPoolManager.Schedule(
			cancellationToken => RunKiskLifetimeDespawnAsync(kisk.ObjectId, cancellationToken),
			delay);
		kisk.SetDespawnTask(task);
	}

	private async ValueTask RunKiskLifetimeDespawnAsync(int kiskObjectId, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested || _world == null || _runtimeContext == null)
			return;

		await RemoveRuntimeKiskAsync(kiskObjectId, cancelScheduledDespawnTask: false);
	}

	internal async Task RemoveRuntimeKiskAsync(int kiskObjectId, bool cancelScheduledDespawnTask = true)
	{
		if (_world == null || _runtimeContext == null)
			return;

		var result = PlayerKiskLifetimeService.DespawnExpiredKisk(
			_world,
			_runtimeContext.Kisks,
			_idFactory,
			kiskObjectId,
			cancelScheduledDespawnTask);
		if (!result.RemovedRegistry)
			return;

		await PlayerKiskRemovalRuntimeCleanupService.ApplyAsync(
			result,
			_connectionRegistry,
			_runtimeContext,
			_world,
			pvpZoneCounterService: _creaturePvpZoneCounterService);
	}

	internal async Task HandleReviveAsync(Player player, CmRevive packet)
	{
		// Java parity: network/aion/clientpackets/CM_REVIVE.runImpl routes ReviveType.KISK_REVIVE to PlayerReviveService.kiskRevive.
		if (packet.ReviveId != PlayerKiskReviveService.KiskReviveId)
			return;

		var result = PlayerKiskReviveService.TryUseKiskRevive(
			player,
			_runtimeContext?.Kisks,
			kiskObjectId => TryGetKiskPosition(kiskObjectId, out var position) ? position : null);
		if (!result.UsedKisk || result.Kisk == null || result.KiskPosition == null)
			return;

		await SendPacketAsync(new SmKiskUpdate(result.Kisk));
		await BroadcastKiskUpdateAsync(result.Kisk, result.KiskPosition.Value, excludedPlayerObjectId: player.ObjectId);
		if (result.ShouldDeleteKisk)
			await RemoveRuntimeKiskAsync(result.Kisk.ObjectId);

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var resourceMaxStats = SmStatsInfo.CalculateCurrentResourceMaxStats(
			player,
			staticData?.PlayerExperienceTable,
			staticData?.ItemTemplates,
			staticData?.ItemRandomBonuses,
			staticData?.ItemSets,
			staticData?.EnchantTemplates,
			staticData?.TemperingTemplates,
			staticData?.SkillTemplates,
			staticData?.TitleTemplates);
		ClearReviveTargets(player);
		PlayerReviveRestoreService.ApplyKiskReviveRestore(
			player,
			resourceMaxStats.MaxHp,
			resourceMaxStats.MaxMp,
			player.HasNoResurrectPenaltyEffect);
		new PlayerReviveCleanupAdapterService().Apply(new PlayerReviveCleanupAdapterRequest(
			player.ObjectId,
			player.AggroList.Entries,
			ExecuteLiveAggroMutation: true,
			player.AggroList));
		await SendReviveMovementUpdatesAsync(player);
		await BroadcastEmotionAsync(player, new SmEmotion(player, EmotionType.Resurrect));
		await UpdatePlayerStatsAndSpeedVisuallyAsync(player);
		player.ClearResurrectionPositionState();
		await TeleportPlayerToKiskPositionAsync(player, result.KiskPosition.Value, staticData);

		// Full Java PlayerReviveService.revive + TeleportService.teleportTo side effects remain queued:
		// soul-sickness handling, flying-before-death state restoration, full world despawn/spawn
		// ownership, protection tasks, instance/legion leave callbacks, and exact socket ordering need the broader revive/teleport model.
	}

	private async Task SendReviveMovementUpdatesAsync(Player player, CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerReviveService.revive calls PlayerGroupService.updateGroup(..., MOVEMENT)
		// and PlayerAllianceService.updateAlliance(..., MOVEMENT) before broadcasting SM_EMOTION RESURRECT.
		var groupPlan = new PlayerGroupMovementUpdatePlanner(_playerGroupRuntime)
			.CreateReviveMovementUpdatePlan(player)
			.MemberInfoUpdatePlan;
		if (groupPlan != null)
		{
			foreach (var intent in groupPlan.MemberInfoIntents)
			{
				var packet = intent.CreatePacket();
				if (packet != null)
					await SendTeamMovementPacketAsync(intent.RecipientObjectId, packet, cancellationToken);
			}
		}

		var alliance = _playerAllianceRuntime.Resolve(player);
		if (alliance == null)
			return;

		var members = _playerAllianceRuntime.GetMemberObjectIds(alliance.AllianceId)
			.Select(memberObjectId => _playerAllianceRuntime.GetMember(alliance.AllianceId, memberObjectId)?.Player)
			.Where(member => member != null)
			.Cast<Player>()
			.ToArray();
		var alliancePlan = new PlayerAllianceMovementUpdatePlanner()
			.CreateReviveMovementUpdatePlan(alliance.AllianceId, members, player);
		if (alliancePlan == null)
			return;

		foreach (var intent in alliancePlan.MemberInfoIntents)
		{
			var packet = intent.CreatePacket();
			if (packet != null)
				await SendTeamMovementPacketAsync(intent.RecipientObjectId, packet, cancellationToken);
		}
	}

	private async Task SendTeamMovementPacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	internal async Task<PlayerTeleportResult> TeleportPlayerToKiskPositionAsync(
		Player player,
		WorldPosition destination,
		StaticData? staticData = null)
	{
		staticData ??= _runtimeContext?.DataManager?.StaticData;
		var teleport = PlayerTeleportService.TeleportToKiskPosition(player, destination);
		// Java parity: PlayerReviveService.kiskRevive -> TeleportService.teleportTo(kisk.getPosition);
		// same-map TeleportService.spawnOnSameMap calls PlayerController.updateZone after World.setPosition.
		RevalidatePlayerCreaturePvpZones(player, staticData);
		await SendKiskReviveTeleportPacketsAsync(player, teleport, staticData);
		return teleport;
	}

	internal async Task<PlayerTeleportResult?> HandleTeleportAnimationDoneAsync(Player player)
	{
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (ShouldFallbackDelayedTeleportToCurrentSpawn(player, _runtimeContext?.WorldMapStates))
		{
			var pendingTeleport = PlayerTeleportService.CancelPendingTeleport(player);
			if (pendingTeleport == null)
				return null;

			// Java parity: TeleportService.SpawnTask.run delayed fallback sends SM_PLAYER_INFO and World.spawn(player) without moving.
			await SendPacketAsync(new SmPlayerInfo(player, staticData?.PlayerExperienceTable));
			return null;
		}

		var queuedTeleport = player.PendingTeleport;
		if (queuedTeleport != null
			&& (player.Position.WorldId != queuedTeleport.Destination.WorldId
				|| player.Position.InstanceId != queuedTeleport.Destination.InstanceId))
			await SendInstanceLeaveMessageIfNeededAsync(player, player.Position);

		// Java parity: CM_TELEPORT_ANIMATION_DONE.runImpl executes TeleportService.SpawnTask.run; SpawnTask mutates World.setPosition before spawn packets and zone callbacks.
		var teleport = PlayerTeleportService.CompletePendingTeleport(player);
		if (teleport == null)
			return null;

		RevalidatePlayerCreaturePvpZones(player, staticData);
		await SendDelayedTeleportCompletionPacketsAsync(player, teleport, staticData);
		return teleport;
	}

	private async Task SendInstanceLeaveMessageIfNeededAsync(Player player, WorldPosition previousPosition)
	{
		var worldMapStates = _runtimeContext?.WorldMapStates;
		if (worldMapStates == null
			|| !worldMapStates.TryGetWorldMapInstance(previousPosition.WorldId, previousPosition.InstanceId, out var instance)
			|| instance == null)
			return;

		// Java parity: InstanceService.onLeaveInstance invokes the instance handler before reset-warning message selection.
		instance.InstanceHandler.OnLeaveInstance(player);

		var plan = InstanceLeaveMessageService.CreateLeaveMessagePlan(
			instance,
			_options.Instance.SoloDestroyDelaySeconds,
			_options.Instance.DestroyDelaySeconds,
			registeredTeamHasNoMembers: InstanceRegisteredTeamDisbandService.IsRegisteredTeamDisbanded(
				instance,
				_playerGroupRuntime,
				_playerAllianceRuntime));
		if (plan.Packet != null)
			await SendPacketAsync(plan.Packet);

		// Java parity: InstanceService.onLeaveInstance invokes AutoGroupService after reset-warning packet selection.
		var autoGroupLeave = _autoGroupInstanceLeaveRuntimeService.OnLeaveInstance(
			player,
			previousPosition.WorldId,
			previousPosition.InstanceId,
			onlinePlayersInsideAfterLeave: Math.Max(0, instance.PlayerCount - 1));
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var autoGroups = staticData?.AutoGroups;
		if (_connectionRegistry != null
			&& autoGroupLeave.Plan.WouldCheckQuickEntries
			&& autoGroupLeave.SnapshotAfterLeave?.InstanceMaskId is { } instanceMaskId)
		{
			var refill = _autoGroupLookingPartyRegistrations.TryRefillQueuedQuickEntry(
				instanceMaskId,
				autoGroups,
				staticData?.InstanceCooltimes,
				request => _autoGroupInstanceLeaveRuntimeService.TryAddOpenQuickEntry(request));
			if (refill != null)
			{
				foreach (var delivery in refill.WindowDeliveries)
				{
					var deliveryAutoGroup = autoGroups?.GetTemplateByInstanceMaskId(delivery.MaskId);
					if (deliveryAutoGroup != null)
						await _connectionRegistry.SendPacketToPlayerAsync(delivery.PlayerObjectId, new SmAutoGroup(deliveryAutoGroup, delivery.WindowId));
				}

				ScheduleAutoGroupPenaltyRefreshes(refill.PenaltyRefreshIntents);
			}
		}

		foreach (var packet in autoGroupLeave.OpenRegistrationPackets)
			await SendPacketAsync(packet);
	}

	private static bool ShouldFallbackDelayedTeleportToCurrentSpawn(Player player, WorldMapRuntimeStateTable? worldMapStates)
	{
		var pendingTeleport = player.PendingTeleport;
		if (pendingTeleport == null)
			return false;

		// Java parity: TeleportService.SpawnTask.run checks player.isDead() before applying delayed teleport position.
		if (player.IsInState(PlayerCreatureState.Dead) || player.LifeStats?.CurrentHp <= 0)
			return true;

		// Java parity: SpawnTask.run falls back if InstanceService.instanceExists(worldId, instanceId) is false.
		return worldMapStates?.InstanceExists(pendingTeleport.Destination.WorldId, pendingTeleport.Destination.InstanceId) == false;
	}

	internal async Task<PendingTeleportRequestResult> QueueDelayedTeleportAsync(
		Player player,
		WorldPosition destination,
		TeleportAnimation? animation = null,
		StaticData? staticData = null)
	{
		staticData ??= _runtimeContext?.DataManager?.StaticData;
		animation ??= TeleportAnimation.FadeOutBeam;
		var selectedAnimation = animation.Value;
		var pendingTeleport = PlayerTeleportService.QueuePendingTeleport(player, destination, selectedAnimation);
		var packet = new SmTeleportLoc(
			pendingTeleport.Destination,
			selectedAnimation,
			staticData?.WorldMaps ?? Array.Empty<WorldMapSummary>());
		// Java parity: TeleportService.sendLoc calls World.despawn(player, animation.getDefaultObjectDeleteAnimation()) before SM_TELEPORT_LOC.
		if (_connectionRegistry != null)
		{
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(
				player.Position,
				player.ObjectId,
				new SmDelete(player.ObjectId, selectedAnimation.DefaultObjectDeleteAnimation));
		}

		// Java parity: TeleportService.sendLoc queues SpawnTask under TaskId.TELEPORT, then sends SM_TELEPORT_LOC; position changes after CM_TELEPORT_ANIMATION_DONE.
		await SendPacketAsync(packet);
		return new PendingTeleportRequestResult(pendingTeleport, packet);
	}

	internal async Task<PlayerTeleportResult?> TeleportSameInstancePortalAsync(
		Player player,
		PortalLocSummary portalLoc,
		StaticData? staticData = null,
		CancellationToken cancellationToken = default)
	{
		staticData ??= _runtimeContext?.DataManager?.StaticData;
		var destination = new WorldPosition(
			portalLoc.WorldId,
			portalLoc.X,
			portalLoc.Y,
			portalLoc.Z,
			portalLoc.Heading,
			player.Position.InstanceId);
		if (player.Position.WorldId != destination.WorldId || player.Position.InstanceId != destination.InstanceId)
			return null;

		// Java parity: PortalService.port same-map branch calls TeleportService.teleportTo with TeleportAnimation.NONE.
		if (_connectionRegistry != null)
		{
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(
				player.Position,
				player.ObjectId,
				new SmDelete(player.ObjectId, TeleportAnimation.None.DefaultObjectDeleteAnimation));
		}

		var teleport = PlayerTeleportService.TeleportWithinSameInstance(player, destination);
		RevalidatePlayerCreaturePvpZones(player, staticData);
		await SendDelayedTeleportCompletionPacketsAsync(player, teleport, staticData);
		return teleport;
	}

	internal async Task<PortalContinueTransferResult?> QueuePortalContinueTransferAsync(
		Player player,
		PortalEntryPreparationResult preparation,
		StaticData? staticData = null,
		WorldMapRuntimeStateTable? worldMapStates = null,
		InstanceCooltimeTable? instanceCooltimes = null,
		DateTimeOffset? now = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/teleport/PortalService.port maxPlayers 0/1 continuation after checkAndRemoveRequiredItems.
		staticData ??= _runtimeContext?.DataManager?.StaticData;
		worldMapStates ??= _runtimeContext?.WorldMapStates;
		instanceCooltimes ??= staticData?.InstanceCooltimes;
		var portalLoc = preparation.EntryPlan.PortalLoc;
		if (portalLoc == null || instanceCooltimes == null || worldMapStates == null)
			return null;
		if (preparation.EntryPlan.TeamPlan != null)
			return await QueuePortalTeamContinueTransferAsync(
				player,
				preparation.EntryPlan.TeamPlan,
				portalLoc,
				staticData,
				worldMapStates,
				instanceCooltimes,
				now ?? DateTimeOffset.Now);
		if (preparation.EntryPlan.Action != PortalEntryPlanAction.Continue)
			return null;

		var targetMap = worldMapStates.GetMap(portalLoc.WorldId);
		var portalLocation = new WorldPosition(
			portalLoc.WorldId,
			portalLoc.X,
			portalLoc.Y,
			portalLoc.Z,
			portalLoc.Heading);
		var maxPlayers = instanceCooltimes.GetMaxMemberCount(portalLoc.WorldId, player.Race);
		var registeredInstance = preparation.EntryPlan.RegisteredInstance;

		if (registeredInstance != null && portalLoc.WorldId != player.Position.WorldId)
		{
			// Java parity: PortalService.transfer reuses the registered solo instance and applies cooldown after sendLoc.
			registeredInstance.SetStartPositionIfMissing(portalLocation with { InstanceId = registeredInstance.InstanceId });
			registeredInstance.Register(player.ObjectId);
			var transfer = await QueueInstancePortalTransferAsync(
				player,
				portalLocation with { InstanceId = registeredInstance.InstanceId },
				preparation.EntryPlan.Reenter,
				instanceCooltimes,
				TeleportAnimation.FadeOutBeam,
				staticData,
				now);
			return PortalContinueTransferResult.FromRegisteredInstance(transfer, registeredInstance);
		}

		if (targetMap?.Summary.IsInstance == true)
		{
			var transfer = await QueueAllocatedInstancePortalTransferAsync(
				player,
				portalLocation,
				preparation.EntryPlan.Reenter,
				worldMapStates,
				instanceCooltimes,
				ownerId: IsPersonalWorld(portalLoc.WorldId) ? player.ObjectId : 0,
				maxPlayers: maxPlayers,
				animation: TeleportAnimation.FadeOutBeam,
				staticData: staticData,
				now: now,
				difficultyId: preparation.EntryPlan.DifficultyId);
			return PortalContinueTransferResult.AllocatedInstance(transfer);
		}

		var teleport = await QueueDelayedTeleportAsync(
			player,
			portalLocation,
			TeleportAnimation.FadeOutBeam,
			staticData);
		return PortalContinueTransferResult.OpenWorld(teleport);
	}

	private async Task<PortalContinueTransferResult> QueuePortalTeamContinueTransferAsync(
		Player player,
		PortalTeamEntryPlan teamPlan,
		PortalLocSummary portalLoc,
		StaticData? staticData,
		WorldMapRuntimeStateTable worldMapStates,
		InstanceCooltimeTable instanceCooltimes,
		DateTimeOffset now)
	{
		var groupPlan = GroupPortalTransferPlan.FromTeamPlan(teamPlan, portalLoc, player, instanceCooltimes, _options, now);
		if (teamPlan.Kind is not (PortalTeamEntryKind.Group
			or PortalTeamEntryKind.Alliance
			or PortalTeamEntryKind.League
			or PortalTeamEntryKind.PlayerObject) || groupPlan == null)
		{
			return PortalContinueTransferResult.UnsupportedTeamPortal(
				teamPlan,
				portalLoc,
				player,
				instanceCooltimes,
				_options,
				now);
		}

		var registeredInstance = teamPlan.RegisteredInstance;
		var transferTeamPlan = teamPlan;
		var transferGroupPlan = groupPlan;
		if (registeredInstance == null
			&& teamPlan.Disposition == PortalTeamEntryDisposition.FreshInstanceAllocationNeeded
			&& teamPlan.TeamId > 0)
		{
			// Java parity: PortalService.port group branch calls InstanceService.getNextAvailableInstance(mapId, difficult, maxPlayers),
			// then, for actual groups, WorldMapInstance.registerTeam(group), before falling through to the same transfer helper.
			// The !instanceGroupReq/no-group branch allocates through the same path but does not register a team id.
			registeredInstance = worldMapStates.CreateNextWorldMapInstance(
				portalLoc.WorldId,
				maxPlayers: teamPlan.MaxPlayers,
				difficultyId: teamPlan.DifficultyId);
			if (registeredInstance == null)
			{
				return PortalContinueTransferResult.UnsupportedTeamPortal(
					teamPlan,
					portalLoc,
					player,
					instanceCooltimes,
					_options,
					now);
			}

			if (teamPlan.Kind is PortalTeamEntryKind.Group or PortalTeamEntryKind.Alliance or PortalTeamEntryKind.League)
				registeredInstance.RegisterTeamId(teamPlan.TeamId);
			if (staticData != null && _worldNpcSpawnService != null)
				_worldNpcSpawnService.SpawnWorldNpcsForInstance(
					registeredInstance,
					portalLoc.WorldId,
					staticData.NpcSpawns,
					staticData.NpcTemplates,
					staticData.StaticDoors,
					staticData.ItemTemplates);
			registeredInstance.NotifyInstanceCreated();
			transferTeamPlan = teamPlan with
			{
				Disposition = PortalTeamEntryDisposition.RegisteredInstanceTransfer,
				RegisteredInstance = registeredInstance,
			};
			transferGroupPlan = GroupPortalTransferPlan.FromTeamPlan(
				transferTeamPlan,
				portalLoc,
				player,
				instanceCooltimes,
				_options,
				now);
		}

		if (registeredInstance == null
			|| transferGroupPlan?.CapacityPlan.State != GroupPortalCapacityState.WouldPassCapacityGuard)
		{
			return PortalContinueTransferResult.UnsupportedTeamPortal(
				transferTeamPlan,
				portalLoc,
				player,
				instanceCooltimes,
				_options,
				now);
		}

		var destination = new WorldPosition(
			portalLoc.WorldId,
			portalLoc.X,
			portalLoc.Y,
			portalLoc.Z,
			portalLoc.Heading,
			registeredInstance.InstanceId);
		registeredInstance.SetStartPositionIfMissing(destination);
		registeredInstance.Register(player.ObjectId);
		var transfer = await QueueInstancePortalTransferAsync(
			player,
			destination,
			transferTeamPlan.Reenter,
			instanceCooltimes,
			TeleportAnimation.FadeOutBeam,
			staticData,
			now);
		return PortalContinueTransferResult.FromRegisteredTeamInstance(
			transfer,
			registeredInstance,
			transferTeamPlan,
			transferGroupPlan);
	}

	internal async Task<InstanceEntranceCooldownResult> ApplyInstanceEntranceCooldownAsync(
		Player player,
		int worldId,
		bool reenter,
		InstanceCooltimeTable instanceCooltimes,
		DateTimeOffset? now = null)
	{
		var effectiveNow = now ?? DateTimeOffset.Now;
		var result = InstanceEntranceCooldownService.ApplyEntranceCooldown(
			player,
			worldId,
			reenter,
			instanceCooltimes,
			_options,
			effectiveNow);
		var packet = InstanceEntranceCooldownService.CreateEntryInfoPacket(
			result,
			player,
			instanceCooltimes,
			() => effectiveNow);
		if (result.Added && _playerEnterWorldService != null)
			await _playerEnterWorldService.SavePortalCooldownsAsync(player, effectiveNow.ToUnixTimeMilliseconds());
		if (packet != null)
		{
			// Java parity: PortalCooldownList.addPortalCooldown -> sendEntryInfo owner-only branch via PacketSendUtility.sendPacket.
			await SendPacketAsync(packet);
		}

		return result;
	}

	internal async Task<InstancePortalTransferResult> QueueInstancePortalTransferAsync(
		Player player,
		WorldPosition destination,
		bool reenter,
		InstanceCooltimeTable instanceCooltimes,
		TeleportAnimation? animation = null,
		StaticData? staticData = null,
		DateTimeOffset? now = null)
	{
		// Java parity: services/teleport/PortalService.transfer calls TeleportService.teleportTo before adding portal cooldown.
		var teleport = await QueueDelayedTeleportAsync(player, destination, animation, staticData);
		var cooldown = await ApplyInstanceEntranceCooldownAsync(
			player,
			destination.WorldId,
			reenter,
			instanceCooltimes,
			now);
		return new InstancePortalTransferResult(teleport, cooldown);
	}

	internal async Task<AllocatedInstancePortalTransferResult> QueueAllocatedInstancePortalTransferAsync(
		Player player,
		WorldPosition portalLocation,
		bool reenter,
		WorldMapRuntimeStateTable worldMapStates,
		InstanceCooltimeTable instanceCooltimes,
		int ownerId = 0,
		int maxPlayers = 0,
		TeleportAnimation? animation = null,
		StaticData? staticData = null,
		DateTimeOffset? now = null,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null)
	{
		// Java parity: PortalService.port allocates/registers the instance before PortalService.transfer teleports and adds cooldown.
		Action<int, WorldMapInstanceRuntimeState>? emptyInstanceScheduler = null;
		if (_emptyInstanceCheckerService != null)
		{
			emptyInstanceScheduler = (worldId, instance) =>
			{
				var delayPlan = InstanceServiceFormulaService.CreateDestroyDelayPlan(
					instance.MaxPlayers,
					_options.Instance.SoloDestroyDelaySeconds,
					_options.Instance.DestroyDelaySeconds);
				_emptyInstanceCheckerService.Schedule(
					worldId,
					instance,
					TimeSpan.FromSeconds(delayPlan.DestroyDelaySeconds),
					registeredTeamDisbanded: registeredInstance => InstanceRegisteredTeamDisbandService.IsRegisteredTeamDisbanded(
						registeredInstance,
						_playerGroupRuntime,
						_playerAllianceRuntime));
			};
		}

		var runtimePlan = InstanceRuntimeService.CreatePortalTransferInstance(
			worldMapStates,
			player,
			portalLocation,
			ownerId,
			maxPlayers,
			difficultyId,
			instanceHandler,
			emptyInstanceScheduler: emptyInstanceScheduler);
		if (staticData != null && _worldNpcSpawnService != null)
		{
			// Java parity: InstanceService.getNextAvailableInstance spawns instance objects before PortalService.transfer teleports.
			_worldNpcSpawnService.SpawnWorldNpcsForInstance(
				runtimePlan.Instance,
				portalLocation.WorldId,
				staticData.NpcSpawns,
				staticData.NpcTemplates,
				staticData.StaticDoors,
				staticData.ItemTemplates);
		}
		runtimePlan.Instance.NotifyInstanceCreated();

		var transfer = await QueueInstancePortalTransferAsync(
			player,
			runtimePlan.Destination,
			reenter,
			instanceCooltimes,
			animation,
			staticData,
			now);
		return new AllocatedInstancePortalTransferResult(runtimePlan, transfer);
	}

	private void ClearReviveTargets(Player player)
	{
		if (_connectionRegistry == null)
			return;

		var revivePosition = player.Position;
		var onlinePlayers = new List<Player>();
		_connectionRegistry.ForEachOnlinePlayer(onlinePlayers.Add);
		PlayerReviveTargetCleanupService.ClearKnownPlayerTargets(
			player,
			onlinePlayers,
			(candidate, _) => WorldVisibility.IsVisibleTo(candidate, revivePosition));
	}

	private async Task SendKiskReviveTeleportPacketsAsync(Player player, PlayerTeleportResult teleport, StaticData? staticData)
	{
		// Java parity: TeleportService.teleportTo(kisk position) uses TeleportAnimation.NONE; no SM_TELEPORT_LOC fade-out request is sent.
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(teleport.PreviousPosition, player.ObjectId, new SmDelete(player.ObjectId));

		await SendPacketAsync(new SmChannelInfo(player.Position, staticData?.WorldMaps ?? Array.Empty<WorldMapSummary>()));
		await SendPacketAsync(new SmPlayerSpawn(player));

		if (teleport.UsesSameWorldSpawnPath)
		{
			// Java parity: TeleportService.spawnOnSameMap sends player info, stats, and active motion after SM_PLAYER_SPAWN.
			await SendPacketAsync(new SmPlayerInfo(player, staticData?.PlayerExperienceTable));
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
			await SendPacketAsync(new SmMotion(player.ObjectId, player.Motions));
		}

		if (_connectionRegistry == null)
			return;

		await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			player.Position,
			player.ObjectId,
			new SmPlayerInfo(player, staticData?.PlayerExperienceTable));
		await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			player.Position,
			player.ObjectId,
			new SmMotion(player.ObjectId, player.Motions));
		await RefreshHousingVisibilityForPlayerAsync(player);
		await RefreshNpcVisibilityForPlayerAsync(player);
	}

	private async Task SendDelayedTeleportCompletionPacketsAsync(Player player, PlayerTeleportResult teleport, StaticData? staticData)
	{
		if (teleport.UsesSameWorldSpawnPath)
		{
			// Java parity: TeleportService.SpawnTask.run same-world+instance branch delegates to spawnOnSameMap.
			await SendPacketAsync(new SmChannelInfo(player.Position, staticData?.WorldMaps ?? Array.Empty<WorldMapSummary>()));
			await SendPacketAsync(new SmPlayerInfo(player, staticData?.PlayerExperienceTable));
			player.PortAnimation = ArrivalAnimation.None;
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
			await SendPacketAsync(new SmMotion(player.ObjectId, player.Motions));
			return;
		}

		// Java parity: TeleportService.SpawnTask.run map/instance-change branch sends channel info and player spawn, then CM_LEVEL_READY completes full map load.
		await SendPacketAsync(new SmChannelInfo(player.Position, staticData?.WorldMaps ?? Array.Empty<WorldMapSummary>()));
		await SendPacketAsync(new SmPlayerSpawn(player));
		if (ShouldSendInstanceDungeonOpenedForSelf(player.Position, staticData?.WorldMaps))
			await SendPacketAsync(SmSystemMessage.InstanceDungeonOpenedForSelf(player.Position.WorldId));
	}

	private static bool ShouldSendInstanceDungeonOpenedForSelf(WorldPosition position, IReadOnlyList<WorldMapSummary>? worldMaps)
	{
		// Java parity: TeleportService.SpawnTask.run sends STR_MSG_INSTANCE_DUNGEON_OPENED_FOR_SELF
		// when WORLD_MAPS_DATA marks the destination as instance and WorldMapType.getWorld(worldId).isPersonal() is false.
		var worldMap = worldMaps?.FirstOrDefault(map => map.MapId == position.WorldId);
		return worldMap is { IsInstance: true } && !IsPersonalWorld(position.WorldId);
	}

	private static bool IsPersonalWorld(int worldId)
	{
		// Java parity: WorldMapType personal flag is true only for the housing/legion personal worlds.
		return worldId is 700020000 or 710020000 or 720010000 or 730010000;
	}

	private async Task RequestOrBindPlayerToKiskAsync(Player player, PlayerKiskRuntimeState kisk)
	{
		// Java parity: ToyPetSpawnAction.act -> kisk.getController().onDialogRequest(player) or KiskService.onBind.
		if (kisk.MaxMembers > 1)
		{
			var pendingRequest = new PendingKiskBindRequest(kisk.ObjectId, SmQuestionWindow.RegisterBindstone);
			// Java parity: AIActions.addRequest stores the bindstone RequestResponseHandler
			// in Player.getResponseRequester().putRequest before sending SM_QUESTION_WINDOW.
			if (!player.ResponseRequester.PutRequest(
				SmQuestionWindow.RegisterBindstone,
				new QuestionResponseRequest(kisk.ObjectId, QuestionResponseRequestKind.KiskBind, pendingRequest)))
			{
				return;
			}

			player.PendingKiskBindRequest = pendingRequest;
			await SendPacketAsync(new SmQuestionWindow(SmQuestionWindow.RegisterBindstone, kisk.ObjectId, rangeOrCooldownSeconds: 5));
			return;
		}

		await BindPlayerToKiskAsync(player, kisk);
	}

	private async Task HandleKiskBindQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond, which removes
		// the bindstone RequestResponseHandler before invoking accept/deny behavior.
		var pendingRequest = responder.PendingKiskBindRequest;
		if (pendingRequest == null || packet.QuestionId != pendingRequest.QuestionId)
			return;

		var dispatch = responder.ResponseRequester.Respond(packet.QuestionId, packet.Response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.KiskBind)
		{
			responder.PendingKiskBindRequest = null;
			return;
		}

		var request = dispatch.Request.Payload as PendingKiskBindRequest ?? pendingRequest;
		responder.PendingKiskBindRequest = null;
		if (packet.Response == 0)
			return;
		if (packet.SenderObjectId != 0 && packet.SenderObjectId != request.KiskObjectId)
			return;

		var kisk = _runtimeContext?.Kisks.GetKiskState(request.KiskObjectId);
		if (kisk == null || kisk.ObjectId != request.KiskObjectId)
			return;

		await BindPlayerToKiskAsync(responder, kisk, fullFailureAsNoAuthority: true);
	}

	private async Task BindPlayerToKiskAsync(Player player, PlayerKiskRuntimeState kisk, bool fullFailureAsNoAuthority = false)
	{
		if (!TryGetKiskPosition(kisk.ObjectId, out var position))
			return;

		var authorization = PlayerKiskAuthorizationService.ValidateBind(player, kisk);
		switch (authorization.Status)
		{
			case PlayerKiskBindAuthorizationStatus.AlreadyRegistered:
				await SendPacketAsync(SmSystemMessage.BindstoneAlreadyRegistered());
				return;
			case PlayerKiskBindAuthorizationStatus.Full:
				await SendPacketAsync(fullFailureAsNoAuthority
					? SmSystemMessage.CannotRegisterBindstoneHaveNoAuthority()
					: SmSystemMessage.CannotRegisterBindstoneFull());
				return;
			case PlayerKiskBindAuthorizationStatus.NoAuthority:
				await SendPacketAsync(SmSystemMessage.CannotRegisterBindstoneHaveNoAuthority());
				return;
		}

		var previousKisk = GetPreviousBoundKiskState(player, kisk.ObjectId);
		var bindResult = PlayerKiskBindService.Bind(player, kisk, previousKisk);
		switch (bindResult.Status)
		{
			case PlayerKiskBindStatus.Bound:
				if (previousKisk != null && bindResult.RemovedOldKiskObjectId.HasValue)
				{
					await SendPacketAsync(new SmKiskUpdate(previousKisk));
					if (TryGetKiskPosition(previousKisk.ObjectId, out var previousKiskPosition))
						await BroadcastKiskUpdateAsync(previousKisk, previousKiskPosition, excludedPlayerObjectId: player.ObjectId);
				}
				await SendPacketAsync(new SmKiskUpdate(kisk));
				await BroadcastKiskUpdateAsync(kisk, position, excludedPlayerObjectId: player.ObjectId);
				await SendPacketAsync(SmBindPointInfo.Kisk(position, kisk.ObjectId));
				await SendPacketAsync(SmSystemMessage.BindstoneRegister());
				await BroadcastActionAnimationAsync(player, new SmActionAnimation(player.ObjectId, SmActionAnimation.BindKisk));
				break;
			case PlayerKiskBindStatus.AlreadyRegistered:
				await SendPacketAsync(SmSystemMessage.BindstoneAlreadyRegistered());
				break;
			case PlayerKiskBindStatus.Full:
				await SendPacketAsync(SmSystemMessage.CannotRegisterBindstoneFull());
				break;
		}
	}

	private PlayerKiskRuntimeState? GetPreviousBoundKiskState(Player player, int newKiskObjectId)
	{
		// Java parity: KiskService.onBind removes the player from Player.getKisk() before adding the new Kisk.
		if (player.BoundKiskObjectId == 0 || player.BoundKiskObjectId == newKiskObjectId)
			return null;

		return _runtimeContext?.Kisks.GetKiskState(player.BoundKiskObjectId);
	}

	private async Task BroadcastKiskUpdateAsync(
		PlayerKiskRuntimeState kisk,
		WorldPosition position,
		int? excludedPlayerObjectId = null)
	{
		if (_connectionRegistry == null)
			return;

		// Java parity: model/gameobjects/Kisk.broadcastKiskUpdate fans SM_KISK_UPDATE to members outside the kisk known list,
		// then to same-race players who can see the kisk.
		var onlinePlayers = new List<Player>();
		_connectionRegistry.ForEachOnlinePlayer(onlinePlayers.Add);
		var fanout = PlayerKiskUpdateFanoutService.CreatePlan(kisk, position, onlinePlayers, _isKnownNpc, excludedPlayerObjectId);

		foreach (var playerObjectId in fanout.DirectMemberObjectIds)
			await _connectionRegistry.SendPacketToPlayerAsync(playerObjectId, new SmKiskUpdate(kisk));

		if (fanout.VisibleSameRaceObjectIds.Count == 0)
			return;

		var visibleTargets = fanout.VisibleSameRaceObjectIds.ToHashSet();
		await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			position,
			kisk.ObjectId,
			new SmKiskUpdate(kisk),
			includeSourcePlayer: true,
			filter: player => visibleTargets.Contains(player.ObjectId));
	}

	private bool TryGetKiskPosition(int kiskObjectId, out WorldPosition position)
	{
		if (_world != null
			&& _world.TryGetObject(kiskObjectId, out var gameObject)
			&& gameObject is IWorldNpcObject kiskNpc)
		{
			position = kiskNpc.Position;
			return true;
		}

		position = default;
		return false;
	}

	private async Task DismountRideAsync(Player player)
	{
		if (!player.DismountRide())
			return;

		await BroadcastEmotionAsync(player, new SmEmotion(player, EmotionType.ChangeSpeed, 0, 0));
		await BroadcastEmotionAsync(player, new SmEmotion(player, EmotionType.RideEnd));
	}

	private static PlayerRideInfo ToPlayerRideInfo(RideInfoSummary rideInfo)
	{
		// Java parity: model/templates/ride/RideInfo projected into Player.ride runtime state.
		return new PlayerRideInfo(
			rideInfo.NpcId,
			rideInfo.StartFp,
			rideInfo.CostFp,
			rideInfo.SprintSpeed,
			rideInfo.FlySpeed,
			rideInfo.MoveSpeed);
	}

	private async Task ScheduleIdianPolishAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		IdianPolishPlan polishPlan,
		StaticData staticData,
		bool success)
	{
		// Java parity: model/templates/item/actions/PolishAction.act delayed TaskId.ITEM_USE completion.
		var removeCooldownDelayIdOnCancel = AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: true);
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 5000, 0, 0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: sourceTemplate.GetClientName() ?? sourceTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.Item,
			delay: TimeSpan.FromMilliseconds(5000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await ApplyIdianPolishPlanAsync(player, inventoryItems, sourceItem, polishPlan, staticData, success, cancellationToken);
			},
			cancelEndState: 2,
			removeCooldownDelayIdOnCancel: removeCooldownDelayIdOnCancel);
	}

	private async Task ApplyIdianPolishPlanAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		IdianPolishPlan polishPlan,
		StaticData staticData,
		bool success,
		CancellationToken cancellationToken)
	{
		if (polishPlan.SourceTemplate == null)
			return;

		int? deletedSourceObjectId = polishPlan.DeleteSourceItem ? sourceItem.ObjectId : null;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveIdianPolishMutationAsync(
				player,
				polishPlan.TargetItemUpdate,
				polishPlan.SourceItemUpdate,
				deletedSourceObjectId,
				cancellationToken);
		if (!saved)
			return;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, success ? 1 : 2, success ? 1 : 0));

		if (polishPlan.DeleteSourceItem)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == sourceItem.ObjectId);
			await SendPacketAsync(new SmDeleteItem(sourceItem.ObjectId, SmDeleteItem.UseDeleteType));
		}
		else if (polishPlan.SourceItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, polishPlan.SourceItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(
				polishPlan.SourceItemUpdate,
				polishPlan.SourceTemplate,
				SmInventoryUpdateItem.DecreaseItemUse,
				GetGeneralInfoWarehouseRestrictionFlag(polishPlan.SourceItemUpdate.ItemId, staticData.ItemRestrictionCleanups)));
		}

		if (!success)
		{
			player.InventoryItems = inventoryItems.ToArray();
			await SendPacketAsync(SmSystemMessage.EnchantItemFailed(polishPlan.SourceTemplate.GetClientName() ?? polishPlan.SourceTemplate.Name));
			return;
		}

		if (polishPlan.TargetItemUpdate == null || polishPlan.TargetTemplate == null)
			return;

		ReplaceInventoryItem(inventoryItems, polishPlan.TargetItemUpdate);
		player.InventoryItems = inventoryItems.ToArray();
		await SendPacketAsync(SmSystemMessage.PolishSuccess(polishPlan.TargetTemplate.GetClientName() ?? polishPlan.TargetTemplate.Name));
		await SendPacketAsync(new SmInventoryUpdateItem(
			polishPlan.TargetItemUpdate,
			polishPlan.TargetTemplate,
			SmInventoryUpdateItem.DecreaseItemUse,
			GetGeneralInfoWarehouseRestrictionFlag(polishPlan.TargetItemUpdate.ItemId, staticData.ItemRestrictionCleanups)));
		if (polishPlan.TargetItemUpdate.IsEquipped)
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
	}

	private async Task HandleTamperingUseItemAsync(
		Player player,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		int targetItemObjectId,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/TamperingAction.canAct + act.
		if (targetItemObjectId == 0)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		var targetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == targetItemObjectId);
		if (targetItem == null)
			return;

		var targetTemplate = staticData.ItemTemplates.GetItemTemplate(targetItem.ItemId);
		if (targetTemplate == null || targetTemplate.MaxTampering <= 0 || targetItem.Tempering >= targetTemplate.MaxTampering)
			return;

		var startPlan = TamperingActionExecutionPlanService.CreateStartPlan(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId);
		var removeCooldownDelayIdOnCancel = AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: true);
		await CancelPendingItemUseAsync(player);
		await BroadcastItemUsageAnimationAsync(player, startPlan.BroadcastPacket);
		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: targetTemplate.GetClientName() ?? targetTemplate.Name,
			cancelMessage: PendingItemUseCancelMessage.ItemAuthorize,
			delay: TimeSpan.FromMilliseconds(startPlan.DelayMilliseconds),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteTamperingUseItemAsync(
					player,
					sourceItem.ObjectId,
					sourceTemplate,
					targetItem,
					targetTemplate,
					staticData,
					cancellationToken);
			},
			cancelEndState: 3,
			removeCooldownDelayIdOnCancel: removeCooldownDelayIdOnCancel);
	}

	private async Task CompleteTamperingUseItemAsync(
		Player player,
		int sourceItemObjectId,
		ItemTemplateSummary sourceTemplate,
		InventoryItem originalTargetItem,
		ItemTemplateSummary targetTemplate,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		var inventoryItems = player.InventoryItems.ToList();
		var currentTargetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == originalTargetItem.ObjectId);
		if (currentTargetItem == null && !originalTargetItem.IsEquipped)
		{
			await SendPacketAsync(SmSystemMessage.EnchantItemNoTargetItem());
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItemObjectId, sourceTemplate.TemplateId, 0, 2, 0));
			return;
		}

		var sourceItem = inventoryItems.FirstOrDefault(item => item.ObjectId == sourceItemObjectId && item.Location == CubeStorageId && !item.IsEquipped);
		if (sourceItem == null)
		{
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItemObjectId, sourceTemplate.TemplateId, 0, 2, 0));
			return;
		}

		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var targetItem = currentTargetItem ?? originalTargetItem;
		if (targetTemplate.MaxTampering <= 0 || targetItem.Tempering >= targetTemplate.MaxTampering)
		{
			var sourceSaved = _playerEnterWorldService == null
				|| await _playerEnterWorldService.SaveItemUseSourceMutationAsync(player, sourceItemUpdate, deletedSourceObjectId, cancellationToken);
			if (!sourceSaved)
				return;

			await ApplySourceItemMutationAsync(
				player,
				inventoryItems,
				sourceTemplate,
				sourceItemUpdate,
				deletedSourceObjectId,
				staticData.ItemRestrictionCleanups);
			return;
		}

		var plan = TamperingActionExecutionPlanService.CreateMutationPlan(
			targetItem,
			targetTemplate,
			player.AccountMembership,
			_options.Rates.TamperingChances,
			_options.Custom.EnableEnchantAnnounce,
			player.Name);
		var updatedItems = new List<InventoryItem> { plan.TargetItemUpdate };
		if (sourceItemUpdate != null)
			updatedItems.Add(sourceItemUpdate);
		var deletedObjectIds = new List<int>();
		if (deletedSourceObjectId.HasValue)
			deletedObjectIds.Add(deletedSourceObjectId.Value);
		if (plan.Status == TamperingActionMutationStatus.FailedDestroyed)
			deletedObjectIds.Add(targetItem.ObjectId);
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveAssemblyItemActionMutationAsync(
				player,
				updatedItems,
				deletedObjectIds,
				Array.Empty<InventoryItem>(),
				Array.Empty<InventoryItem>(),
				cancellationToken);
		if (!saved)
			return;

		await ApplySourceItemMutationAsync(
			player,
			inventoryItems,
			sourceTemplate,
			sourceItemUpdate,
			deletedSourceObjectId,
			staticData.ItemRestrictionCleanups);
		await SendPacketAsync(new SmInventoryUpdateItem(
			plan.TargetItemUpdate,
			targetTemplate,
			SmInventoryUpdateItem.StatsChange,
			GetGeneralInfoWarehouseRestrictionFlag(plan.TargetItemUpdate.ItemId, staticData.ItemRestrictionCleanups)));
		await SendPacketAsync(plan.ResultMessage);
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				sourceItemObjectId,
				sourceTemplate.TemplateId,
				0,
				plan.Status == TamperingActionMutationStatus.Succeeded ? 1 : 2,
				0));

		if (plan.Status == TamperingActionMutationStatus.FailedDestroyed)
		{
			player.TrackDeletedItem(targetItem);
			inventoryItems.RemoveAll(item => item.ObjectId == targetItem.ObjectId);
			player.InventoryItems = inventoryItems.ToArray();
			await SendPacketAsync(new SmDeleteItem(targetItem.ObjectId, SmDeleteItem.UseDeleteType));
			if (!targetItem.IsEquipped)
				await SendPacketAsync(SmCubeUpdate.CubeSize(player));
			if (targetItem.IsEquipped)
				await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
			return;
		}

		ReplaceInventoryItem(inventoryItems, plan.TargetItemUpdate);
		player.InventoryItems = inventoryItems.ToArray();
		if (plan.AnnouncementPacket != null && _connectionRegistry != null)
		{
			await _connectionRegistry.BroadcastToWorldAsync(
				plan.AnnouncementPacket,
				otherPlayer => string.Equals(otherPlayer.Race, player.Race, StringComparison.Ordinal));
		}

		if (plan.TargetItemUpdate.IsEquipped)
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
	}

	private async Task BroadcastItemUsageAnimationAsync(Player player, SmItemUsageAnimation packet)
	{
		// Java parity: PacketSendUtility.broadcastPacket(..., true) includes the source player.
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, packet, includeSourcePlayer: true);
		else
			await SendPacketAsync(packet);
	}

	private async Task HandleChargeUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/ChargeAction.canAct + act.
		var itemTemplates = staticData.ItemTemplates;
		var chargeWay = sourceTemplate.Improvement?.ChargeWay ?? 0;
		if (chargeWay == 0)
			return;

		var chargePlans = inventoryItems
			.Where(item => item.IsEquipped)
			.Select(item => ItemChargeService.CreateChargePlan(
				player,
				item,
				itemTemplates,
				sourceTemplate.ChargeActionMaxLevel,
				ignoreRankRequirement: false,
				requirePayment: false))
			.Where(plan => plan is { } && plan.ChargeWay == chargeWay)
			.Cast<ItemChargePlan>()
			.ToArray();
		if (chargePlans.Length == 0)
			return;

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 3000, 0, 0));

		await SchedulePendingItemUseAsync(
			player,
			itemObjectId: sourceItem.ObjectId,
			itemTemplateId: sourceItem.ItemId,
			targetItemName: sourceTemplate.GetClientName() ?? sourceTemplate.Name,
			cancelMessage: chargeWay == 1 ? PendingItemUseCancelMessage.ItemCharge : PendingItemUseCancelMessage.ItemCharge2,
			delay: TimeSpan.FromMilliseconds(3000),
			completeAsync: async cancellationToken =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				await CompleteChargeUseItemAsync(
					player,
					inventoryItems,
					sourceItem,
					sourceTemplate,
					chargePlans,
					staticData,
					cancellationToken);
			},
			cancelEndState: 1);
	}

	private static int? AddItemCooldownIfNeeded(Player player, ItemTemplateSummary sourceTemplate, bool removeOnCancel)
	{
		// Java parity: network/aion/clientpackets/CM_USE_ITEM adds item cooldown before AbstractItemAction.act.
		if (sourceTemplate.UseDelayMillis <= 0)
			return null;

		player.AddItemCooldown(sourceTemplate.UseDelayId, sourceTemplate.UseDelayMillis, DateTimeOffset.UtcNow);
		return removeOnCancel ? sourceTemplate.UseDelayId : null;
	}

	private async Task CompleteChargeUseItemAsync(
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		IReadOnlyList<ItemChargePlan> chargePlans,
		StaticData staticData,
		CancellationToken cancellationToken)
	{
		var chargedItems = chargePlans
			.Select(plan => CopyInventoryItem(plan.Item, charge: plan.TargetChargePoints))
			.ToArray();
		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveItemChargeActionMutationAsync(
				player,
				chargedItems,
				sourceItemUpdate,
				deletedSourceObjectId,
				cancellationToken);
		if (!saved)
			return;

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 0));

		if (deletedSourceObjectId.HasValue)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == sourceItem.ObjectId);
			await SendPacketAsync(new SmDeleteItem(sourceItem.ObjectId, SmDeleteItem.UseDeleteType));
		}
		else if (sourceItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, sourceItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(sourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}

		var completedChargeWays = new HashSet<int>();
		foreach (var (plan, chargedItem) in chargePlans.Zip(chargedItems))
		{
			ReplaceInventoryItem(inventoryItems, chargedItem);
			if (GetChargeBarStep(plan.Item.Charge) != GetChargeBarStep(chargedItem.Charge))
				await SendPacketAsync(new SmInventoryUpdateItem(chargedItem, plan.Template, SmInventoryUpdateItem.Charge));

			var itemName = plan.Template.GetClientName() ?? plan.Template.Name;
			await SendPacketAsync(
				plan.ChargeWay == 1
					? SmSystemMessage.ItemChargeSuccess(itemName, plan.Level)
					: SmSystemMessage.ItemCharge2Success(itemName, plan.Level));
			completedChargeWays.Add(plan.ChargeWay);
		}

		player.InventoryItems = inventoryItems.ToArray();
		await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
		foreach (var chargeWayComplete in completedChargeWays)
		{
			await SendPacketAsync(
				chargeWayComplete == 1
					? SmSystemMessage.ItemChargeAllComplete()
					: SmSystemMessage.ItemCharge2AllComplete());
		}
	}

	private static int GetChargeBarStep(int charge)
	{
		return Math.Clamp(charge, 0, ItemChargeService.Level2ChargePoints) / 50_000;
	}

	private async Task HandleClientCommandRollAsync(Player player, CmClientCommandRoll packet)
	{
		// Java parity: network/aion/clientpackets/CM_CLIENT_COMMAND_ROLL.runImpl.
		var maxRoll = packet.MaxRoll <= 0 ? 100 : packet.MaxRoll;
		var roll = RandomNumberGenerator.GetInt32(maxRoll) + 1;
		await SendPacketAsync(SmSystemMessage.DiceCustomMe(roll, maxRoll));
		if (_connectionRegistry != null)
		{
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(
				player.Position,
				player.ObjectId,
				SmSystemMessage.DiceCustomOther(player.Name, roll, maxRoll));
		}
	}

	private async Task HandleReportPlayerAsync(Player player, CmReportPlayer packet)
	{
		// Java parity: network/aion/clientpackets/CM_REPORT_PLAYER.runImpl.
		const string UnlimitedReports = "\u221e";
		switch (packet.ReportType)
		{
			case 0:
				var reportedName = GetRealCharacterName(packet.PlayerName);
				Player? reportedPlayer = null;
				_connectionRegistry?.TryGetOnlinePlayerByName(reportedName, out reportedPlayer);
				if (reportedPlayer != null && !string.Equals(reportedPlayer.Race, player.Race, StringComparison.OrdinalIgnoreCase))
				{
					await SendPacketAsync(SmSystemMessage.DoNotAccuse());
				}
				else if (reportedPlayer != null && reportedPlayer.ObjectId == player.ObjectId)
				{
					await SendPacketAsync(SmSystemMessage.InvalidTarget());
				}
				else
				{
					_logger.LogInformation("Player {PlayerName} ({PlayerObjectId}) reported player {ReportedPlayer}", player.Name, player.ObjectId, packet.PlayerName);
					await SendPacketAsync(SmSystemMessage.AccuseSubmit(packet.PlayerName, UnlimitedReports));
				}
				break;
			case 1:
				await SendPacketAsync(SmSystemMessage.AccuseCountInfo(UnlimitedReports));
				break;
			default:
				_logger.LogWarning(
					"Player {PlayerName} ({PlayerObjectId}) sent unhandled report type {ReportType} for {ReportedPlayer}",
					player.Name,
					player.ObjectId,
					packet.ReportType,
					packet.PlayerName);
				break;
		}
	}

	private void HandleUiSettings(Player player, CmUiSettings packet)
	{
		// Java parity: network/aion/clientpackets/CM_UI_SETTINGS.runImpl mutates PlayerSettings for PlayerSettingsDAO.saveSettings.
		switch (packet.SettingsType)
		{
			case 0:
				player.Settings.UiSettings = packet.Data;
				break;
			case 1:
				player.Settings.Shortcuts = packet.Data;
				break;
			case 2:
				player.Settings.HouseBuddies = packet.Data;
				break;
			default:
				_logger.LogWarning(
					"Player {PlayerName} ({PlayerObjectId}) sent unknown UI settings type {SettingsType}",
					player.Name,
					player.ObjectId,
					packet.SettingsType);
				break;
		}
	}

	private async Task HandleCustomSettingsAsync(Player player, CmCustomSettings packet)
	{
		// Java parity: network/aion/clientpackets/CM_CUSTOM_SETTINGS.runImpl.
		player.Settings.Display = packet.Display;
		player.Settings.Deny = packet.Deny;

		var response = new SmCustomSettings(player);
		if (_connectionRegistry == null)
		{
			await SendPacketAsync(response);
			return;
		}

		var sent = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			player.Position,
			player.ObjectId,
			response,
			includeSourcePlayer: true);
		if (sent == 0)
			await SendPacketAsync(response);
	}

	private async Task HandleSetNoteAsync(Player player, CmSetNote packet)
	{
		// Java parity: network/aion/clientpackets/CM_SET_NOTE.runImpl.
		if (string.Equals(player.Note, packet.Note, StringComparison.Ordinal))
			return;

		player.Note = packet.Note;

		if (_connectionRegistry != null)
		{
			foreach (var friend in player.Friends)
			{
				if (!_connectionRegistry.TryGetOnlinePlayerByName(friend.Name, out var friendPlayer) || friendPlayer == null)
					continue;

				UpdateFriendSnapshot(friendPlayer, player, player.FriendListStatus);
				await _connectionRegistry.SendPacketToPlayerAsync(
					friendPlayer.ObjectId,
					new SmFriendList(friendPlayer.Friends, GetPlayerExperienceTable()));
			}
		}

		var response = new SmUpdateNote(player);
		if (_connectionRegistry == null)
		{
			await SendPacketAsync(response);
			return;
		}

		var sent = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			player.Position,
			player.ObjectId,
			response,
			includeSourcePlayer: true);
		if (sent == 0)
			await SendPacketAsync(response);
	}

	internal async Task<PlayerShowBrandCommandPlan> HandleShowBrandCommandAsync(
		Player player,
		CmShowBrand packet,
		CancellationToken cancellationToken = default)
	{
		// Java parity: network/aion/clientpackets/CM_SHOW_BRAND.runImpl ignores action and either echoes SM_SHOW_BRAND to solo players or broadcasts via TemporaryPlayerTeam.updateBrand.
		var plan = _showBrandCommandPlanner.CreatePlan(player, packet.BrandId, packet.TargetObjectId);
		if (plan.SoloEchoIntent != null)
			await SendShowBrandAsync(player.ObjectId, plan.SoloEchoIntent.CreatePacket(), cancellationToken);

		if (plan.GroupUpdatePlan != null)
		{
			foreach (var intent in plan.GroupUpdatePlan.BrandBroadcasts)
				await SendShowBrandAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
		}

		if (plan.AllianceUpdatePlan != null)
		{
			foreach (var intent in plan.AllianceUpdatePlan.BrandBroadcasts)
				await SendShowBrandAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
		}

		return plan;
	}

	private async Task SendShowBrandAsync(
		int recipientObjectId,
		SmShowBrand packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	internal async Task<PlayerAllianceReadyCheckPlan?> HandlePlayerStatusInfoAsync(
		Player player,
		CmPlayerStatusInfo packet,
		CancellationToken cancellationToken = default)
	{
		// Java parity: network/aion/clientpackets/CM_PLAYER_STATUS_INFO.runImpl resolves TeamCommand before dispatching.
		if (!IsKnownPlayerStatusTeamCommand(packet.CommandCode))
			throw new InvalidOperationException($"Invalid team command code {packet.CommandCode}");

		// Java parity: network/aion/clientpackets/CM_PLAYER_STATUS_INFO.runImpl delegates ready-check commands through PlayerTeamCommandService -> PlayerAllianceService.checkReady.
		if (packet.CommandCode == 9)
		{
			// Java parity: TeamCommand.GROUP_SET_LFG -> Player.setLookingForGroup(selectedObjectId == 2).
			player.IsLookingForGroup = packet.SelectedObjectId == 2;
			return null;
		}

		if (packet.CommandCode == 3)
		{
			await HandleGroupLeaderChangeAsync(player, packet.SelectedObjectId, cancellationToken);
			return null;
		}

		if (packet.CommandCode == 2)
		{
			await HandleGroupBanMemberAsync(player, packet.SelectedObjectId, cancellationToken);
			return null;
		}

		if (packet.CommandCode == 6)
		{
			await HandleGroupRemoveMemberAsync(player, packet.SelectedObjectId, cancellationToken);
			return null;
		}

		if (packet.CommandCode is 10 or 11)
		{
			await HandleGroupMentorStatusAsync(player, isMentor: packet.CommandCode == 10, cancellationToken);
			return null;
		}

		if (packet.CommandCode == 27)
		{
			await HandleAllianceGroupChangeAsync(player, packet, cancellationToken);
			return null;
		}

		if (packet.CommandCode is 25 or 26)
		{
			await HandleAllianceViceCaptainAssignmentAsync(
				player,
				packet.SelectedObjectId,
				packet.CommandCode == 25 ? PlayerAllianceAssignType.Promote : PlayerAllianceAssignType.Demote,
				cancellationToken);
			return null;
		}

		if (packet.CommandCode == 17)
		{
			await HandleAllianceLeaderChangeAsync(player, packet.SelectedObjectId, cancellationToken);
			return null;
		}

		if (packet.CommandCode == 14)
		{
			await HandleAllianceLeaveAsync(player, cancellationToken);
			return null;
		}

		if (packet.CommandCode == 16)
		{
			await HandleAllianceBanMemberAsync(player, packet.SelectedObjectId, cancellationToken);
			return null;
		}

		if (packet.CommandCode == 29)
		{
			// Java parity: PlayerTeamCommandService LEAGUE_LEAVE -> LeagueService.removeAlliance -> LeagueLeftEvent(LEAVE).
			var leagueLeaveAlliance = _playerAllianceRuntime.Resolve(player);
			if (leagueLeaveAlliance == null)
				return null;

			var leagueLeavePlan = _playerLeagueRuntime.RemoveAlliance(
				leagueLeaveAlliance.AllianceId,
				_playerAllianceRuntime);
			foreach (var intent in leagueLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence))
				await SendLeaguePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
			return null;
		}

		if (packet.CommandCode == 31)
		{
			// Java parity: CM_PLAYER_STATUS_INFO handles LEAGUE_ALLIANCE_MOVE directly through LeagueService.moveAlliance, outside PlayerTeamCommandService.
			var leagueMoveAlliance = _playerAllianceRuntime.Resolve(player);
			if (leagueMoveAlliance == null)
				throw new InvalidOperationException("Player alliance should not be null");

			var leagueMovePlan = _playerLeagueRuntime.MoveAlliance(
				leagueMoveAlliance.AllianceId,
				player.ObjectId,
				packet.SelectedObjectId,
				packet.AllianceGroupId,
				_playerAllianceRuntime);
			if (leagueMovePlan != null)
			{
				foreach (var intent in leagueMovePlan.PacketIntents.OrderBy(intent => intent.Sequence))
					await SendLeaguePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
			}
			return null;
		}

		if (packet.CommandCode == 30)
		{
			// Java parity: PlayerTeamCommandService LEAGUE_EXPEL -> findLeagueAlliance -> LeagueService.expelAlliance.
			var leagueExpelAlliance = _playerAllianceRuntime.Resolve(player);
			if (leagueExpelAlliance == null)
				return null;

			var leagueExpelPlan = _playerLeagueRuntime.ExpelAlliance(
				leagueExpelAlliance.AllianceId,
				player.ObjectId,
				packet.SelectedObjectId,
				_playerAllianceRuntime);
			foreach (var intent in leagueExpelPlan.PacketIntents.OrderBy(intent => intent.Sequence))
				await SendLeaguePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
			return null;
		}

		if (packet.CommandCode == 32)
		{
			// Java parity: PlayerTeamCommandService LEAGUE_SET_LEADER -> findLeagueAlliance -> LeagueService.setLeader.
			var leagueCommandAlliance = _playerAllianceRuntime.Resolve(player);
			if (leagueCommandAlliance == null)
				return null;

			var leagueSetLeaderPlan = _playerLeagueRuntime.SetLeader(
				leagueCommandAlliance.AllianceId,
				player.ObjectId,
				packet.SelectedObjectId,
				_playerAllianceRuntime);
			if (leagueSetLeaderPlan != null)
			{
				foreach (var intent in leagueSetLeaderPlan.PacketIntents.OrderBy(intent => intent.Sequence))
					await SendLeaguePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
			}
			return null;
		}

		if (!Enum.IsDefined(typeof(PlayerAllianceReadyCheckCommand), packet.CommandCode))
			return null;

		var alliance = _playerAllianceRuntime.Resolve(player);
		if (alliance == null)
			return null;

		var plan = _playerAllianceRuntime.CheckReady(
			alliance.AllianceId,
			player,
			(PlayerAllianceReadyCheckCommand)packet.CommandCode);
		if (plan == null)
			return null;

		foreach (var intent in plan.PacketIntents.OrderBy(intent => intent.Sequence))
			await SendAllianceReadyCheckAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

		return plan;
	}

	private static bool IsKnownPlayerStatusTeamCommand(int commandCode)
	{
		// Java parity: model/team/common/events/TeamCommand.getCommand contains this exact command id set.
		return commandCode is
			2 or 3 or 6 or 9 or 10 or 11 or
			14 or 16 or 17 or
			20 or 21 or 22 or 23 or 24 or
			25 or 26 or 27 or
			29 or 30 or 31 or 32;
	}

	private static string FormatJavaPlayer(Player player)
	{
		// Java parity: model/gameobjects/player/Player.toString.
		return $"Player [id={player.ObjectId}, name={player.Name}]";
	}

	private static InvalidOperationException CreateInvalidTeamMemberException(Player player, int memberObjectId)
	{
		// Java parity: PlayerTeamCommandService.findMember requireNonNull message.
		return new InvalidOperationException($"{FormatJavaPlayer(player)} tried to execute team command on non-existent member with ID {memberObjectId}");
	}

	private async Task SendAllianceReadyCheckAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task<PlayerGroupMentorStatusChangePlan?> HandleGroupMentorStatusAsync(
		Player player,
		bool isMentor,
		CancellationToken cancellationToken)
	{
		// Java parity: PlayerTeamCommandService GROUP_START_MENTORING/GROUP_END_MENTORING -> PlayerGroupService mentoring events.
		var group = _playerGroupRuntime.Resolve(player);
		if (group == null)
			return null;

		var plan = _playerGroupRuntime.CreateMentorStatusChangePlan(group.TeamId, player, isMentor);
		if (plan == null)
			return null;

		foreach (var intent in plan.SystemMessageIntents)
			await SendGroupMentorPacketAsync(intent.RecipientObjectId, intent.Message, cancellationToken);

		foreach (var intent in plan.MemberInfoIntents)
		{
			var packet = intent.CreatePacket();
			if (packet != null)
				await SendGroupMentorPacketAsync(intent.RecipientObjectId, packet, cancellationToken);
		}

		return plan;
	}

	private async Task SendGroupMentorPacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task SendGroupInvitePacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken = default)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task SendGroupEnteredPlanAsync(
		PlayerGroupEnteredPacketPlan plan,
		CancellationToken cancellationToken = default)
	{
		var groupInfo = plan.CreateGroupInfoPacket();
		if (groupInfo != null)
			await SendGroupInvitePacketAsync(plan.EnteringPlayerObjectId, groupInfo, cancellationToken);

		foreach (var intent in plan.SystemMessageIntents)
			await SendGroupInvitePacketAsync(intent.RecipientObjectId, intent.Message, cancellationToken);

		foreach (var intent in plan.MemberInfoIntents)
		{
			var packet = intent.CreatePacket();
			if (packet != null)
				await SendGroupInvitePacketAsync(intent.RecipientObjectId, packet, cancellationToken);
		}

		if (plan.BrandIntent != null)
			await SendGroupInvitePacketAsync(plan.BrandIntent.RecipientObjectId, plan.BrandIntent.CreatePacket(), cancellationToken);

		if (plan.AbyssRankUpdateIntent != null)
			await SendGroupInvitePacketAsync(
				plan.AbyssRankUpdateIntent.PlayerObjectId,
				plan.AbyssRankUpdateIntent.CreatePacket(),
				cancellationToken);
	}

	private async Task<PlayerGroupLeaderChangePlan?> HandleGroupLeaderChangeAsync(
		Player player,
		int targetObjectId,
		CancellationToken cancellationToken)
	{
		// Java parity: PlayerTeamCommandService GROUP_SET_LEADER -> PlayerGroupService.changeLeader.
		var group = _playerGroupRuntime.Resolve(player);
		if (group == null)
			return null;

		var newLeaderObjectId = targetObjectId == 0 ? player.ObjectId : targetObjectId;
		if (!_playerGroupRuntime.HasMember(group.TeamId, newLeaderObjectId))
			throw CreateInvalidTeamMemberException(player, newLeaderObjectId);

		var plan = _playerGroupRuntime.ChangeLeader(group.TeamId, newLeaderObjectId);
		if (plan == null)
			return null;

		foreach (var intent in plan.PacketIntents.OrderBy(intent => intent.Sequence))
		{
			await SendGroupLeaderPacketAsync(intent.RecipientObjectId, new SmGroupInfo(intent.GroupInfoPlan), cancellationToken);
			await SendGroupLeaderPacketAsync(intent.RecipientObjectId, intent.SystemMessage, cancellationToken);
		}

		return plan;
	}

	private async Task SendGroupLeaderPacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task<PlayerGroupLeavePlan?> HandleGroupRemoveMemberAsync(
		Player player,
		int targetObjectId,
		CancellationToken cancellationToken)
	{
		// Java parity: PlayerTeamCommandService GROUP_REMOVE_MEMBER -> PlayerGroupService.removePlayer.
		var group = _playerGroupRuntime.Resolve(player);
		if (group == null)
			return null;

		var leavedPlayerObjectId = targetObjectId == 0 ? player.ObjectId : targetObjectId;
		var leavedMember = _playerGroupRuntime.GetMember(group.TeamId, leavedPlayerObjectId);
		if (leavedMember == null)
			throw CreateInvalidTeamMemberException(player, leavedPlayerObjectId);

		var plan = _playerGroupRuntime.RemoveMemberWithLeavePlan(leavedMember.Player);
		if (plan == null)
			return null;

		foreach (var intent in plan.PacketIntents.OrderBy(intent => intent.Sequence))
			await SendGroupRemovePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

		if (plan.LeaderChangePlan != null)
		{
			foreach (var intent in plan.LeaderChangePlan.PacketIntents.OrderBy(intent => intent.Sequence))
			{
				await SendGroupRemovePacketAsync(intent.RecipientObjectId, new SmGroupInfo(intent.GroupInfoPlan), cancellationToken);
				await SendGroupRemovePacketAsync(intent.RecipientObjectId, intent.SystemMessage, cancellationToken);
			}
		}

		foreach (var intent in plan.BaseLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence))
			await SendGroupRemovePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

		return plan;
	}

	private async Task<PlayerGroupLeavePlan?> HandleGroupBanMemberAsync(
		Player player,
		int targetObjectId,
		CancellationToken cancellationToken)
	{
		// Java parity: PlayerTeamCommandService GROUP_BAN_MEMBER -> PlayerGroupService.banPlayer.
		var group = _playerGroupRuntime.Resolve(player);
		if (group == null)
			return null;

		var bannedPlayerObjectId = targetObjectId == 0 ? player.ObjectId : targetObjectId;
		var bannedMember = _playerGroupRuntime.GetMember(group.TeamId, bannedPlayerObjectId);
		if (bannedMember == null)
			throw CreateInvalidTeamMemberException(player, bannedPlayerObjectId);

		if (bannedPlayerObjectId == player.ObjectId)
		{
			await SendGroupRemovePacketAsync(player.ObjectId, SmSystemMessage.PartyCantBanSelf(), cancellationToken);
			return null;
		}

		if (!_playerGroupRuntime.IsLeader(group.TeamId, player))
		{
			await SendGroupRemovePacketAsync(player.ObjectId, SmSystemMessage.ForceOnlyLeaderCanBanish(), cancellationToken);
			return null;
		}

		var descriptor = _playerGroupRuntime.GetDescriptor(group.TeamId);
		if (descriptor?.TeamType == PlayerGroupType.AutoGroup)
		{
			await SendGroupRemovePacketAsync(player.ObjectId, SmSystemMessage.PartyForceNoRightToDecide(), cancellationToken);
			return null;
		}

		var plan = _playerGroupRuntime.RemoveMemberWithLeavePlan(
			bannedMember.Player,
			PlayerGroupLeaveReason.Ban,
			player.Name);
		if (plan == null)
			return null;

		foreach (var intent in plan.PacketIntents.OrderBy(intent => intent.Sequence))
			await SendGroupRemovePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

		if (plan.LeaderChangePlan != null)
		{
			foreach (var intent in plan.LeaderChangePlan.PacketIntents.OrderBy(intent => intent.Sequence))
			{
				await SendGroupRemovePacketAsync(intent.RecipientObjectId, new SmGroupInfo(intent.GroupInfoPlan), cancellationToken);
				await SendGroupRemovePacketAsync(intent.RecipientObjectId, intent.SystemMessage, cancellationToken);
			}
		}

		if (bannedMember.IsOnline)
			await SendGroupRemovePacketAsync(bannedMember.ObjectId, SmSystemMessage.PartyYouAreBanished(), cancellationToken);

		foreach (var intent in plan.BaseLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence))
			await SendGroupRemovePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

		return plan;
	}

	private async Task SendGroupRemovePacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task<PlayerAllianceGroupChangeServicePlan> HandleAllianceGroupChangeAsync(
		Player player,
		CmPlayerStatusInfo packet,
		CancellationToken cancellationToken)
	{
		// Java parity: CM_PLAYER_STATUS_INFO ALLIANCE_CHANGE_GROUP -> PlayerAllianceService.changeMemberGroup.
		var plan = _playerAllianceGroupChangeServicePlanner.CreateChangeMemberGroupPlan(
			player,
			packet.SelectedObjectId,
			packet.SecondObjectId,
			packet.AllianceGroupId);

		if (plan.SystemMessageIntent != null)
			await SendAllianceGroupChangePacketAsync(plan.SystemMessageIntent.RecipientObjectId, plan.SystemMessageIntent.Message, cancellationToken);

		if (plan.GroupChangePlan != null)
		{
			var recipients = _playerAllianceRuntime.GetMemberObjectIds(plan.AllianceId);
			foreach (var intent in plan.GroupChangePlan.MemberInfoIntents)
			{
				var memberInfo = intent.CreatePacket();
				if (memberInfo == null)
					continue;

				foreach (var recipientObjectId in recipients)
					await SendAllianceGroupChangePacketAsync(recipientObjectId, memberInfo, cancellationToken);
			}
		}

		return plan;
	}

	private async Task SendLeaguePacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task SendAllianceGroupChangePacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task<PlayerAllianceViceCaptainAssignmentPlan?> HandleAllianceViceCaptainAssignmentAsync(
		Player player,
		int targetObjectId,
		PlayerAllianceAssignType assignType,
		CancellationToken cancellationToken)
	{
		// Java parity: PlayerTeamCommandService ALLIANCE_SET_VICECAPTAIN/ALLIANCE_UNSET_VICECAPTAIN -> PlayerAllianceService.changeViceCaptain.
		var alliance = _playerAllianceRuntime.Resolve(player);
		if (alliance == null)
			return null;

		var eventPlayerObjectId = targetObjectId == 0 ? player.ObjectId : targetObjectId;
		if (!_playerAllianceRuntime.HasMember(alliance.AllianceId, eventPlayerObjectId))
			throw CreateInvalidTeamMemberException(player, eventPlayerObjectId);

		var eventPlayer = _playerAllianceRuntime.GetMember(alliance.AllianceId, eventPlayerObjectId)?.Player;
		var plan = _playerAllianceRuntime.AssignViceCaptain(alliance.AllianceId, eventPlayerObjectId, assignType);
		if (plan == null)
			return null;

		await DispatchAllianceViceCaptainAssignmentAsync(
			alliance,
			plan,
			assignType,
			eventPlayer?.Name ?? string.Empty,
			cancellationToken);

		return plan;
	}

	private async Task SendAllianceViceCaptainPacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task<PlayerAllianceLeaveWorkflowPlan?> HandleAllianceLeaveAsync(
		Player player,
		CancellationToken cancellationToken)
	{
		// Java parity: PlayerTeamCommandService ALLIANCE_LEAVE -> PlayerAllianceService.removePlayer.
		var alliance = _playerAllianceRuntime.Resolve(player);
		if (alliance == null)
			return null;

		if (_playerAllianceRuntime.IsLeader(alliance.AllianceId, player))
		{
			var fallbackLeaderObjectId = _playerAllianceRuntime.SelectFallbackLeaderObjectId(alliance.AllianceId, player.ObjectId);
			if (fallbackLeaderObjectId == null)
			{
				// Java parity: ChangeAllianceLeaderEvent may find no online fallback; the following PlayerAllianceLeavedEvent still removes the leader.
				// Keep the non-disband multi-member variant deferred because Java can temporarily retain a removed leader reference there.
				if (_playerAllianceRuntime.GetMemberObjectIds(alliance.AllianceId).Count != 2)
					return null;
			}
			else
			{
				var leaderChangePlan = _playerAllianceRuntime.ChangeLeader(
					alliance.AllianceId,
					fallbackLeaderObjectId.Value,
					eventPlayerWasSpecified: false);
				if (leaderChangePlan != null)
				{
					foreach (var intent in leaderChangePlan.AllianceInfoIntents)
						await SendAllianceLeaderPacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

					foreach (var intent in leaderChangePlan.SystemMessageIntents)
						await SendAllianceLeaderPacketAsync(intent.RecipientObjectId, intent.Message, cancellationToken);
				}
			}
		}

		var plan = _playerAllianceRuntime.RemoveMemberWithLeaveWorkflow(
			player,
			deferDisbandCleanup: alliance.LeagueId != 0);
		if (plan == null)
			return null;

		await SendAllianceLeaveWorkflowAsync(plan, alliance.LeagueId, cancellationToken);

		return plan;
	}

	private async Task<PlayerAllianceLeaveWorkflowPlan?> HandleAllianceBanMemberAsync(
		Player player,
		int targetObjectId,
		CancellationToken cancellationToken)
	{
		// Java parity: PlayerTeamCommandService ALLIANCE_BAN_MEMBER -> PlayerAllianceService.banPlayer.
		var alliance = _playerAllianceRuntime.Resolve(player);
		if (alliance == null)
			return null;

		var bannedPlayerObjectId = targetObjectId == 0 ? player.ObjectId : targetObjectId;
		var bannedMember = _playerAllianceRuntime.GetMember(alliance.AllianceId, bannedPlayerObjectId);
		if (bannedMember == null)
			throw CreateInvalidTeamMemberException(player, bannedPlayerObjectId);

		if (bannedPlayerObjectId == player.ObjectId)
		{
			await SendAllianceLeavePacketAsync(player.ObjectId, SmSystemMessage.ForceCantBanSelf(), cancellationToken);
			return null;
		}

		if (!_playerAllianceRuntime.IsLeader(alliance.AllianceId, player))
		{
			await SendAllianceLeavePacketAsync(player.ObjectId, SmSystemMessage.ForceOnlyLeaderCanBanish(), cancellationToken);
			return null;
		}

		var descriptor = _playerAllianceRuntime.GetDescriptor(alliance.AllianceId);
		if (descriptor?.TeamType == PlayerAllianceTeamType.AutoAlliance)
		{
			await SendAllianceLeavePacketAsync(player.ObjectId, SmSystemMessage.PartyForceNoRightToDecide(), cancellationToken);
			return null;
		}

		var plan = _playerAllianceRuntime.RemoveMemberWithLeaveWorkflow(
			bannedMember.Player,
			PlayerAllianceLeaveReason.Ban,
			player.Name,
			deferDisbandCleanup: alliance.LeagueId != 0);
		if (plan == null)
			return null;

		await SendAllianceLeaveWorkflowAsync(plan, alliance.LeagueId, cancellationToken);

		return plan;
	}

	private async Task SendAllianceLeaveWorkflowAsync(
		PlayerAllianceLeaveWorkflowPlan plan,
		int leagueId,
		CancellationToken cancellationToken)
	{
		if (leagueId != 0 && plan.AllianceLeavePlan.WouldDisband)
		{
			var (preDisbandIntents, disbandIntents, postDisbandIntents) = SplitAllianceDisbandIntents(plan);
			var leagueAllianceInfoByRecipient = CreateLeagueAllianceInfoByRecipient(plan, leagueId);
			foreach (var intent in preDisbandIntents)
				await SendAllianceLeavePacketAsync(
					intent.RecipientObjectId,
					CreateAllianceLeavePacket(intent, leagueAllianceInfoByRecipient),
					cancellationToken);

			if (plan.AllianceLeavePlan.WouldBroadcastLeague)
			{
				var leagueBroadcastPlan = _playerLeagueRuntime.BroadcastAllianceInfoExceptAlliance(
					leagueId,
					plan.AllianceId,
					_playerAllianceRuntime);
				if (leagueBroadcastPlan != null)
				{
					foreach (var intent in leagueBroadcastPlan.PacketIntents.OrderBy(intent => intent.Sequence))
						await SendLeaguePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
				}
			}

			var leagueLeavePlan = _playerLeagueRuntime.RemoveAlliance(plan.AllianceId, _playerAllianceRuntime);
			foreach (var intent in leagueLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence))
				await SendLeaguePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

			_playerAllianceRuntime.CompleteDeferredDisbandAfterLeaveWorkflow(plan.AllianceId);

			foreach (var intent in disbandIntents)
				await SendAllianceLeavePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

			foreach (var intent in postDisbandIntents)
				await SendAllianceLeavePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
		}
		else
		{
			var leagueAllianceInfoByRecipient = leagueId != 0 && plan.AllianceLeavePlan.WouldBroadcastLeague
				? CreateLeagueAllianceInfoByRecipient(plan, leagueId)
				: null;
			var (preBroadcastIntents, postBroadcastIntents) = leagueId != 0 && plan.AllianceLeavePlan.WouldBroadcastLeague
				? SplitAlliancePostBroadcastIntents(plan)
				: (plan.AllianceLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence).ToArray(), Array.Empty<PlayerAlliancePacketIntent>());

			foreach (var intent in preBroadcastIntents)
				await SendAllianceLeavePacketAsync(
					intent.RecipientObjectId,
					CreateAllianceLeavePacket(intent, leagueAllianceInfoByRecipient),
					cancellationToken);

			if (leagueId != 0 && plan.AllianceLeavePlan.WouldBroadcastLeague)
			{
				var leagueBroadcastPlan = _playerLeagueRuntime.BroadcastAllianceInfoExceptAlliance(
					leagueId,
					plan.AllianceId,
					_playerAllianceRuntime);
				if (leagueBroadcastPlan != null)
				{
					foreach (var intent in leagueBroadcastPlan.PacketIntents.OrderBy(intent => intent.Sequence))
						await SendLeaguePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
				}
			}

			foreach (var intent in postBroadcastIntents)
				await SendAllianceLeavePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
		}

		foreach (var intent in plan.BaseLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence))
			await SendAllianceLeavePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
	}

	private IReadOnlyDictionary<int, GameServerPacket>? CreateLeagueAllianceInfoByRecipient(
		PlayerAllianceLeaveWorkflowPlan plan,
		int leagueId)
	{
		// Java parity: PlayerAllianceLeavedEvent sends new SM_ALLIANCE_INFO(team) to remaining alliance members.
		// When the alliance is in a league, the constructor expands the real league id, loot rules, and league rows.
		var leagueInfoPlan = _playerLeagueRuntime.CreateAllianceInfoFanout(
			leagueId,
			plan.AllianceId,
			messageId: 0,
			message: string.Empty,
			_playerAllianceRuntime);
		return leagueInfoPlan?.PacketIntents
			.Where(intent => intent.Kind == PlayerLeaguePacketIntentKind.AllianceInfo)
			.ToDictionary(intent => intent.RecipientObjectId, intent => intent.CreatePacket());
	}

	private static GameServerPacket CreateAllianceLeavePacket(
		PlayerAlliancePacketIntent intent,
		IReadOnlyDictionary<int, GameServerPacket>? leagueAllianceInfoByRecipient)
	{
		if (intent.Kind == PlayerAlliancePacketIntentKind.AllianceInfo
			&& leagueAllianceInfoByRecipient != null
			&& leagueAllianceInfoByRecipient.TryGetValue(intent.RecipientObjectId, out var packet))
			return packet;

		return intent.CreatePacket();
	}

	private static (
		IReadOnlyList<PlayerAlliancePacketIntent> PreBroadcastIntents,
		IReadOnlyList<PlayerAlliancePacketIntent> PostBroadcastIntents) SplitAlliancePostBroadcastIntents(PlayerAllianceLeaveWorkflowPlan plan)
	{
		// Java parity: PlayerAllianceLeavedEvent BAN sends STR_FORCE_BAN_ME after League.broadcast(team).
		const int forceBanMeMessageId = 1300979;

		var orderedIntents = plan.AllianceLeavePlan.PacketIntents
			.OrderBy(intent => intent.Sequence)
			.ToArray();
		var postBroadcastStartIndex = Array.FindIndex(
			orderedIntents,
			intent => intent.Kind == PlayerAlliancePacketIntentKind.SystemMessage
				&& intent.SystemMessage?.MessageId == forceBanMeMessageId);
		if (postBroadcastStartIndex < 0)
			return (orderedIntents, []);

		return (
			orderedIntents.Take(postBroadcastStartIndex).ToArray(),
			orderedIntents.Skip(postBroadcastStartIndex).ToArray());
	}

	private static (
		IReadOnlyList<PlayerAlliancePacketIntent> PreDisbandIntents,
		IReadOnlyList<PlayerAlliancePacketIntent> DisbandIntents,
		IReadOnlyList<PlayerAlliancePacketIntent> PostDisbandIntents) SplitAllianceDisbandIntents(PlayerAllianceLeaveWorkflowPlan plan)
	{
		// Java parity: PlayerAllianceLeavedEvent sends normal leave fanout, then PlayerAllianceService.disband(..., true)
		// emits league-left before AllianceDisbandEvent, then BAN sends STR_FORCE_BAN_ME before base PlayerLeavedEvent.
		const int partyAllianceDispersedMessageId = 1300201;
		const int forceBanMeMessageId = 1300979;

		var orderedIntents = plan.AllianceLeavePlan.PacketIntents
			.OrderBy(intent => intent.Sequence)
			.ToArray();
		var disbandStartIndex = Array.FindIndex(
			orderedIntents,
			intent => intent.Kind == PlayerAlliancePacketIntentKind.SystemMessage
				&& intent.SystemMessage?.MessageId == partyAllianceDispersedMessageId);
		if (disbandStartIndex < 0)
			return (orderedIntents, [], []);

		var postDisbandStartIndex = Array.FindIndex(
			orderedIntents,
			disbandStartIndex,
			intent => intent.Kind == PlayerAlliancePacketIntentKind.SystemMessage
				&& intent.SystemMessage?.MessageId == forceBanMeMessageId);

		if (postDisbandStartIndex < 0)
		{
			return (
				orderedIntents.Take(disbandStartIndex).ToArray(),
				orderedIntents.Skip(disbandStartIndex).ToArray(),
				[]);
		}

		return (
			orderedIntents.Take(disbandStartIndex).ToArray(),
			orderedIntents.Skip(disbandStartIndex).Take(postDisbandStartIndex - disbandStartIndex).ToArray(),
			orderedIntents.Skip(postDisbandStartIndex).ToArray());
	}

	private async Task SendAllianceLeavePacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task<PlayerAllianceLeaderChangePlan?> HandleAllianceLeaderChangeAsync(
		Player player,
		int targetObjectId,
		CancellationToken cancellationToken)
	{
		// Java parity: PlayerTeamCommandService ALLIANCE_SET_CAPTAIN -> PlayerAllianceService.changeLeader.
		var alliance = _playerAllianceRuntime.Resolve(player);
		if (alliance == null)
			return null;

		var newLeaderObjectId = targetObjectId == 0 ? player.ObjectId : targetObjectId;
		if (!_playerAllianceRuntime.HasMember(alliance.AllianceId, newLeaderObjectId))
			throw CreateInvalidTeamMemberException(player, newLeaderObjectId);

		var changedAllianceMemberObjectIds = _playerAllianceRuntime.GetMemberObjectIds(alliance.AllianceId);
		var leagueId = alliance.LeagueId;
		var newLeader = _playerAllianceRuntime.GetMember(alliance.AllianceId, newLeaderObjectId)?.Player;

		var plan = _playerAllianceRuntime.ChangeLeader(
			alliance.AllianceId,
			newLeaderObjectId,
			eventPlayerWasSpecified: true);
		if (plan == null)
			return null;

		if (leagueId != 0)
		{
			var leagueBroadcastPlan = _playerLeagueRuntime.BroadcastAllianceInfo(
				leagueId,
				skippedPlayerObjectId: null,
				_playerAllianceRuntime);
			if (leagueBroadcastPlan != null)
			{
				foreach (var intent in leagueBroadcastPlan.PacketIntents.OrderBy(intent => intent.Sequence))
					await SendLeaguePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
			}
		}

		var leagueTimeoutPlan = leagueId != 0 && newLeader != null
			? _playerLeagueRuntime.CreateAllianceLeaderChangeTimeoutPlan(
				leagueId,
				alliance.AllianceId,
				plan.NewLeaderObjectId,
				newLeader.Name,
				changedAllianceMemberObjectIds,
				_playerAllianceRuntime)
			: null;

		foreach (var intent in plan.AllianceInfoIntents)
			await SendAllianceLeaderPacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

		await DispatchAllianceLeaderChangeSystemMessagesAsync(
			plan,
			changedAllianceMemberObjectIds,
			leagueTimeoutPlan,
			cancellationToken);

		var demoteOldLeaderPlan = _playerAllianceRuntime.AssignViceCaptain(
			alliance.AllianceId,
			plan.OldLeaderObjectId,
			PlayerAllianceAssignType.DemoteCaptainToViceCaptain);
		if (demoteOldLeaderPlan != null)
		{
			var oldLeader = _playerAllianceRuntime.GetMember(alliance.AllianceId, plan.OldLeaderObjectId)?.Player;
			await DispatchAllianceViceCaptainAssignmentAsync(
				alliance,
				demoteOldLeaderPlan,
				PlayerAllianceAssignType.DemoteCaptainToViceCaptain,
				oldLeader?.Name ?? string.Empty,
				cancellationToken);
		}

		return plan;
	}

	private async Task DispatchAllianceViceCaptainAssignmentAsync(
		PlayerAllianceSnapshot alliance,
		PlayerAllianceViceCaptainAssignmentPlan plan,
		PlayerAllianceAssignType assignType,
		string eventPlayerName,
		CancellationToken cancellationToken)
	{
		if (plan.SystemMessageIntent != null)
			await SendAllianceViceCaptainPacketAsync(plan.SystemMessageIntent.RecipientObjectId, plan.SystemMessageIntent.Message, cancellationToken);

		var leagueInfoPlan = alliance.LeagueId != 0 && plan.WouldBroadcastLeague
			? _playerLeagueRuntime.CreateAllianceInfoFanout(
				alliance.LeagueId,
				alliance.AllianceId,
				GetAllianceViceCaptainMessageId(assignType),
				eventPlayerName,
				_playerAllianceRuntime)
			: null;

		if (leagueInfoPlan != null)
		{
			foreach (var intent in leagueInfoPlan.PacketIntents.OrderBy(intent => intent.Sequence))
				await SendAllianceViceCaptainPacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
		}
		else
		{
			foreach (var intent in plan.AllianceInfoIntents)
				await SendAllianceViceCaptainPacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
		}

		if (alliance.LeagueId != 0 && plan.WouldBroadcastLeague)
		{
			var leagueBroadcastPlan = _playerLeagueRuntime.BroadcastAllianceInfo(
				alliance.LeagueId,
				skippedPlayerObjectId: null,
				_playerAllianceRuntime);
			if (leagueBroadcastPlan != null)
			{
				foreach (var intent in leagueBroadcastPlan.PacketIntents.OrderBy(intent => intent.Sequence))
					await SendLeaguePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
			}
		}
	}

	private static int GetAllianceViceCaptainMessageId(PlayerAllianceAssignType assignType)
	{
		return assignType switch
		{
			PlayerAllianceAssignType.Promote => PlayerAllianceInfoPacketPlan.ViceCaptainPromoteMessageId,
			PlayerAllianceAssignType.Demote => PlayerAllianceInfoPacketPlan.ViceCaptainDemoteMessageId,
			_ => 0,
		};
	}

	private async Task DispatchAllianceLeaderChangeSystemMessagesAsync(
		PlayerAllianceLeaderChangePlan plan,
		IReadOnlyList<int> changedAllianceMemberObjectIds,
		PlayerLeagueLeaderChangeTimeoutPlan? leagueTimeoutPlan,
		CancellationToken cancellationToken)
	{
		foreach (var changedAllianceMemberObjectId in changedAllianceMemberObjectIds)
		{
			foreach (var intent in plan.SystemMessageIntents.Where(intent => intent.RecipientObjectId == changedAllianceMemberObjectId))
				await SendAllianceLeaderPacketAsync(intent.RecipientObjectId, intent.Message, cancellationToken);

			if (leagueTimeoutPlan == null)
				continue;

			foreach (var timeoutIntent in leagueTimeoutPlan.TimeoutIntents
				.Where(intent => intent.TriggeringChangedAllianceMemberObjectId == changedAllianceMemberObjectId)
				.OrderBy(intent => intent.PacketIntent.Sequence))
			{
				await SendLeaguePacketAsync(
					timeoutIntent.PacketIntent.RecipientObjectId,
					timeoutIntent.PacketIntent.CreatePacket(),
					cancellationToken);
			}
		}
	}

	private async Task SendAllianceLeaderPacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task HandleFriendStatusAsync(Player player, CmFriendStatus packet)
	{
		// Java parity: model/gameobjects/player/FriendList.setStatus(Status, PlayerCommonData).
		var previousStatus = player.FriendListStatus;
		var effectiveStatus = NormalizeFriendStatus(packet.Status);
		player.FriendListStatus = effectiveStatus;

		if (_connectionRegistry == null)
			return;

		foreach (var friend in player.Friends)
		{
			if (!_connectionRegistry.TryGetOnlinePlayerByName(friend.Name, out var friendPlayer) || friendPlayer == null)
				continue;

			var reciprocalFriend = UpdateFriendSnapshot(friendPlayer, player, effectiveStatus);
			if (reciprocalFriend == null)
				continue;

			await _connectionRegistry.SendPacketToPlayerAsync(
				friendPlayer.ObjectId,
				new SmFriendUpdate(reciprocalFriend, effectiveStatus, GetPlayerExperienceTable()));

			if (previousStatus == 0)
			{
				await _connectionRegistry.SendPacketToPlayerAsync(
					friendPlayer.ObjectId,
					new SmFriendNotify(SmFriendNotify.Login, player.Name));
			}
			else if (effectiveStatus == 0)
			{
				await _connectionRegistry.SendPacketToPlayerAsync(
					friendPlayer.ObjectId,
					new SmFriendNotify(SmFriendNotify.Logout, player.Name));
			}
		}
	}

	internal async Task HandleFriendAddAsync(Player requester, CmFriendAdd packet)
	{
		// Java parity: network/aion/clientpackets/CM_FRIEND_ADD.runImpl.
		var targetName = ConvertCharacterName(packet.TargetName);
		if (_connectionRegistry == null
			|| !_connectionRegistry.TryGetOnlinePlayerByName(targetName, out var target)
			|| target == null
			|| !target.IsOnline)
		{
			await SendPacketAsync(new SmFriendResponse(SmFriendResponse.TargetOffline));
			return;
		}

		if (requester.ObjectId == target.ObjectId)
		{
			await SendPacketAsync(SmSystemMessage.BuddyListBusy());
			return;
		}

		if (_options.Custom.FriendListGmRestrict
			&& ((target.AccessLevel > 0 && requester.AccessLevel == 0)
				|| (requester.AccessLevel > 0 && target.AccessLevel == 0)))
		{
			await SendPacketAsync(SmSystemMessage.BuddyCantAddWhenAskedQuestion(target.Name));
			return;
		}

		if (requester.Friends.Any(friend => friend.ObjectId == target.ObjectId))
		{
			await SendPacketAsync(new SmFriendResponse(SmFriendResponse.TargetAlreadyFriend));
			return;
		}

		if (!string.Equals(requester.Race, target.Race, StringComparison.OrdinalIgnoreCase))
		{
			await SendPacketAsync(new SmFriendResponse(SmFriendResponse.TargetNotFound));
			return;
		}

		if (requester.BlockedUsers.Any(blockedUser => blockedUser.ObjectId == target.ObjectId))
		{
			await SendPacketAsync(SmSystemMessage.BuddyListNoBlockedCharacter());
			return;
		}

		if (target.BlockedUsers.Any(blockedUser => blockedUser.ObjectId == requester.ObjectId))
		{
			await SendPacketAsync(new SmFriendResponse(SmFriendResponse.TargetBlockedYou));
			return;
		}

		if (IsFriendListFull(requester))
		{
			await SendPacketAsync(new SmFriendResponse(SmFriendResponse.ListFull));
			return;
		}

		if (IsFriendListFull(target))
		{
			await SendPacketAsync(new SmFriendResponse(SmFriendResponse.TargetListFull, target.Name));
			return;
		}

		if (target.Settings.DeniesFriendRequests())
		{
			await SendPacketAsync(SmSystemMessage.RejectedFriend(target.Name));
			return;
		}

		var pendingRequest = new PendingFriendRequest(requester.ObjectId, requester.Name);
		// Java parity: CM_FRIEND_ADD registers a RequestResponseHandler in
		// Player.getResponseRequester().putRequest before sending SM_QUESTION_WINDOW.
		if (!target.ResponseRequester.PutRequest(
			SmQuestionWindow.BuddyListAddBuddyRequest,
			new QuestionResponseRequest(requester.ObjectId, QuestionResponseRequestKind.FriendInvite, pendingRequest)))
		{
			await SendPacketAsync(SmSystemMessage.BuddyListBusy());
			return;
		}

		target.PendingFriendRequest = pendingRequest;
		var sent = await _connectionRegistry.SendPacketToPlayerAsync(
			target.ObjectId,
			new SmQuestionWindow(
				SmQuestionWindow.BuddyListAddBuddyRequest,
				requester.ObjectId,
				0,
				requester.Name,
				packet.Message));
		if (!sent)
		{
			target.ResponseRequester.Remove(SmQuestionWindow.BuddyListAddBuddyRequest);
			target.PendingFriendRequest = null;
		}
	}

	private async Task HandleChargeAllQuestionResponseAsync(Player player, CmQuestionResponse packet)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond, which removes
		// the ItemChargeService RequestResponseHandler before invoking accept/deny behavior.
		var pendingRequest = player.PendingChargeAllRequest;
		if (pendingRequest == null || packet.QuestionId != GetChargeAllQuestionId(pendingRequest.ChargeWay))
			return;

		var dispatch = player.ResponseRequester.Respond(packet.QuestionId, packet.Response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.ChargeAll)
		{
			player.PendingChargeAllRequest = null;
			return;
		}

		var request = dispatch.Request.Payload as PendingChargeAllRequest ?? pendingRequest;
		player.PendingChargeAllRequest = null;
		if (packet.Response == 0)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null || request.Items.Count == 0)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		InventoryItem? kinahUpdate = null;
		AbyssPointsAddPlan? abyssPointsPlan = null;
		switch (request.ChargeWay)
		{
			case 1:
				// Java parity: charge-all acceptRequest calls processPayment, which reaches
				// processKinahPayment for chargeWay 1.
				var chargeAllKinahPlan = ItemChargeService.CreateKinahPaymentPlan(
					inventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId),
					request.PaymentAmount);
				if (!chargeAllKinahPlan.Succeeded)
					return;
				kinahUpdate = chargeAllKinahPlan.KinahItemUpdate;
				break;
			case 2:
				// Java parity: ItemChargeService.startChargingEquippedItems acceptRequest
				// calls processPayment, which reaches processAPPayment for chargeWay 2.
				var chargeAllPaymentPlan = ItemChargeService.CreateAbyssPointPaymentPlan(
					player,
					request.PaymentAmount,
					CreateAbyssPointsOptions());
				if (!chargeAllPaymentPlan.Succeeded)
					return;
				abyssPointsPlan = chargeAllPaymentPlan.AbyssPointsPlan;
				break;
			default:
				return;
		}

		var chargedItems = request.Items
			.Select(pending =>
			{
				var currentItem = inventoryItems.FirstOrDefault(item => item.ObjectId == pending.ObjectId && item.Location == CubeStorageId);
				if (currentItem == null || currentItem.ItemId != pending.ItemId)
					return null;

				// Java parity: ItemChargeService.startChargingEquippedItems keeps the quoted
				// payment amount, then acceptRequest calls chargeItems(..., requirePayment=false),
				// so each item recalculates current chargeability before mutation.
				var currentPlan = ItemChargeService.CreateChargePlan(
					player,
					currentItem,
					itemTemplates,
					pending.Level,
					ignoreRankRequirement: false,
					requirePayment: false);
				if (currentPlan == null || currentPlan.ChargeWay != request.ChargeWay)
					return null;
				return CopyInventoryItem(currentItem, charge: currentPlan.TargetChargePoints);
			})
			.Where(item => item != null)
			.Cast<InventoryItem>()
			.ToArray();

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveItemChargeAllMutationAsync(player, chargedItems, kinahUpdate, abyssPointsPlan?.UpdatedRank);
		if (!saved)
			return;

		if (kinahUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, kinahUpdate);
			if (itemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
				await SendPacketAsync(new SmInventoryUpdateItem(kinahUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
		}
		if (abyssPointsPlan?.UpdatedRank != null)
		{
			player.AbyssRank = abyssPointsPlan.UpdatedRank;
			foreach (var playerPacket in abyssPointsPlan.PlayerPackets)
				await SendPacketAsync(playerPacket);
		}

		foreach (var chargedItem in chargedItems)
		{
			var pending = request.Items.First(item => item.ObjectId == chargedItem.ObjectId);
			ReplaceInventoryItem(inventoryItems, chargedItem);
			var itemTemplate = itemTemplates.GetItemTemplate(chargedItem.ItemId);
			if (itemTemplate == null)
				continue;

			if (GetChargeBarStep(pending.PreviousCharge) != GetChargeBarStep(chargedItem.Charge))
				await SendPacketAsync(new SmInventoryUpdateItem(chargedItem, itemTemplate, SmInventoryUpdateItem.Charge));

			var itemName = itemTemplate.GetClientName() ?? itemTemplate.Name;
			await SendPacketAsync(
				request.ChargeWay == 1
					? SmSystemMessage.ItemChargeSuccess(itemName, pending.Level)
					: SmSystemMessage.ItemCharge2Success(itemName, pending.Level));
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
		}

		player.InventoryItems = inventoryItems.ToArray();
		if (abyssPointsPlan != null)
			await ApplyAbyssRankChangedSideEffectsAsync(player, abyssPointsPlan.OldRank, staticData);
		if (chargedItems.Length > 0)
		{
			await SendPacketAsync(
				request.ChargeWay == 1
					? SmSystemMessage.ItemChargeAllComplete()
					: SmSystemMessage.ItemCharge2AllComplete());
		}
	}

	private static bool IsChargeAllQuestion(int questionId)
	{
		return questionId is SmQuestionWindow.ItemChargeAllConfirm or SmQuestionWindow.ItemCharge2AllConfirm;
	}

	private static bool IsRiftPortalQuestion(int questionId)
	{
		return questionId is SmQuestionWindow.DirectPortalPassConfirm or SmQuestionWindow.VortexPortalPassConfirm;
	}

	private static int GetChargeAllQuestionId(int chargeWay)
	{
		return chargeWay == 1 ? SmQuestionWindow.ItemChargeAllConfirm : SmQuestionWindow.ItemCharge2AllConfirm;
	}

	internal async Task HandleQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		// Java parity: network/aion/clientpackets/CM_QUESTION_RESPONSE.runImpl.
		if (responder.IsTrading && packet.Response != 0)
			CancelExchangeForQuestionAccept(responder);

		if (packet.QuestionId == SmQuestionWindow.SoulBoundItemConfirm)
		{
			await HandleSoulBindQuestionResponseAsync(responder, packet);
			return;
		}

		if (IsChargeAllQuestion(packet.QuestionId))
		{
			await HandleChargeAllQuestionResponseAsync(responder, packet);
			return;
		}

		if (IsRiftPortalQuestion(packet.QuestionId))
		{
			await HandleRiftPortalQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.RegisterBindstone)
		{
			await HandleKiskBindQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.UnionInviteMe)
		{
			await HandleLeagueInviteQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.PartyInvite)
		{
			await HandleGroupInviteQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.AllianceInvite)
		{
			await HandleAllianceInviteQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.DuelAcceptRequest)
		{
			await HandleDuelAcceptQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.DuelWithdrawRequest)
		{
			await HandleDuelWithdrawQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.TeleportToNpcConfirm)
		{
			await HandleTeleportToNpcQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.SummonPartyAcceptRequest)
		{
			await HandleRecallInstantQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.CraftAddSkillConfirm)
		{
			await HandleCraftSkillLearnQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.WarehouseExpandWarning)
		{
			await HandleStorageExpansionQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.AskRecoverExperience)
		{
			await HandleExperienceRecoveryQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.ExchangeAcceptRequest)
		{
			await HandleExchangeQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.InstanceDungeonWithDifficultyEnterConfirm)
		{
			await HandleBeshmundirDifficultyQuestionResponseAsync(responder, packet);
			return;
		}

		if (packet.QuestionId == SmQuestionWindow.VortexDefenderInvitation)
		{
			HandleVortexDefenderInvitationQuestionResponse(responder, packet);
			return;
		}

		if (packet.QuestionId != SmQuestionWindow.BuddyListAddBuddyRequest)
			return;

		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond, which removes the
		// RequestResponseHandler and invokes denyRequest for 0 or acceptRequest for nonzero responses.
		var dispatch = responder.ResponseRequester.Respond(packet.QuestionId, packet.Response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.FriendInvite)
		{
			responder.PendingFriendRequest = null;
			return;
		}

		var request = dispatch.Request.Payload as PendingFriendRequest ?? responder.PendingFriendRequest;
		if (request == null)
			return;

		responder.PendingFriendRequest = null;
		if (_connectionRegistry == null
			|| !_connectionRegistry.TryGetOnlinePlayerByName(request.RequesterName, out var requester)
			|| requester == null)
		{
			return;
		}

		if (packet.Response == 0)
		{
			await _connectionRegistry.SendPacketToPlayerAsync(
				requester.ObjectId,
				new SmFriendResponse(SmFriendResponse.TargetDenied, responder.Name));
			return;
		}

		await AcceptFriendRequestAsync(requester, responder);
	}

	private void HandleVortexDefenderInvitationQuestionResponse(Player responder, CmQuestionResponse packet)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond; the Vortex
		// accept callback remains metadata-only until live Vortex team/alliance mutation lands.
		// locationId resolution order:
		//   1. PendingVortexDefenderInvitationRequest.LocationId embedded when invitation was stored (principled)
		//   2. VortexInvasionRuntime.FindDefenderLocationId (if responder is already in defenders map)
		//   3. VortexLocationService.GetLocationByWorld(responder world) (zone position approximation)
		//   4. 0 fallback (observer will self-resolve from payload, see VortexDefenderAcceptanceRuntimeObserverService)
		var locationId = _vortexInvasionRuntime?.FindDefenderLocationId(responder.ObjectId)
			?? _defenderAcceptanceVortexLocationService?.GetLocationByWorld(responder.Position.WorldId)?.Id
			?? 0;
		VortexDefenderAcceptanceInputs? resolvedInputs = null;
		if (_vortexInvasionRuntime != null && _worldPlayerLookup != null && locationId != 0)
		{
			var snapshot = _vortexInvasionRuntime.GetSnapshot(locationId);
			resolvedInputs = new VortexDefenderAcceptanceInputResolverService().Resolve(snapshot, _worldPlayerLookup);
		}
		// Pass locationId=0 when unknown; the observer self-resolves from PendingVortexDefenderInvitationRequest.LocationId.
		var observerReport = new VortexDefenderAcceptanceRuntimeObserverService().Observe(
			locationId,
			responder,
			packet.QuestionId,
			packet.Response,
			existingDefenders: resolvedInputs?.ExistingDefenders,
			defenderAlliance: resolvedInputs?.DefenderAlliance);
		_vortexDefenderAcceptanceObserver?.Invoke(observerReport);
		_vortexDefenderInvitationResponseObserver?.Invoke(observerReport.TransitionReport.ConsumptionReport);
	}

	internal async Task<GroupInviteRequestResult?> HandleInviteToGroupAsync(
		Player inviter,
		CmInviteToGroup packet,
		CancellationToken cancellationToken = default)
	{
		// Java parity: network/aion/clientpackets/CM_INVITE_TO_GROUP.runImpl.
		if (inviter.IsInState(PlayerCreatureState.Dead) || inviter.LifeStats?.CurrentHp <= 0)
		{
			await SendPacketAsync(SmSystemMessage.PartyCantInviteWhenDead(), cancellationToken);
			return null;
		}

		if (_connectionRegistry == null
			|| !_connectionRegistry.TryGetOnlinePlayerByName(packet.PlayerName, out var invited)
			|| invited == null)
		{
			await SendPacketAsync(SmSystemMessage.NoSuchUser(packet.PlayerName), cancellationToken);
			return null;
		}

		if (invited.Settings.DeniesGroupRequests())
		{
			await SendPacketAsync(SmSystemMessage.RejectedInviteParty(invited.Name), cancellationToken);
			return null;
		}

		if (packet.InviteType == 12)
			return await HandleInviteToAllianceAsync(inviter, invited, cancellationToken);

		if (packet.InviteType == 28)
			return await HandleInviteToLeagueAsync(inviter, invited, cancellationToken);

		if (packet.InviteType != 0)
			return null;

		var result = _playerGroupInviteRequestService.SendInvite(inviter, invited);
		await SendPacketAsync(result.InviterMessage, cancellationToken);
		if (result.QuestionWindow != null)
			await _connectionRegistry.SendPacketToPlayerAsync(invited.ObjectId, result.QuestionWindow);
		return result;
	}

	internal async Task<DuelRequestPlan> HandleDuelRequestAsync(
		Player requester,
		CmDuelRequest packet,
		CancellationToken cancellationToken = default)
	{
		// Java parity: network/aion/clientpackets/CM_DUEL_REQUEST.runImpl resolves the target from
		// the active player's known-list, then calls DuelService.onDuelRequest.
		var target = TryGetOnlinePlayerByObjectId(packet.TargetObjectId);
		var plan = _playerDuelRequestService.SendDuelRequest(requester, target);
		if (plan.RejectionIntent != null)
			await SendDuelPacketAsync(plan.RejectionIntent.RecipientObjectId, plan.RejectionIntent.Packet, cancellationToken);

		foreach (var intent in plan.PacketIntents)
			await SendDuelPacketAsync(intent.RecipientObjectId, intent.Packet, cancellationToken);

		return plan;
	}

	internal async Task<ExchangeRequestPlan> HandleExchangeRequestAsync(
		Player requester,
		CmExchangeRequest packet,
		CancellationToken cancellationToken = default)
	{
		// Java parity: network/aion/clientpackets/CM_EXCHANGE_REQUEST.runImpl resolves the target
		// from world online players, validates the represented guards, then asks with SM_QUESTION_WINDOW.
		var target = TryGetOnlinePlayerByObjectId(packet.TargetObjectId);
		var plan = _playerExchangeRequestService.SendExchangeRequest(requester, target);
		foreach (var intent in plan.PacketIntents)
			await SendExchangePacketAsync(intent.RecipientObjectId, intent.Packet, cancellationToken);

		return plan;
	}

	internal async Task<ExchangeCancelPlan> HandleExchangeCancelAsync(
		Player activePlayer,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ExchangeService.cancelExchange -> returnItems sends inventory restore packets before clearing exchange.
		await RestoreExchangeItemsToInventoryUiAsync(activePlayer, cancellationToken);
		var plan = _playerExchangeRequestService.CancelExchange(activePlayer, TryGetOnlinePlayerByObjectId);
		foreach (var intent in plan.PacketIntents)
			await SendExchangePacketAsync(intent.RecipientObjectId, intent.Packet, cancellationToken);

		return plan;
	}

	private async Task RestoreExchangeItemsToInventoryUiAsync(Player player, CancellationToken cancellationToken = default)
	{
		// Java parity: ExchangeService.returnItems sends SM_INVENTORY_ADD_ITEM or SM_INVENTORY_UPDATE_ITEM to restore
		// committed items to inventory display when exchange is cancelled. Routes to the item owner (self or partner).
		if (player.ExchangeItems.Count == 0)
			return;

		var templates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		if (templates == null)
			return;

		foreach (var (itemObjectId, committedCount) in player.ExchangeItems)
		{
			var item = player.InventoryItems.FirstOrDefault(i => i.ObjectId == itemObjectId);
			if (item == null)
				continue;

			var template = templates.GetItemTemplate(item.ItemId);
			if (template == null)
				continue;

			// Java parity: INC_PLAYER_EXCHANGE_GET_BACK (0x23) restores the full stack.
			await SendExchangePacketAsync(player.ObjectId, new SmInventoryUpdateItem(item, template, SmInventoryUpdateItem.PlayerExchangeGetBack), cancellationToken);
		}
	}

	private async Task HandleGroupDistributionAsync(Player player, long amount, byte partyType, CancellationToken cancellationToken = default)
	{
		// Java parity: CM_GROUP_DISTRIBUTION.runImpl. amount < 2 returns immediately.
		if (amount < 2)
			return;

		// Java parity: PlayerRestrictions.canTrade(player) gate. canTrade (duel/restriction state) is not modeled in the
		// port yet; documented omission — it only blocks distribution in transient restricted states.

		// Java parity: CM_GROUP_DISTRIBUTION partyType routing:
		//   1 -> PlayerGroupService.distributeKinah (not in alliance) OR PlayerAllianceService.distributeKinahInGroup (alliance sub-group).
		//   2 -> PlayerAllianceService.distributeKinah (whole alliance).
		//   3 -> LeagueService.distributeKinah (league; not modeled, deferred).
		var teamId = player.CurrentTeamId;
		IReadOnlyList<Player>? onlineMembers = null;
		var isTeamMember = false;
		switch (partyType)
		{
			case 1 when player.TeamMembership == PlayerTeamMembership.Group && teamId != 0:
				onlineMembers = _playerGroupRuntime.GetOnlineMemberPlayers(teamId);
				isTeamMember = _playerGroupRuntime.HasMember(teamId, player.ObjectId);
				break;
			case 1 when player.TeamMembership == PlayerTeamMembership.Alliance && teamId != 0:
				// Java parity: distributeKinahInGroup — the distributor's alliance sub-group only.
				var allianceGroupId = _playerAllianceRuntime.GetMemberAllianceGroupId(teamId, player.ObjectId);
				if (allianceGroupId != null)
				{
					onlineMembers = _playerAllianceRuntime.GetOnlineMemberPlayersByGroupId(teamId, allianceGroupId.Value);
					isTeamMember = _playerAllianceRuntime.HasMember(teamId, player.ObjectId);
				}

				break;
			case 2 when player.TeamMembership == PlayerTeamMembership.Alliance && teamId != 0:
				onlineMembers = _playerAllianceRuntime.GetOnlineMemberPlayers(teamId);
				isTeamMember = _playerAllianceRuntime.HasMember(teamId, player.ObjectId);
				break;
		}

		if (onlineMembers == null)
			return;

		// Java parity: TeamKinahDistributionEvent — checkCondition (hasMember) + handleEvent decision.
		var distributorKinahItem = player.InventoryItems.FirstOrDefault(i => i.ItemId == KinahItemId && i.Location == CubeStorageId);
		var distributorKinah = distributorKinahItem?.Count ?? 0L;
		var plan = GroupKinahDistributionPlanService.Plan(amount, distributorKinah, onlineMembers.Count, isTeamMember);

		if (plan.Outcome == GroupKinahDistributionOutcome.NotEnoughMoney)
		{
			// Java parity: STR_NOT_ENOUGH_MONEY to the distributor.
			await SendToPlayerOrActiveAsync(player.ObjectId, SmSystemMessage.NotEnoughMoney(), cancellationToken);
			return;
		}

		if (plan.Outcome != GroupKinahDistributionOutcome.Distribute || distributorKinahItem == null)
			return;

		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		var kinahTemplate = itemTemplates?.GetItemTemplate(KinahItemId);

		// Java parity: distributor tryDecreaseKinah(amount) -> Storage.decreaseKinah default DEC_KINAH_BUY.
		var decreased = CopyInventoryItem(distributorKinahItem, count: distributorKinahItem.Count - amount);
		ReplaceInventoryItemFor(player, decreased);
		if (kinahTemplate != null)
			await SendPacketAsync(new SmInventoryUpdateItem(decreased, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy), cancellationToken);

		// Java parity: for each online member -> increaseKinah(rewardPerPlayer) (default INC_KINAH_COLLECT) + split message.
		// The distributor is part of onlineMembers, so they also receive their share and the ME_TO_B message.
		foreach (var member in onlineMembers)
		{
			var memberKinah = member.InventoryItems.FirstOrDefault(i => i.ItemId == KinahItemId && i.Location == CubeStorageId);
			if (memberKinah != null)
			{
				var increased = CopyInventoryItem(memberKinah, count: memberKinah.Count + plan.RewardPerPlayer);
				ReplaceInventoryItemFor(member, increased);
				if (kinahTemplate != null)
					await SendToPlayerOrActiveAsync(member.ObjectId, new SmInventoryUpdateItem(increased, kinahTemplate, SmInventoryUpdateItem.IncreaseKinahCollect), cancellationToken);
			}

			var message = member.ObjectId == player.ObjectId
				? SmSystemMessage.MsgSplitMeToB(amount, plan.OnlineMemberCount, plan.RewardPerPlayer)
				: SmSystemMessage.MsgSplitBToMe(player.Name, amount, plan.OnlineMemberCount, plan.RewardPerPlayer);
			await SendToPlayerOrActiveAsync(member.ObjectId, message, cancellationToken);
		}
	}

	private async Task HandleQuestShareAsync(Player player, int questId)
	{
		// Java parity: CM_QUEST_SHARE.runImpl. Resolves the quest template, the sharer's quest state, and the
		// current-team online members, then fans out the QuestSharePlanService decision (1100001/1100000/1100005
		// messages, SM_QUEST_ACTION.SHARE, and 1100002/1100003 per-member messages).
		var questTemplates = _runtimeContext?.DataManager?.StaticData.NearbyQuestTemplates;
		NearbyQuestTemplateSummary? template = null;
		if (questTemplates != null)
			questTemplates.TryGetQuest(questId, out template);

		var questState = player.Quests.FirstOrDefault(quest => quest.QuestId == questId);

		// Java: player.getCurrentGroup() — the whole group OR whole alliance (not the alliance sub-group); null when solo.
		var teamId = player.CurrentTeamId;
		IReadOnlyList<Player> currentTeamOnlineMembers = player.TeamMembership switch
		{
			PlayerTeamMembership.Group when teamId != 0 => _playerGroupRuntime.GetOnlineMemberPlayers(teamId),
			PlayerTeamMembership.Alliance when teamId != 0 => _playerAllianceRuntime.GetOnlineMemberPlayers(teamId),
			_ => Array.Empty<Player>(),
		};

		var plan = QuestSharePlanService.Plan(
			player,
			questId,
			template,
			questState,
			currentTeamOnlineMembers,
			// Java: QuestService.checkStartConditions(member, questId, false).
			member => questTemplates != null
				&& NearbyQuestStartConditionService.CheckNearbyStartConditions(member, questId, questTemplates).CanStart);

		foreach (var instruction in plan.Instructions)
			await SendToPlayerOrActiveAsync(instruction.RecipientObjectId, instruction.Packet);
	}

	private async Task HandleDeleteQuestAsync(Player player, int questId, CancellationToken cancellationToken = default)
	{
		// Java parity: CM_DELETE_QUEST.runImpl -> timer-clear SM_QUEST_ACTION(questId, 0),
		// then QuestService.abandonQuest(player, questId).
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var questTemplates = staticData?.NearbyQuestTemplates;
		NearbyQuestTemplateSummary? template = null;
		if (questTemplates != null)
			questTemplates.TryGetQuest(questId, out template);

		var now = DateTimeOffset.Now;
		var currentEpochSeconds = now.ToUnixTimeSeconds() > int.MaxValue ? int.MaxValue : (int)now.ToUnixTimeSeconds();
		Func<int, bool>? hasQuestHandler = staticData?.QuestHandlers == null
			? null
			: staticData.QuestHandlers.IsHaveHandler;
		var result = QuestAbandonService.Abandon(
			player,
			questId,
			template,
			currentEpochSeconds,
			questTemplates,
			NpcFactionDailyResetService.GetNextResetEpochSeconds(now, _options),
			hasQuestHandler: hasQuestHandler);
		if (_playerEnterWorldService != null)
			await _playerEnterWorldService.PersistQuestAbandonAsync(player, result, cancellationToken);

		foreach (var packet in result.TimerPackets)
			await SendPacketAsync(packet, cancellationToken);

		foreach (var packet in result.NpcFactionDailyQuestPackets)
			await SendPacketAsync(packet, cancellationToken);

		foreach (var deletion in result.WorkItemDeletions)
		{
			await SendPacketAsync(new SmDeleteItem(deletion.Item.ObjectId, deletion.DeleteType), cancellationToken);
			await SendPacketAsync(SmCubeUpdate.CubeSizeSnapshot(
				deletion.CubeItemCountAfterDeletion,
				player.NpcExpands,
				player.QuestExpands,
				player.ItemExpands), cancellationToken);
		}

		if (result.WorkOrderRecipeId is { } recipeId)
		{
			var recipeDeleted = _playerEnterWorldService == null
				? DeleteRecipeInMemory(player, recipeId)
				: await _playerEnterWorldService.DeleteRecipeAsync(player, recipeId, cancellationToken);
			if (recipeDeleted)
				await SendPacketAsync(new SmRecipeDelete(recipeId), cancellationToken);
		}

		if (result.AbandonPacket != null)
			await SendPacketAsync(result.AbandonPacket, cancellationToken);

		// Java also cancels the QUEST_TIMER task-map entry; C# currently emits the timer-clear packet only.
		if (result.NearbyQuestRefreshRequired)
			await SendNearbyQuestRefreshAsync(player, cancellationToken);
	}

	private async Task SendNearbyQuestRefreshAsync(Player player, CancellationToken cancellationToken = default)
	{
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var worldMapStates = _runtimeContext?.WorldMapStates;
		if (worldMapStates == null || staticData?.NearbyQuestTemplates == null)
			return;

		worldMapStates.TryGetWorldMapInstance(player.Position.WorldId, player.Position.InstanceId, out var mapInstance);
		var nearbyPlan = NearbyQuestRefreshPlanService.CreatePlan(player, mapInstance, staticData.NearbyQuestTemplates);
		var packetPlan = NearbyQuestRefreshPlanService.CreatePacketFactoryPlan(nearbyPlan);
		if (packetPlan.Packet != null)
			await SendPacketAsync(packetPlan.Packet, cancellationToken);
	}

	private async Task SendToPlayerOrActiveAsync(int recipientObjectId, GameServerPacket packet, CancellationToken cancellationToken = default)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task ExecuteExchangeTradeAsync(Player player, CancellationToken cancellationToken = default)
	{
		// Java parity: ExchangeService.performTrade. Items move between the two players' inventories and kinah is exchanged.
		// Full-stack and partial-stack item trades + kinah are live. Partial-stack splits require an IDFactory to allocate
		// the receiver's new item id; if none is available we abort cleanly to avoid item loss/duplication.
		var partner = TryGetOnlinePlayerByObjectId(player.CurrentExchangePartnerObjectId);
		if (partner == null)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;

		var playerItems = ResolveCommittedExchangeItems(player);
		var partnerItems = ResolveCommittedExchangeItems(partner);

		// A partial-stack split (committed < stack count) needs the IDFactory; without it, abort cleanly.
		var hasPartialStack = playerItems.Any(entry => entry.Committed < entry.Item.Count)
			|| partnerItems.Any(entry => entry.Committed < entry.Item.Count);
		if (hasPartialStack && _idFactory == null)
		{
			_logger.LogInformation(
				"Deferring partial-stack exchange trade between {PlayerObjectId} and {PartnerObjectId} (no IDFactory); cancelling.",
				player.ObjectId,
				partner.ObjectId);
			await RestoreExchangeItemsToInventoryUiAsync(player, cancellationToken);
			await RestoreExchangeItemsToInventoryUiAsync(partner, cancellationToken);
			await SendExchangePacketAsync(player.ObjectId, new SmExchangeConfirmation(SmExchangeConfirmation.Canceled), cancellationToken);
			await SendExchangePacketAsync(partner.ObjectId, new SmExchangeConfirmation(SmExchangeConfirmation.Canceled), cancellationToken);
			_playerExchangeRequestService.CancelExchange(player, TryGetOnlinePlayerByObjectId);
			return;
		}

		// Java parity: validateInventorySize — each player needs free cube slots for the items they will receive.
		if (itemTemplates != null)
		{
			var playerFreeSlots = InventoryCapacity.GetFreeCubeSlots(player, itemTemplates);
			var partnerFreeSlots = InventoryCapacity.GetFreeCubeSlots(partner, itemTemplates);
			if (playerFreeSlots < partnerItems.Count)
			{
				await SendExchangePacketAsync(player.ObjectId, new SmSystemMessage(1300359), cancellationToken); // CANT_EXCHANGE_HEAVY
				await SendExchangePacketAsync(partner.ObjectId, new SmSystemMessage(1300357), cancellationToken); // PARTNER_TOO_HEAVY
				await CancelExchangeAfterFailedTradeAsync(player, partner, cancellationToken);
				return;
			}
			if (partnerFreeSlots < playerItems.Count)
			{
				await SendExchangePacketAsync(partner.ObjectId, new SmSystemMessage(1300359), cancellationToken);
				await SendExchangePacketAsync(player.ObjectId, new SmSystemMessage(1300357), cancellationToken);
				await CancelExchangeAfterFailedTradeAsync(player, partner, cancellationToken);
				return;
			}
		}

		// Kinah net change validation (each player's own kinah row count must stay non-negative).
		var kinahTemplate = itemTemplates?.GetItemTemplate(KinahItemId);
		var playerKinah = player.InventoryItems.FirstOrDefault(i => i.ItemId == KinahItemId && i.Location == CubeStorageId);
		var partnerKinah = partner.InventoryItems.FirstOrDefault(i => i.ItemId == KinahItemId && i.Location == CubeStorageId);
		var playerNet = partner.ExchangeKinah - player.ExchangeKinah;
		var partnerNet = player.ExchangeKinah - partner.ExchangeKinah;
		var playerKinahNewCount = (playerKinah?.Count ?? 0L) + playerNet;
		var partnerKinahNewCount = (partnerKinah?.Count ?? 0L) + partnerNet;
		if (playerKinahNewCount < 0 || partnerKinahNewCount < 0)
		{
			await CancelExchangeAfterFailedTradeAsync(player, partner, cancellationToken);
			return;
		}

		// Java parity: performTrade sends SM_EXCHANGE_CONFIRMATION(0) to both after removal, before delivery.
		await SendExchangePacketAsync(player.ObjectId, new SmExchangeConfirmation(SmExchangeConfirmation.Success), cancellationToken);
		await SendExchangePacketAsync(partner.ObjectId, new SmExchangeConfirmation(SmExchangeConfirmation.Success), cancellationToken);

		// Transfer items both directions (full-stack ownership move or partial-stack split).
		await TransferTradeItemsAsync(player, partner, playerItems, itemTemplates, cancellationToken);
		await TransferTradeItemsAsync(partner, player, partnerItems, itemTemplates, cancellationToken);

		// Apply kinah exchange.
		if (playerNet != 0 && playerKinah != null && kinahTemplate != null)
		{
			var update = CopyInventoryItem(playerKinah, count: playerKinahNewCount);
			ReplaceInventoryItemFor(player, update);
			await SendExchangePacketAsync(player.ObjectId, new SmInventoryUpdateItem(update, kinahTemplate,
				playerNet > 0 ? SmInventoryUpdateItem.PlayerExchangeGet : SmInventoryUpdateItem.DecreaseKinahBuy), cancellationToken);
		}
		if (partnerNet != 0 && partnerKinah != null && kinahTemplate != null)
		{
			var update = CopyInventoryItem(partnerKinah, count: partnerKinahNewCount);
			ReplaceInventoryItemFor(partner, update);
			await SendExchangePacketAsync(partner.ObjectId, new SmInventoryUpdateItem(update, kinahTemplate,
				partnerNet > 0 ? SmInventoryUpdateItem.PlayerExchangeGet : SmInventoryUpdateItem.DecreaseKinahBuy), cancellationToken);
		}

		// Java parity: cleanUpExchanges resets exchange state for both players (also clears the item/kinah baskets).
		_playerExchangeRequestService.CancelExchange(player, TryGetOnlinePlayerByObjectId);
	}

	private async Task CancelExchangeAfterFailedTradeAsync(Player player, Player partner, CancellationToken cancellationToken)
	{
		// Java parity: performTrade failed-validation branch calls cleanUpExchanges(true, ...) and restores items to UI.
		await RestoreExchangeItemsToInventoryUiAsync(player, cancellationToken);
		await RestoreExchangeItemsToInventoryUiAsync(partner, cancellationToken);
		_playerExchangeRequestService.CancelExchange(player, TryGetOnlinePlayerByObjectId);
	}

	private static List<(InventoryItem Item, long Committed)> ResolveCommittedExchangeItems(Player owner)
	{
		// Resolve each committed objectId to the live cube InventoryItem and its committed count.
		var resolved = new List<(InventoryItem, long)>();
		foreach (var (itemObjectId, committed) in owner.ExchangeItems)
		{
			var item = owner.InventoryItems.FirstOrDefault(i => i.ObjectId == itemObjectId && i.Location == CubeStorageId);
			if (item != null)
				resolved.Add((item, committed));
		}

		return resolved;
	}

	private async Task TransferTradeItemsAsync(
		Player giver,
		Player receiver,
		IReadOnlyList<(InventoryItem Item, long Committed)> committedItems,
		ItemTemplateTable? itemTemplates,
		CancellationToken cancellationToken)
	{
		// Java parity: ExchangeService.removeItemsFromInventory (giver) + putItemToInventory (receiver).
		foreach (var (item, committed) in committedItems)
		{
			// Java parity: putItemToInventory sets equipmentSlot(0) and unwraps packCount (positive -> negative) before add.
			var unwrappedPackCount = item.PackCount > 0 ? item.PackCount * -1 : item.PackCount;
			var template = itemTemplates?.GetItemTemplate(item.ItemId);

			if (committed < item.Count && _idFactory != null)
			{
				// Java parity: removeItemsFromInventory decreaseItemCount branch (committed < stack) — the giver keeps the
				// remainder; addItem already created a fresh-id ExchangeItem (newItem) holding the committed count, which
				// putItemToInventory then adds to the receiver.
				var reducedSource = CopyInventoryItem(item, count: item.Count - committed);
				ReplaceInventoryItemFor(giver, reducedSource);

				var receivedSplit = CopyInventoryItem(
					item,
					objectId: _idFactory.NextId(),
					location: CubeStorageId,
					slot: FirstAvailableSlot,
					count: committed,
					ownerId: receiver.ObjectId,
					isEquipped: false,
					packCount: unwrappedPackCount);
				receiver.InventoryItems = receiver.InventoryItems.Append(receivedSplit).ToArray();

				// Persist atomically: UPDATE the giver's source count + INSERT the receiver's new row (keyed by its OwnerId).
				if (_playerEnterWorldService != null)
				{
					var saved = await _playerEnterWorldService.SaveItemSplitMutationAsync(giver, reducedSource, receivedSplit, cancellationToken);
					if (!saved)
						_logger.LogWarning("Item {ItemObjectId} partial-stack split persistence failed during trade.", item.ObjectId);
				}

				// Java parity: decreaseItemCount sends SM_INVENTORY_UPDATE_ITEM(DEC_ITEM_USE = 0x16) for the reduced source.
				if (template != null)
					await SendExchangePacketAsync(giver.ObjectId, new SmInventoryUpdateItem(reducedSource, template, SmInventoryUpdateItem.DecreaseItemUse), cancellationToken);

				if (template != null)
					await SendExchangePacketAsync(receiver.ObjectId, SmInventoryAddItem.CreatePlayerExchangeGet(receivedSplit, template), cancellationToken);

				continue;
			}

			// Full-stack transfer: the same item row changes owner.
			// Persist the atomic ownership transfer first (authoritative); in-memory move follows.
			if (_playerEnterWorldService != null)
			{
				var transferred = await _playerEnterWorldService.TransferItemOwnershipAsync(
					item.ObjectId, giver.ObjectId, receiver.ObjectId, CubeStorageId, FirstAvailableSlot, cancellationToken);
				if (!transferred)
					_logger.LogWarning("Item {ItemObjectId} ownership transfer persistence failed during trade.", item.ObjectId);
			}

			// Remove from giver in-memory list WITHOUT TrackDeletedItem (the row now belongs to the receiver; a logout delete would wrongly remove it).
			giver.InventoryItems = giver.InventoryItems.Where(i => i.ObjectId != item.ObjectId).ToArray();
			await SendExchangePacketAsync(giver.ObjectId, new SmDeleteItem(item.ObjectId, SmDeleteItem.MoveDeleteType), cancellationToken);

			var received = CopyInventoryItem(
				item,
				location: CubeStorageId,
				slot: FirstAvailableSlot,
				ownerId: receiver.ObjectId,
				isEquipped: false,
				packCount: unwrappedPackCount);
			receiver.InventoryItems = receiver.InventoryItems.Append(received).ToArray();

			if (template != null)
				await SendExchangePacketAsync(receiver.ObjectId, SmInventoryAddItem.CreatePlayerExchangeGet(received, template), cancellationToken);
		}

		if (committedItems.Count > 0)
		{
			await SendExchangePacketAsync(giver.ObjectId, SmCubeUpdate.CubeSize(giver), cancellationToken);
			await SendExchangePacketAsync(receiver.ObjectId, SmCubeUpdate.CubeSize(receiver), cancellationToken);
		}
	}

	private static void ReplaceInventoryItemFor(Player owner, InventoryItem replacement)
	{
		var items = owner.InventoryItems.ToList();
		ReplaceInventoryItem(items, replacement);
		owner.InventoryItems = items.ToArray();
	}

	internal async Task<ExchangeLockPlan> HandleExchangeLockAsync(
		Player activePlayer,
		CancellationToken cancellationToken = default)
	{
		var plan = _playerExchangeRequestService.LockExchange(activePlayer, TryGetOnlinePlayerByObjectId);
		foreach (var intent in plan.PacketIntents)
			await SendExchangePacketAsync(intent.RecipientObjectId, intent.Packet, cancellationToken);

		return plan;
	}

	internal async Task<ExchangeConfirmPlan> HandleExchangeOkAsync(
		Player activePlayer,
		CancellationToken cancellationToken = default)
	{
		var plan = _playerExchangeRequestService.ConfirmExchange(activePlayer, TryGetOnlinePlayerByObjectId);
		foreach (var intent in plan.PacketIntents)
			await SendExchangePacketAsync(intent.RecipientObjectId, intent.Packet, cancellationToken);

		if (plan.Status == ExchangeConfirmStatus.TradeExecutionBlocked)
			await ExecuteExchangeTradeAsync(activePlayer, cancellationToken);

		return plan;
	}

	private async Task HandleExchangeAddKinahAsync(Player player, CmExchangeAddKinah packet)
	{
		// Java parity: network/aion/clientpackets/CM_EXCHANGE_ADD_KINAH.runImpl -> ExchangeService.addKinah.
		if (!player.IsTrading || player.IsExchangeLocked)
			return;

		var partnerObjectId = player.CurrentExchangePartnerObjectId;
		if (partnerObjectId == 0)
			return;

		var inventoryKinah = player.InventoryItems
			.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId)?.Count ?? 0L;
		var plan = ExchangeAddKinahPlanService.CreatePlan(packet.KinahCount, inventoryKinah, player.ExchangeKinah);
		if (!plan.ShouldSendToSelf)
			return;

		player.ExchangeKinah += plan.CountToAdd;
		if (plan.SelfPacket != null)
			await SendPacketAsync(plan.SelfPacket);
		if (plan.OtherPacket != null && _connectionRegistry != null)
			await _connectionRegistry.SendPacketToPlayerAsync(partnerObjectId, plan.OtherPacket);
	}

	private async Task HandleExchangeAddItemAsync(Player player, CmExchangeAddItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_EXCHANGE_ADD_ITEM.runImpl -> ExchangeService.addItem.
		if (!player.IsTrading || player.IsExchangeLocked)
			return;

		var partnerObjectId = player.CurrentExchangePartnerObjectId;
		if (partnerObjectId == 0)
			return;

		if (player.ExchangeItems.Count >= 18)
			return; // Java parity: Exchange.isExchangeListFull() caps at 18 items.

		var sourceItem = player.InventoryItems.FirstOrDefault(
			i => i.ObjectId == packet.ItemObjectId && i.Location == CubeStorageId && !i.IsEquipped);
		if (sourceItem == null)
			return;

		var templates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		var template = templates?.GetItemTemplate(sourceItem.ItemId);
		if (template == null)
			return;

		// Java parity: isItemRestrictedFrom check — non-tradeable items cannot enter exchange.
		if (!template.IsTradeable)
			return;

		// Track committed item count.
		var alreadyCommitted = player.ExchangeItems.GetValueOrDefault(sourceItem.ObjectId, 0L);
		var requestedCount = Math.Max(1, packet.ItemCount);
		var available = sourceItem.Count - alreadyCommitted;
		var countToAdd = Math.Min(available, requestedCount);
		if (countToAdd <= 0)
			return;

		var newCommitted = alreadyCommitted + countToAdd;
		player.ExchangeItems[sourceItem.ObjectId] = newCommitted;

		// Java parity: show reduced inventory count or delete if full stack committed.
		if (newCommitted >= sourceItem.Count || template.MaxStackCount <= 1)
		{
			// Java parity: SM_DELETE_ITEM(objectId, PUT_TO_EXCHANGE) to self.
			await SendPacketAsync(new SmDeleteItem(sourceItem.ObjectId, SmDeleteItem.PutToExchangeDeleteType));
		}
		else
		{
			// Java parity: fake item showing remaining count via SM_INVENTORY_UPDATE_ITEM(PutToExchange).
			var remainingCountItem = CopyInventoryItem(sourceItem, count: sourceItem.Count - newCommitted);
			await SendPacketAsync(new SmInventoryUpdateItem(remainingCountItem, template, SmInventoryUpdateItem.PutToExchange));
		}

		// Java parity: SM_EXCHANGE_ADD_ITEM(0, item, self) + SM_EXCHANGE_ADD_ITEM(1, item, partner).
		// The exchange item shown has 'newCommitted' count, matching Java ExchangeItem.getItemCount().
		var exchangeDisplayItem = CopyInventoryItem(sourceItem, count: newCommitted);
		await SendPacketAsync(new SmExchangeAddItem(SmExchangeAddItem.ActionSelf, exchangeDisplayItem, template));
		if (_connectionRegistry != null)
			await _connectionRegistry.SendPacketToPlayerAsync(partnerObjectId,
				new SmExchangeAddItem(SmExchangeAddItem.ActionOther, exchangeDisplayItem, template));
	}

	private async Task SendDuelPacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken = default)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task SendExchangePacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken = default)
	{
		if (_connectionRegistry != null && await _connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet))
			return;

		if (_activePlayer?.ObjectId == recipientObjectId)
			await SendPacketAsync(packet, cancellationToken);
	}

	private async Task<GroupInviteRequestResult?> HandleInviteToAllianceAsync(
		Player inviter,
		Player invited,
		CancellationToken cancellationToken)
	{
		// Java parity: CM_INVITE_TO_GROUP invite type 12 dispatches PlayerAllianceService.inviteToAlliance.
		var result = _playerAllianceInviteRequestService.SendInvite(
			inviter,
			invited,
			_playerGroupRuntime,
			_playerAllianceRuntime,
			TryGetOnlinePlayerByObjectId);
		if (result.RejectionMessage != null)
			await SendPacketAsync(result.RejectionMessage, cancellationToken);

		foreach (var message in result.RequesterMessages)
			await SendGroupInvitePacketAsync(inviter.ObjectId, message, cancellationToken);

		if (result.QuestionWindow != null && result.Request != null)
			await SendGroupInvitePacketAsync(result.Request.RequestTargetObjectId, result.QuestionWindow, cancellationToken);

		return null;
	}

	private async Task<GroupInviteRequestResult?> HandleInviteToLeagueAsync(
		Player inviter,
		Player invited,
		CancellationToken cancellationToken)
	{
		// Java parity: CM_INVITE_TO_GROUP invite type 28 dispatches LeagueService.inviteToLeague.
		var planner = new PlayerLeagueInvitePlanner();
		var firstChecks = planner.CreateCanInviteFirstChecksPlan(inviter, invited);
		if (firstChecks.SystemMessageIntent != null)
		{
			await SendPacketAsync(firstChecks.SystemMessageIntent.Message, cancellationToken);
			return null;
		}

		var allianceChecks = planner.CreateCanInviteAllianceChecksPlan(inviter, invited, _playerLeagueRuntime);
		if (allianceChecks.SystemMessageIntent != null)
		{
			await SendPacketAsync(allianceChecks.SystemMessageIntent.Message, cancellationToken);
			return null;
		}

		var setupPlan = planner.CreateRequestSetupPlan(inviter, invited, _playerAllianceRuntime);
		var requestTarget = TryGetOnlinePlayerByObjectId(setupPlan.RequestTargetObjectId);
		if (requestTarget == null)
			return null;

		var pendingPlan = planner.TryPutPendingRequest(requestTarget, setupPlan);
		if (!pendingPlan.Registered)
			return null;

		foreach (var intent in setupPlan.RequesterSystemMessages)
			await SendGroupInvitePacketAsync(intent.RecipientObjectId, intent.Message, cancellationToken);
		await SendGroupInvitePacketAsync(
			setupPlan.QuestionWindowIntent.RecipientObjectId,
			setupPlan.QuestionWindowIntent.QuestionWindow,
			cancellationToken);
		return null;
	}

	private async Task<GroupInviteResponseResult> HandleGroupInviteQuestionResponseAsync(
		Player invited,
		CmQuestionResponse packet)
	{
		var result = _playerGroupInviteRequestService.HandleResponse(
			invited,
			packet.QuestionId,
			packet.Response,
			_playerGroupRuntime,
			() => _idFactory?.NextId() ?? 0,
			TryGetOnlinePlayerByObjectId);

		if (result.Status == GroupInviteResponseStatus.Denied && result.Request != null && result.DenyMessage != null)
			await SendGroupInvitePacketAsync(result.Request.InviterObjectId, result.DenyMessage);

		if (result.Status == GroupInviteResponseStatus.Accepted)
			await DispatchFindGroupJoinedTeamPlansAsync(result.FindGroupJoinedTeamPlans);

		if (result.Status == GroupInviteResponseStatus.Accepted && result.EnteredPacketPlan != null)
			await SendGroupEnteredPlanAsync(result.EnteredPacketPlan);

		return result;
	}

	private async Task<AllianceInviteResponseResult> HandleAllianceInviteQuestionResponseAsync(
		Player responder,
		CmQuestionResponse packet)
	{
		var result = _playerAllianceInviteRequestService.HandleResponse(
			responder,
			packet.QuestionId,
			packet.Response,
			_playerGroupRuntime,
			_playerAllianceRuntime,
			() => _idFactory?.NextId() ?? 0,
			TryGetOnlinePlayerByObjectId);

		if (result.Status is AllianceInviteResponseStatus.Denied or AllianceInviteResponseStatus.Rejected
			&& result.Request != null
			&& result.Message != null)
		{
			await SendGroupInvitePacketAsync(result.Request.RequesterObjectId, result.Message);
		}

		if (result.Status == AllianceInviteResponseStatus.Accepted)
		{
			await DispatchFindGroupJoinedTeamPlansAsync(result.FindGroupJoinedTeamPlans);

			foreach (var enteredPlan in result.EnteredPlans)
			{
				foreach (var intent in enteredPlan.PacketIntents.OrderBy(intent => intent.Sequence))
				{
					var packetToSend = intent.CreatePacket();
					if (packetToSend != null)
						await SendGroupInvitePacketAsync(intent.RecipientObjectId, packetToSend);
				}
			}
		}

		return result;
	}

	private async Task DispatchFindGroupJoinedTeamPlansAsync(IReadOnlyList<FindGroupJoinedTeamPlan> plans)
	{
		if (_connectionRegistry == null)
			return;

		foreach (var plan in plans)
			await DispatchFindGroupJoinedTeamPlanAsync(plan);
	}

	private async Task DispatchFindGroupJoinedTeamPlanAsync(FindGroupJoinedTeamPlan plan)
	{
		await DispatchFindGroupWorldBroadcastIntentAsync(plan.ApplicationRemoval.WorldBroadcastIntent);
		await DispatchFindGroupWorldBroadcastIntentAsync(plan.SoloRecruitmentRemoval.WorldBroadcastIntent);

		if (plan.TeamRecruitmentAdd != null)
			await DispatchFindGroupRecruitmentAddAsync(plan.TeamRecruitmentAdd);
		else if (plan.FullTeamRecruitmentRemoval != null)
			await DispatchFindGroupWorldBroadcastIntentAsync(plan.FullTeamRecruitmentRemoval.WorldBroadcastIntent);
	}

	private async Task DispatchFindGroupRecruitmentAddAsync(FindGroupRecruitmentMutationPlan plan)
	{
		foreach (var intent in plan.DirectPacketIntents)
			await _connectionRegistry!.SendPacketToPlayerAsync(intent.RecipientObjectId, intent.Packet);

		var showPlan = plan.ShowRecruitmentsPlan;
		if (showPlan == null)
			return;

		var recipientObjectId = plan.DirectPacketIntents.FirstOrDefault()?.RecipientObjectId;
		if (recipientObjectId is null)
			return;

		await _connectionRegistry!.SendPacketToPlayerAsync(
			recipientObjectId.Value,
			showPlan.Packet);
	}

	private async Task DispatchFindGroupWorldBroadcastIntentAsync(FindGroupWorldBroadcastIntent? intent)
	{
		if (intent == null)
			return;

		await _connectionRegistry!.BroadcastToWorldAsync(
			intent.Packet,
			player => string.Equals(player.Race, intent.Race, StringComparison.Ordinal));
	}

	private async Task<DuelResponsePlan> HandleDuelAcceptQuestionResponseAsync(
		Player responder,
		CmQuestionResponse packet)
	{
		var result = _playerDuelRequestService.HandleTargetResponse(
			responder,
			packet.QuestionId,
			packet.Response,
			TryGetOnlinePlayerByObjectId);
		foreach (var intent in result.PacketIntents)
			await SendDuelPacketAsync(intent.RecipientObjectId, intent.Packet);
		return result;
	}

	private async Task<DuelResponsePlan> HandleDuelWithdrawQuestionResponseAsync(
		Player requester,
		CmQuestionResponse packet)
	{
		var result = _playerDuelRequestService.HandleWithdrawResponse(
			requester,
			packet.QuestionId,
			packet.Response,
			TryGetOnlinePlayerByObjectId);
		foreach (var intent in result.PacketIntents)
			await SendDuelPacketAsync(intent.RecipientObjectId, intent.Packet);
		return result;
	}

	private async Task<TeleportToNpcResponseResult> HandleTeleportToNpcQuestionResponseAsync(
		Player responder,
		CmQuestionResponse packet)
	{
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null)
			return TeleportToNpcResponseResult.MissingRequest();

		var result = new PlayerTeleportToNpcRequestService().HandleResponse(
			responder,
			packet.QuestionId,
			packet.Response,
			staticData.NpcSpawns,
			staticData.NpcTemplates);
		if (result.Status != TeleportToNpcResponseStatus.Accepted || result.Teleport == null)
			return result;

		// Java parity: TeleportService.teleportToNpc accepts with TeleportAnimation.NONE and immediately
		// runs the spawn completion path; C# reuses the same completion packet fan-out as same-instance portals.
		RevalidatePlayerCreaturePvpZones(responder, staticData);
		await SendDelayedTeleportCompletionPacketsAsync(
			responder,
			result.Teleport,
			staticData);
		return result;
	}

	private async Task HandleExperienceRecoveryQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		var result = PlayerExperienceRecoveryService.HandleResponse(
			responder,
			packet.QuestionId,
			packet.Response,
			_runtimeContext?.DataManager?.StaticData.ItemTemplates,
			_runtimeContext?.DataManager?.StaticData.PlayerExperienceTable);
		if (!result.Handled)
			return;

		foreach (var responsePacket in result.Packets)
			await SendPacketAsync(responsePacket);
	}

	private async Task<ExchangeResponsePlan> HandleExchangeQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		var result = _playerExchangeRequestService.HandleResponse(
			responder,
			packet.QuestionId,
			packet.Response,
			TryGetOnlinePlayerByObjectId);
		foreach (var intent in result.PacketIntents)
			await SendExchangePacketAsync(intent.RecipientObjectId, intent.Packet);
		return result;
	}

	private async Task<RecallInstantResponsePlan> HandleRecallInstantQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		var result = new PlayerRecallInstantRequestService().HandleResponse(
			responder,
			packet.QuestionId,
			packet.Response,
			TryGetOnlinePlayerByObjectId);
		foreach (var intent in result.PacketIntents)
			await SendExchangePacketAsync(intent.RecipientObjectId, intent.Packet);
		return result;
	}

	private async Task<CraftSkillLearnResponsePlan> HandleCraftSkillLearnQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (itemTemplates == null)
			return CraftSkillLearnResponsePlan.NotHandled(CraftSkillLearnResponseStatus.NoPendingRequest);

		var result = new CraftSkillUpdateService().HandleResponse(
			responder,
			packet.QuestionId,
			packet.Response,
			itemTemplates);
		foreach (var responsePacket in result.Packets)
			await SendPacketAsync(responsePacket);
		return result;
	}

	private async Task<StorageExpansionResponsePlan> HandleStorageExpansionQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		if (itemTemplates == null)
			return StorageExpansionResponsePlan.NotHandled(StorageExpansionResponseStatus.NoPendingRequest);

		var result = new StorageExpansionNpcService().HandleResponse(
			responder,
			packet.QuestionId,
			packet.Response,
			itemTemplates);
		foreach (var responsePacket in result.Packets)
			await SendPacketAsync(responsePacket);
		return result;
	}

	private static void CancelExchangeForQuestionAccept(Player responder)
	{
		// Java parity: CM_QUESTION_RESPONSE.runImpl calls ExchangeService.cancelExchange(player)
		// when a player accepts any question while trading. C# does not have the full ExchangeService
		// map yet, so this clears the currently represented local trade state only.
		responder.IsTrading = false;
		responder.IsExchangeLocked = false;
		responder.IsExchangeConfirmed = false;
		responder.CurrentExchangePartnerObjectId = 0;
	}

	private async Task HandleLeagueInviteQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond, which removes the
		// LeagueInviteEvent handler and invokes denyRequest for 0 or acceptRequest for nonzero responses.
		var pendingRequest = responder.PendingLeagueInviteRequest;
		if (pendingRequest == null || pendingRequest.QuestionId != packet.QuestionId)
			return;

		var dispatch = responder.ResponseRequester.Respond(packet.QuestionId, packet.Response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.LeagueInvite)
		{
			responder.PendingLeagueInviteRequest = null;
			return;
		}

		if (_connectionRegistry == null)
		{
			responder.PendingLeagueInviteRequest = null;
			return;
		}

		var requester = TryGetOnlinePlayerByObjectId(pendingRequest.RequesterObjectId);
		if (requester == null)
		{
			responder.PendingLeagueInviteRequest = null;
			return;
		}

		var requesterAllianceId = requester.CurrentAllianceSnapshot?.AllianceId ?? 0;
		var newLeagueId = packet.Response != 0
			&& requesterAllianceId > 0
			&& _playerLeagueRuntime.ResolveByAllianceId(requesterAllianceId) == null
				? _idFactory?.NextId()
				: null;
		var planner = new PlayerLeagueInvitePlanner();
		var responsePlan = planner.CreatePendingRequestResponsePlan(
			requester,
			responder,
			packet.QuestionId,
			packet.Response,
			_playerLeagueRuntime,
			_playerAllianceRuntime,
			newLeagueId);

		if (responsePlan.DenyPlan != null)
		{
			await _connectionRegistry.SendPacketToPlayerAsync(
				responsePlan.DenyPlan.RequesterObjectId,
				responsePlan.DenyPlan.SystemMessageIntent.Message);
		}

		if (responsePlan.AcceptPlan?.JoinPlan != null)
		{
			foreach (var intent in responsePlan.AcceptPlan.JoinPlan.PacketIntents.OrderBy(intent => intent.Sequence))
				await _connectionRegistry.SendPacketToPlayerAsync(intent.RecipientObjectId, intent.CreatePacket());
		}
	}

	private Player? TryGetOnlinePlayerByObjectId(int playerObjectId)
	{
		Player? match = null;
		_connectionRegistry?.ForEachOnlinePlayer(player =>
		{
			if (player.ObjectId == playerObjectId)
				match = player;
		});
		return match;
	}

	private async Task HandleRiftPortalQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		// Java parity: controllers/RVController.RequestResponseHandler.acceptRequest executes portal use after SM_QUESTION_WINDOW accept.
		if (_riftPortalInteractionService == null)
			return;

		await _riftPortalInteractionService.RespondAsync(
			responder,
			packet.QuestionId,
			packet.Response,
			packetToSend => SendPacketAsync(packetToSend));
	}

	private async Task AcceptFriendRequestAsync(Player requester, Player responder)
	{
		// Java parity: services/SocialService.makeFriends plus CM_FRIEND_ADD acceptRequest guards.
		if (IsFriendListFull(requester))
		{
			await SendPacketAsync(new SmFriendResponse(SmFriendResponse.RequesterListFullCantAccept, requester.Name));
			return;
		}

		if (IsFriendListFull(responder))
			return;

		if (requester.Friends.Any(friend => friend.ObjectId == responder.ObjectId))
			return;

		if (!await _socialRepository.AddFriendsAsync(requester.ObjectId, responder.ObjectId))
			return;

		requester.Friends = requester.Friends
			.Concat([CreateFriendSnapshot(responder)])
			.ToArray();
		responder.Friends = responder.Friends
			.Concat([CreateFriendSnapshot(requester)])
			.ToArray();

		if (_connectionRegistry != null)
		{
			await _connectionRegistry.SendPacketToPlayerAsync(
				requester.ObjectId,
				new SmFriendList(requester.Friends, GetPlayerExperienceTable()));
			await _connectionRegistry.SendPacketToPlayerAsync(
				requester.ObjectId,
				new SmFriendResponse(SmFriendResponse.TargetAdded, responder.Name));
		}

		await SendPacketAsync(new SmFriendList(responder.Friends, GetPlayerExperienceTable()));
		await SendPacketAsync(new SmFriendResponse(SmFriendResponse.TargetAdded, requester.Name));
	}

	private async Task HandleBlockAddAsync(Player player, CmBlockAdd packet)
	{
		// Java parity: network/aion/clientpackets/CM_BLOCK_ADD.runImpl -> SocialService.addBlockedUser.
		var targetName = ConvertCharacterName(packet.TargetName);
		var target = await _socialRepository.LoadPlayerByNameAsync(targetName);

		if (string.Equals(player.Name, packet.TargetName, StringComparison.OrdinalIgnoreCase))
		{
			await SendPacketAsync(new SmBlockResponse(SmBlockResponse.CantBlockSelf, packet.TargetName));
			return;
		}

		if (player.BlockedUsers.Count >= MaxBlockedUsers)
		{
			await SendPacketAsync(new SmBlockResponse(SmBlockResponse.ListFull, packet.TargetName));
			return;
		}

		if (target == null)
		{
			await SendPacketAsync(new SmBlockResponse(SmBlockResponse.TargetNotFound, packet.TargetName));
			return;
		}

		if (player.Friends.Any(friend => friend.ObjectId == target.ObjectId))
		{
			await SendPacketAsync(SmSystemMessage.BlockListNoBuddy());
			return;
		}

		if (player.BlockedUsers.Any(blockedUser => blockedUser.ObjectId == target.ObjectId))
		{
			await SendPacketAsync(SmSystemMessage.BlockListAlreadyBlocked());
			return;
		}

		if (!await _socialRepository.AddBlockedUserAsync(player.ObjectId, target.ObjectId, packet.Reason))
			return;

		player.BlockedUsers = player.BlockedUsers
			.Concat([new PlayerBlockedUser(target.ObjectId, target.Name, packet.Reason)])
			.ToArray();
		await SendPacketAsync(new SmBlockList(player.BlockedUsers));
		await SendPacketAsync(new SmBlockResponse(SmBlockResponse.BlockSuccessful, target.Name));
	}

	private async Task HandleFriendDeleteAsync(Player player, CmFriendDelete packet)
	{
		// Java parity: network/aion/clientpackets/CM_FRIEND_DEL.runImpl -> SocialService.deleteFriend.
		var friend = FindFriendByName(player, packet.TargetName);
		if (friend == null)
		{
			await SendPacketAsync(SmSystemMessage.BuddyListNotInList());
			return;
		}

		if (!await _socialRepository.DeleteFriendsAsync(player.ObjectId, friend.ObjectId))
			return;

		if (_connectionRegistry != null
			&& _connectionRegistry.TryGetOnlinePlayerByName(friend.Name, out var friendPlayer)
			&& friendPlayer != null)
		{
			friendPlayer.Friends = friendPlayer.Friends
				.Where(existingFriend => existingFriend.ObjectId != player.ObjectId)
				.ToArray();
			await _connectionRegistry.SendPacketToPlayerAsync(
				friendPlayer.ObjectId,
				new SmFriendList(friendPlayer.Friends, GetPlayerExperienceTable()));
			await _connectionRegistry.SendPacketToPlayerAsync(
				friendPlayer.ObjectId,
				new SmFriendNotify(SmFriendNotify.Deleted, player.Name));
		}

		player.Friends = player.Friends
			.Where(existingFriend => existingFriend.ObjectId != friend.ObjectId)
			.ToArray();
		await SendPacketAsync(new SmFriendList(player.Friends, GetPlayerExperienceTable()));
		await SendPacketAsync(new SmFriendResponse(SmFriendResponse.TargetRemoved, friend.Name));
	}

	private async Task HandleFriendSetMemoAsync(Player player, CmFriendSetMemo packet)
	{
		// Java parity: network/aion/clientpackets/CM_FRIEND_SET_MEMO.runImpl -> SocialService.setFriendMemo.
		var friend = FindFriendByName(player, packet.TargetName);
		if (friend == null)
		{
			await SendPacketAsync(SmSystemMessage.BuddyListNotInList());
			return;
		}

		if (string.Equals(friend.Memo, packet.Memo, StringComparison.Ordinal))
			return;

		if (!await _socialRepository.SetFriendMemoAsync(player.ObjectId, friend.ObjectId, packet.Memo))
			return;

		player.Friends = player.Friends
			.Select(existingFriend => existingFriend.ObjectId == friend.ObjectId
				? existingFriend with { Memo = packet.Memo }
				: existingFriend)
			.ToArray();
		await SendPacketAsync(new SmFriendList(player.Friends, GetPlayerExperienceTable()));
	}

	private async Task HandleBlockDeleteAsync(Player player, CmBlockDelete packet)
	{
		// Java parity: network/aion/clientpackets/CM_BLOCK_DEL.runImpl -> SocialService.deleteBlockedUser.
		var blockedUser = FindBlockedUserByName(player, packet.TargetName);
		if (blockedUser == null)
		{
			await SendPacketAsync(SmSystemMessage.BuddyListNotInList());
			return;
		}

		if (!await _socialRepository.DeleteBlockedUserAsync(player.ObjectId, blockedUser.ObjectId))
			return;

		player.BlockedUsers = player.BlockedUsers
			.Where(existingBlockedUser => existingBlockedUser.ObjectId != blockedUser.ObjectId)
			.ToArray();
		await SendPacketAsync(new SmBlockList(player.BlockedUsers));
		await SendPacketAsync(new SmBlockResponse(SmBlockResponse.UnblockSuccessful, blockedUser.Name));
	}

	private async Task HandleBlockSetReasonAsync(Player player, CmBlockSetReason packet)
	{
		// Java parity: network/aion/clientpackets/CM_BLOCK_SET_REASON.runImpl -> SocialService.setBlockedReason.
		var blockedUser = FindBlockedUserByName(player, packet.TargetName);
		if (blockedUser == null)
		{
			await SendPacketAsync(SmSystemMessage.BlockListNotInList());
			return;
		}

		if (string.Equals(blockedUser.Reason, packet.Reason, StringComparison.Ordinal))
			return;

		if (!await _socialRepository.SetBlockedReasonAsync(player.ObjectId, blockedUser.ObjectId, packet.Reason))
			return;

		player.BlockedUsers = player.BlockedUsers
			.Select(existingBlockedUser => existingBlockedUser.ObjectId == blockedUser.ObjectId
				? existingBlockedUser with { Reason = packet.Reason }
				: existingBlockedUser)
			.ToArray();
		await SendPacketAsync(new SmBlockList(player.BlockedUsers));
		await SendPacketAsync(new SmBlockResponse(SmBlockResponse.EditNote, blockedUser.Name));
	}

	private SmChatWindow CreateChatWindowPacket(Player target, bool isGroup)
	{
		return new SmChatWindow(target, isGroup, _runtimeContext?.DataManager?.StaticData.PlayerExperienceTable);
	}

	private string GetOrCreateSecurityToken()
	{
		// Java parity: services/player/SecurityTokenService.generateToken.
		if (_securityToken.Length != 0)
			return _securityToken;

		_securityToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
		return _securityToken;
	}

	private PlayerExperienceTable? GetPlayerExperienceTable()
	{
		return _runtimeContext?.DataManager?.StaticData.PlayerExperienceTable;
	}

	private static PlayerFriend? FindFriendByName(Player player, string targetName)
	{
		// Java parity: model/gameobjects/player/FriendList.getFriend(String) uses case-insensitive name matching.
		return player.Friends.FirstOrDefault(friend => string.Equals(friend.Name, targetName, StringComparison.OrdinalIgnoreCase));
	}

	private static PlayerBlockedUser? FindBlockedUserByName(Player player, string targetName)
	{
		// Java parity: model/gameobjects/player/BlockList.getBlockedPlayer(String) uses case-insensitive name matching.
		return player.BlockedUsers.FirstOrDefault(blockedUser => string.Equals(blockedUser.Name, targetName, StringComparison.OrdinalIgnoreCase));
	}

	private static byte NormalizeFriendStatus(byte status)
	{
		// Java parity: FriendList.Status.getByValue fallback in CM_FRIEND_STATUS.runImpl.
		return status is 0 or 1 or 3 ? status : (byte)1;
	}

	private bool IsFriendListFull(Player player)
	{
		// Java parity: model/gameobjects/player/FriendList.isFull uses CustomConfig.FRIENDLIST_SIZE.
		return player.Friends.Count >= _options.Custom.FriendListSize;
	}

	private static PlayerFriend CreateFriendSnapshot(Player player)
	{
		// Java parity: model/gameobjects/player/Friend constructed from PlayerCommonData with an empty memo.
		var activeHouse = GetActiveHouse(player);
		return new PlayerFriend(
			player.ObjectId,
			player.Name,
			player.Exp,
			player.PlayerClass,
			player.Gender,
			player.Position.WorldId,
			player.FriendListStatus == 0 ? player.LastOnline : null,
			player.Note,
			string.Empty,
			player.FriendListStatus != 0 || player.IsOnline,
			activeHouse?.AddressId ?? 0,
			activeHouse?.DoorState ?? 0);
	}

	private static PlayerFriend? UpdateFriendSnapshot(Player friendPlayer, Player activePlayer, byte activeStatus)
	{
		// Java parity: friendPlayer.getFriendList().getFriend(activePlayerId).setPCD(activePlayer.getCommonData()).
		var activeHouse = GetActiveHouse(activePlayer);
		PlayerFriend? updatedFriend = null;
		friendPlayer.Friends = friendPlayer.Friends
			.Select(friend =>
			{
				if (friend.ObjectId != activePlayer.ObjectId)
					return friend;

				updatedFriend = friend with
				{
					Exp = activePlayer.Exp,
					PlayerClass = activePlayer.PlayerClass,
					Gender = activePlayer.Gender,
					MapId = activePlayer.Position.WorldId,
					LastOnline = activeStatus == 0 ? activePlayer.LastOnline : null,
					Note = activePlayer.Note,
					IsOnline = activeStatus != 0,
					HouseAddressId = activeHouse?.AddressId ?? 0,
					HouseDoorState = activeHouse?.DoorState ?? 0,
				};
				return updatedFriend;
			})
			.ToArray();

		return updatedFriend;
	}

	private static PlayerHouse? GetActiveHouse(Player player)
	{
		// Java parity: services/HousingService.findActiveHouse prefers the loaded studio, otherwise the non-inactive custom house.
		return player.Houses.FirstOrDefault(house => !house.IsInactive);
	}

	private void RegisterLoadedHouses(Player player, HousingTemplateTable? housingTemplates)
	{
		// Java parity: services/HousingService.spawnHouses brings House objects into World before KnownList scans.
		if (_world == null || housingTemplates == null)
			return;

		foreach (var house in player.Houses)
			AddOrUpdateWorldHouse(player, house, housingTemplates);
	}

	private WorldHouse? AddOrUpdateWorldHouse(Player player, PlayerHouse house, HousingTemplateTable? housingTemplates)
	{
		// Java parity: House positions are derived from model/templates/housing/HouseAddress.
		if (_world == null || housingTemplates == null)
			return null;
		if (!WorldHouse.TryCreate(player, house, housingTemplates, out var worldHouse) || worldHouse == null)
			return null;

		_world.AddOrUpdateHouse(worldHouse);
		_houseDoorStateService?.SetHouseDoorState(worldHouse.Position.WorldId, worldHouse.AddressId, worldHouse.DoorState);
		return worldHouse;
	}

	private async Task RefreshHousingVisibilityForPlayerAsync(Player player)
	{
		// Java parity: VisibleObject.updateKnownlist after movement includes player-aware House objects.
		var housingTemplates = _runtimeContext?.DataManager?.StaticData.HousingTemplates;
		if (_connectionRegistry == null || _world == null || housingTemplates == null)
			return;

		await _connectionRegistry.RefreshHousingVisibilityAsync(_world.GetHouses(), housingTemplates, player.ObjectId);
	}

	private async Task RefreshNpcVisibilityForPlayerAsync(Player player)
	{
		// Java parity: VisibleObject.updateKnownlist after movement includes visible Npc objects.
		if (_connectionRegistry == null || _world == null)
			return;

		await _connectionRegistry.RefreshNpcVisibilityAsync(_world.GetNpcs(player.Position.WorldId), player.ObjectId);
	}

	private static string GetRealCharacterName(string name)
	{
		// Java parity: utils/ChatUtil.getRealCharName with default Util.convertName behavior.
		var normalized = name.Trim();
		if (normalized.Length == 0)
			return string.Empty;
		if (normalized[0] is '\uE052' or '\uE053')
			normalized = normalized[1..];
		if (normalized.Length == 0)
			return string.Empty;
		return char.ToUpperInvariant(normalized[0]) + normalized[1..].ToLowerInvariant();
	}

	private static string ConvertCharacterName(string name)
	{
		// Java parity: utils/Util.convertName with default NameConfig.ALLOW_CUSTOM_NAMES=false behavior.
		var normalized = name.Trim();
		return normalized.Length == 0
			? string.Empty
			: char.ToUpperInvariant(normalized[0]) + normalized[1..].ToLowerInvariant();
	}

	private async Task HandleQuitAsync(CmQuit packet)
	{
		// Java parity: network/aion/clientpackets/CM_QUIT.runImpl.
		await LeaveActivePlayerAsync(notifyPostmanClient: true);
		if (packet.StayConnected)
		{
			_state = GameConnectionState.Authed;
			await SendPacketAsync(new SmQuitResponse());
			return;
		}

		await SendPacketAsync(new SmQuitResponse());
		await CloseAsync();
	}

	private async Task LeaveActivePlayerAsync(bool notifyPostmanClient)
	{
		var player = _activePlayer;
		if (player == null)
			return;

		await LeavePlayerWorldAsync(player, notifyPostmanClient);
		_activePlayer = null;
	}

	internal async Task LeavePlayerWorldAsync(Player player, bool notifyPostmanClient)
	{
		if (_chatServer != null)
			await _chatServer.SendPlayerLogoutAsync(player.ObjectId);
		_expirableTaskService?.UnregisterPlayer(player);
		if (_options.AutoGroup.Enabled)
		{
			var staticData = _runtimeContext?.DataManager?.StaticData;
			var logoutCleanup = _autoGroupLookingPartyRegistrations.CleanupSearchEntriesOnLogout(
				player.ObjectId,
				staticData?.AutoGroups,
				staticData?.InstanceCooltimes,
				_autoGroupInstanceLeaveRuntimeService.GetActiveInstanceMaskIds());
			foreach (var cancelIntent in logoutCleanup.StartEnterCancelIntents)
				await ApplyAutoGroupCancelEnterAsync(player, cancelIntent.InstanceMaskId, staticData?.AutoGroups, staticData?.InstanceCooltimes);
			if (_connectionRegistry != null)
			{
				foreach (var queueRecheckPlan in logoutCleanup.QueueRecheckPlans)
					await ApplyAutoGroupReadyMatchPlanAsync(
						queueRecheckPlan,
						staticData?.AutoGroups,
						staticData?.InstanceCooltimes,
						_connectionRegistry);
			}

			var position = player.Position;
			var onlinePlayersInsideAtLogout = 0;
			if (_runtimeContext?.WorldMapStates.TryGetWorldMapInstance(position.WorldId, position.InstanceId, out var currentInstance) == true
				&& currentInstance != null)
			{
				// Java parity: PlayerLeaveWorldService clears the client connection before AutoGroupService.onLogout,
				// so the logging-out player no longer counts as an online player for destroyIfPossible.
				onlinePlayersInsideAtLogout = Math.Max(0, currentInstance.PlayerCount - 1);
			}
			_autoGroupInstanceLeaveRuntimeService.DestroyCurrentAutoInstanceIfPossibleOnLogout(
				player,
				position.WorldId,
				position.InstanceId,
				onlinePlayersInsideAtLogout);
		}
		SaveOfflineKiskBinding(player);
		if (IsDead(player))
			ApplyDeadPlayerLogoutRevive(player);
		else
			await ApplyLogoutDuelLossAsync(player);
		await DismissPostmanAsync(player, notifyClient: notifyPostmanClient);
		_pendingHouseObjectUse?.Task.Cancel();
		_pendingHouseObjectUse = null;
		ReleaseHouseObjectOccupants(player.ObjectId);
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, new SmDelete(player.ObjectId));
		_connectionRegistry?.UnregisterPlayerConnection(player.ObjectId, this);
		// Java parity: World.despawn -> MapRegion.revalidateZones on an unspawned Creature leaves tracked zones.
		_creaturePvpZoneCounterService?.ClearCounters(player.ObjectId);
		if (_playerEnterWorldService != null)
			await _playerEnterWorldService.LeaveWorldAsync(player);
		else
			_world?.TryRemoveObject(player.ObjectId, out _);
	}

	private void SaveOfflineKiskBinding(Player player)
	{
		// Java parity: services/KiskService.onLogout stores the current Kisk for later onLogin restoration.
		if (player.BoundKiskObjectId == 0)
			return;

		_runtimeContext?.Kisks.RegisterOfflineBinding(player.ObjectId, player.BoundKiskObjectId);
	}

	private async Task<DuelEndPlan> ApplyLogoutDuelLossAsync(Player player)
	{
		// Java parity: PlayerLeaveWorldService.leaveWorld calls DuelService.loseDuel(player)
		// only from the non-dead branch.
		var plan = _playerDuelRequestService.LoseDuel(player, TryGetOnlinePlayerByObjectId);
		foreach (var intent in plan.PacketIntents)
			await SendDuelPacketAsync(intent.RecipientObjectId, intent.Packet);
		return plan;
	}

	private void ApplyDeadPlayerLogoutRevive(Player player)
	{
		// Java parity: services/player/PlayerLeaveWorldService.leaveWorld revives dead players after KiskService.onLogout.
		if (!IsDead(player))
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var resourceMaxStats = SmStatsInfo.CalculateCurrentResourceMaxStats(
			player,
			staticData?.PlayerExperienceTable,
			staticData?.ItemTemplates,
			staticData?.ItemRandomBonuses,
			staticData?.ItemSets,
			staticData?.EnchantTemplates,
			staticData?.TemperingTemplates,
			staticData?.SkillTemplates,
			staticData?.TitleTemplates);
		ClearReviveTargets(player);

		if (ShouldUseInstanceLogoutRevive(player)
			&& IsInstanceMap(player.Position.WorldId)
			&& TryGetLogoutInstanceStartPosition(player, out var instanceStartPosition))
		{
			PlayerReviveRestoreService.ApplyInstanceReviveRestore(
				player,
				resourceMaxStats.MaxHp,
				resourceMaxStats.MaxMp,
				player.HasNoResurrectPenaltyEffect);
			new PlayerReviveCleanupAdapterService().Apply(new PlayerReviveCleanupAdapterRequest(
				player.ObjectId,
				player.AggroList.Entries,
				ExecuteLiveAggroMutation: true,
				player.AggroList));
			PlayerTeleportService.TeleportWithinSameInstance(player, instanceStartPosition);
			player.ClearResurrectionPositionState();
			return;
		}

		PlayerReviveRestoreService.ApplyBindReviveRestore(
			player,
			resourceMaxStats.MaxHp,
			resourceMaxStats.MaxMp,
			player.HasNoResurrectPenaltyEffect);
		new PlayerReviveCleanupAdapterService().Apply(new PlayerReviveCleanupAdapterRequest(
			player.ObjectId,
			player.AggroList.Entries,
			ExecuteLiveAggroMutation: true,
			player.AggroList));
		TryMoveDeadLogoutPlayerToBindLocation(player, staticData?.PlayerInitialData);
		player.ClearResurrectionPositionState();
	}

	private bool ShouldUseInstanceLogoutRevive(Player player)
	{
		// Java parity: PlayerLeaveWorldService.leaveWorld uses player.isInInstance() || player.getWorldId() == 400030000.
		if (player.Position.WorldId == 400030000)
			return true;

		return IsInstanceMap(player.Position.WorldId);
	}

	private bool IsInstanceMap(int worldId)
	{
		return _runtimeContext?.WorldMapStates.TryGetMap(worldId, out var map) == true
			&& map?.Summary.IsInstance == true;
	}

	private bool TryGetLogoutInstanceStartPosition(Player player, out WorldPosition startPosition)
	{
		startPosition = default;
		if (_runtimeContext?.WorldMapStates.TryGetWorldMapInstance(
				player.Position.WorldId,
				player.Position.InstanceId,
				out var instance) != true
			|| instance?.StartPosition == null)
			return false;

		startPosition = instance.StartPosition.Value;
		return true;
	}

	private static bool TryMoveDeadLogoutPlayerToBindLocation(Player player, PlayerInitialDataTable? playerInitialData)
	{
		if (player.BindPoint == null && playerInitialData == null)
			return false;

		var plan = playerInitialData == null
			? new BindLocationResolutionPlan(
				BindLocationResolutionStatus.PlayerBindPoint,
				new WorldPosition(
					player.BindPoint!.MapId,
					player.BindPoint.X,
					player.BindPoint.Y,
					player.BindPoint.Z,
					player.BindPoint.Heading,
					player.Position.WorldId != player.BindPoint.MapId ? 1 : player.Position.InstanceId),
				"TeleportService.moveToBindLocation -> player.getBindPoint()")
			: PlayerTeleportService.ResolveBindLocation(player, playerInitialData);
		if (plan.Destination == null)
			return false;

		PlayerTeleportService.TeleportToKiskPosition(player, plan.Destination.Value);
		return true;
	}

	private void HandleTargetSelect(Player player, CmTargetSelect packet)
	{
		// Java parity: network/aion/clientpackets/CM_TARGET_SELECT.runImpl updates VisibleObject.target.
		if (packet.SelectTargetOfTarget)
		{
			// Full assist-key behavior depends on target object references, target-of-target, and KnownList visibility.
			_logger.LogDebug("Player {PlayerObjectId} requested target-of-target selection before KnownList target references are ported", player.ObjectId);
			return;
		}

		player.TargetObjectId = packet.TargetObjectId <= 0 ? 0 : packet.TargetObjectId;
	}

	private async Task HandleTitleSetAsync(Player player, CmTitleSet packet)
	{
		// Java parity: network/aion/clientpackets/CM_TITLE_SET.runImpl -> TitleList.setDisplayTitle.
		if (packet.TitleId != NoTitleId && !PlayerHasTitle(player, packet.TitleId))
			return;

		player.TitleId = packet.TitleId;
		await SendPacketAsync(new SmTitleInfo(packet.TitleId));
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, new SmTitleInfo(player, packet.TitleId), includeSourcePlayer: true);

		// Java parity: TitleList.setDisplayTitle -> owner.getController().updateNearbyQuests.
		// Title requirements on quests may change which are available after a title change.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		var worldMapStates = _runtimeContext?.WorldMapStates;
		if (worldMapStates != null && staticData?.NearbyQuestTemplates != null)
		{
			worldMapStates.TryGetWorldMapInstance(player.Position.WorldId, player.Position.InstanceId, out var mapInstance);
			var nearbyPlan = NearbyQuestRefreshPlanService.CreatePlan(player, mapInstance, staticData.NearbyQuestTemplates);
			var packetPlan = NearbyQuestRefreshPlanService.CreatePacketFactoryPlan(nearbyPlan);
			if (packetPlan.Packet != null)
				await SendPacketAsync(packetPlan.Packet);
		}
	}

	private async Task HandleBonusTitleAsync(Player player, CmBonusTitle packet)
	{
		// Java parity: network/aion/clientpackets/CM_BONUS_TITLE.runImpl -> TitleList.setBonusTitle.
		if (packet.BonusTitleId != NoTitleId && !PlayerHasTitle(player, packet.BonusTitleId))
			return;

		player.BonusTitleId = packet.BonusTitleId;
		await SendPacketAsync(new SmTitleInfo(6, packet.BonusTitleId));
	}

	private static bool PlayerHasTitle(Player player, int titleId)
	{
		// Java parity: model/gameobjects/player/title/TitleList.contains.
		return player.Titles.Any(title => title.Id == titleId);
	}

	private async Task HandleMotionAsync(Player player, CmMotion packet)
	{
		// Java parity: network/aion/clientpackets/CM_MOTION.runImpl -> MotionList.setActive.
		var motions = player.Motions.ToArray();
		PlayerMotion? oldMotion;
		PlayerMotion? newMotion = null;

		if (packet.MotionId != 0)
		{
			newMotion = motions.FirstOrDefault(motion => motion.Id == packet.MotionId);
			if (newMotion == null || newMotion.IsActive)
				return;

			oldMotion = motions.FirstOrDefault(motion => motion.IsActive && PlayerMotion.GetMotionType(motion.Id) == packet.MotionType);
			motions = motions
				.Select(motion =>
					motion.Id == packet.MotionId ? motion with { IsActive = true } :
					oldMotion != null && motion.Id == oldMotion.Id ? motion with { IsActive = false } :
					motion)
				.ToArray();
		}
		else
		{
			oldMotion = motions.FirstOrDefault(motion => motion.IsActive && PlayerMotion.GetMotionType(motion.Id) == packet.MotionType);
			if (oldMotion == null)
				return;

			motions = motions
				.Select(motion => motion.Id == oldMotion.Id ? motion with { IsActive = false } : motion)
				.ToArray();
		}

		player.Motions = motions;
		if (oldMotion != null)
			await PersistMotionActiveAsync(player.ObjectId, oldMotion.Id, isActive: false);
		if (newMotion != null)
			await PersistMotionActiveAsync(player.ObjectId, newMotion.Id, isActive: true);

		await SendPacketAsync(new SmMotion(packet.MotionId, packet.MotionType));
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, new SmMotion(player.ObjectId, player.Motions), includeSourcePlayer: true);
	}

	private async Task HandleEmotionAsync(Player player, CmEmotion packet)
	{
		// Java parity: network/aion/clientpackets/CM_EMOTION.runImpl state-changing branches.
		if ((player.LifeStats?.CurrentHp ?? 1) <= 0)
			return;

		if (!IsHandledEmotion(packet.EmotionType))
			return;

		// Java parity: network/aion/clientpackets/CM_EMOTION.runImpl abnormal movement guard before item-use cancellation.
		if (!BypassesEmotionAbnormalGuard(packet.EmotionType)
			&& (player.IsInAnyAbnormalState(PlayerAbnormalState.CantMoveState) || player.IsUnderFear() || player.IsConfused()))
			return;

		if (player.IsInState(PlayerCreatureState.PrivateShop)
			|| (player.IsInState(PlayerCreatureState.WeaponEquipped)
				&& packet.EmotionType is EmotionType.ChairSit or EmotionType.Jump))
			return;

		await CancelPendingItemUseOnEmotionAsync(player);
		if (packet.EmotionType == EmotionType.SelectTarget)
			return;

		// Java parity: network/aion/clientpackets/CM_EMOTION.runImpl stance guard after cancelUseItem/cancelCurrentSkill.
		await CancelCurrentSkillForEmotionAsync(player);
		if (player.IsUnderStance())
		{
			await SendPacketAsync(packet.EmotionType == EmotionType.Fly
				? SmSystemMessage.SkillCannotTakeOffWhileInCurrentStance()
				: SmSystemMessage.SkillCannotChangeModeWhileInCurrentStance());
			return;
		}

		switch (packet.EmotionType)
		{
			case EmotionType.Sit:
				if (player.IsInState(PlayerCreatureState.PrivateShop))
					return;
				if (player.IsInRideMode)
					await DismountRideAsync(player);
				player.SetCreatureState(PlayerCreatureState.Resting, enabled: true);
				break;
			case EmotionType.Stand:
				player.SetCreatureState(PlayerCreatureState.Resting, enabled: false);
				break;
			case EmotionType.ChairSit:
				player.ReplaceCreatureState(PlayerCreatureState.Chair);
				break;
			case EmotionType.ChairUp:
				if (player.IsInState(PlayerCreatureState.Chair))
					player.ReplaceCreatureState(PlayerCreatureState.Active);
				break;
			case EmotionType.LandFlyTeleport:
				player.CompleteFlyTeleport();
				break;
			case EmotionType.Fly:
				var flyResult = PlayerFlightActionService.StartFlying(
					player,
					DateTimeOffset.UtcNow,
					freeFlightAccessLevel: _options.Administration.FreeFlightAccessLevel);
				if (!flyResult.Succeeded)
				{
					if (flyResult.AuditMessage != null)
						_logger.LogWarning(
							"Player {PlayerName} ({PlayerObjectId}) {AuditMessage}",
							player.Name,
							player.ObjectId,
							flyResult.AuditMessage);
					if (flyResult.SystemMessage != null)
						await SendPacketAsync(flyResult.SystemMessage);
					return;
				}
				await UpdatePlayerStatsAndSpeedVisuallyAsync(player);
				break;
			case EmotionType.Land:
				player.EndFlying();
				await UpdatePlayerStatsAndSpeedVisuallyAsync(player);
				break;
			case EmotionType.AttackModeInMove:
			case EmotionType.AttackModeInStanding:
				player.SetCreatureState(PlayerCreatureState.WeaponEquipped, enabled: true);
				break;
			case EmotionType.NeutralModeInMove:
			case EmotionType.NeutralModeInStanding:
				player.SetCreatureState(PlayerCreatureState.WeaponEquipped, enabled: false);
				break;
			case EmotionType.Walk:
				if (player.IsFlying())
					return;
				player.SetCreatureState(PlayerCreatureState.WalkMode, enabled: true);
				break;
			case EmotionType.Run:
				player.SetCreatureState(PlayerCreatureState.WalkMode, enabled: false);
				break;
			case EmotionType.PowershardOn:
				if (!HasEquippedPowerShard(player, _runtimeContext?.DataManager?.StaticData.ItemTemplates))
				{
					await SendPacketAsync(SmSystemMessage.WeaponBoostNoBoosterEquipped());
					return;
				}

				await SendPacketAsync(SmSystemMessage.WeaponBoostStarted());
				player.SetCreatureState(PlayerCreatureState.Powershard, enabled: true);
				break;
			case EmotionType.PowershardOff:
				await SendPacketAsync(SmSystemMessage.WeaponBoostEnded());
				player.SetCreatureState(PlayerCreatureState.Powershard, enabled: false);
				break;
			case EmotionType.StartSprint:
				if (!player.CanStartRideSprint())
					return;
				player.StartRideSprint();
				break;
			case EmotionType.EndSprint:
				if (!player.CanEndRideSprint())
					return;
				player.EndRideSprint();
				break;
			case EmotionType.Emote:
				if (!CanUseEmotion(player, packet.Emotion, _runtimeContext?.DataManager?.StaticData.ItemTemplates))
					return;
				break;
		}

		var targetObjectId = player.TargetObjectId != 0 ? player.TargetObjectId : packet.TargetObjectId;
		await BroadcastEmotionAsync(
			player,
			new SmEmotion(player, packet.EmotionType, packet.Emotion, packet.X, packet.Y, packet.Z, packet.Heading, targetObjectId));
	}

	private async Task CancelCurrentSkillForEmotionAsync(Player player)
	{
		// Java parity: PlayerController.cancelCurrentSkill(null) from CM_EMOTION before stance/mode changes.
		var canceledSkill = player.ClearCastingSkill();
		if (canceledSkill?.Method == PlayerCastingSkillMethod.Cast)
		{
			await BroadcastToSightedPlayersAsync(player, new SmSkillCancel(player.ObjectId, canceledSkill.SkillId));
			await SendPacketAsync(SmSystemMessage.SkillCanceled());
		}
		else if (canceledSkill is { Method: PlayerCastingSkillMethod.Item, HasItemCancellationMetadata: true })
		{
			await SendPacketAsync(SmSystemMessage.ItemCanceled());
			if (canceledSkill.ItemCooldownDelayId.HasValue)
				player.RemoveItemCooldown(canceledSkill.ItemCooldownDelayId.Value);
			await BroadcastToSightedPlayersAsync(
				player,
				new SmItemUsageAnimation(
					player.ObjectId,
					canceledSkill.FirstTargetObjectId,
					canceledSkill.ItemObjectId,
					canceledSkill.ItemTemplateId,
					0,
					3,
					0,
					0,
					1,
					0));
		}
	}

	private static bool IsHandledEmotion(EmotionType emotionType)
	{
		return emotionType is EmotionType.SelectTarget
			or EmotionType.Jump
			or EmotionType.Sit
			or EmotionType.Stand
			or EmotionType.ChairSit
			or EmotionType.ChairUp
			or EmotionType.LandFlyTeleport
			or EmotionType.Fly
			or EmotionType.Land
			or EmotionType.AttackModeInMove
			or EmotionType.AttackModeInStanding
			or EmotionType.NeutralModeInMove
			or EmotionType.NeutralModeInStanding
			or EmotionType.Walk
			or EmotionType.Run
			or EmotionType.OpenDoor
			or EmotionType.CloseDoor
			or EmotionType.PowershardOn
			or EmotionType.PowershardOff
			or EmotionType.StartSprint
			or EmotionType.EndSprint
			or EmotionType.Emote;
	}

	private static bool BypassesEmotionAbnormalGuard(EmotionType emotionType)
	{
		// Java parity: CM_EMOTION skips the abnormal guard only for target select and weapon mode toggles.
		return emotionType is EmotionType.SelectTarget
			or EmotionType.AttackModeInMove
			or EmotionType.AttackModeInStanding
			or EmotionType.NeutralModeInMove
			or EmotionType.NeutralModeInStanding;
	}

	private static bool CanUseEmotion(Player player, int emotionId, ItemTemplateTable? itemTemplates)
	{
		// Java parity: model/gameobjects/player/emotion/EmotionList.canUse plus EmotionLearnAction.isLearnable.
		if (itemTemplates == null)
			return emotionId is >= 1 and <= 35
				|| emotionId > 10000
				|| player.Emotions.Any(emotion => emotion.Id == emotionId);

		return !itemTemplates.IsLearnableEmotion(emotionId)
			|| player.Emotions.Any(emotion => emotion.Id == emotionId);
	}

	private async Task BroadcastEmotionAsync(Player player, SmEmotion packet)
	{
		// Java parity: PacketSendUtility.broadcastToSightedPlayers(player, SM_EMOTION, true).
		await BroadcastToSightedPlayersAsync(player, packet);
	}

	private async Task BroadcastToSightedPlayersAsync(Player player, GameServerPacket packet)
	{
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, packet, includeSourcePlayer: true);
		else
			await SendPacketAsync(packet);
	}

	private async Task HandleBeshmundirDifficultyQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		// Java parity breadcrumb: BeshmundirsWalkAI AIRequest.acceptRequest calls moveToInstance
		// with difficulty 2 for registered request 902050; the alternate difficulty branch is not
		// reached by the currently registered question id in Java source.
		var dispatch = responder.ResponseRequester.Respond(packet.QuestionId, packet.Response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.BeshmundirDifficultyEnter
			|| !dispatch.Accepted
			|| dispatch.Request.Payload is not PendingBeshmundirDifficultyEnterRequest pending)
		{
			return;
		}

		await HandleBeshmundirsWalkMoveToInstanceAsync(responder, pending.NpcObjectId, pending.DifficultyId);
	}

	private async Task BroadcastActionAnimationAsync(Player player, SmActionAnimation packet)
	{
		// Java parity: PacketSendUtility.broadcastPacket(player, SM_ACTION_ANIMATION, true).
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, packet, includeSourcePlayer: true);
		else
			await SendPacketAsync(packet);
	}

	private async Task<PlayerVisualStatsUpdateResult?> UpdatePlayerStatsAndSpeedVisuallyAsync(Player player)
	{
		// Java parity: model/stats/container/PlayerGameStats.updateStatsAndSpeedVisually.
		if (_connectionRegistry == null)
			return null;

		var visualStats = new PlayerVisualStatsUpdateService(_connectionRegistry, _runtimeContext, _gameTimeService);
		return await visualStats.UpdateStatsAndSpeedVisuallyAsync(player, null);
	}

	internal async Task<PlayerZoneRevalidationResult> RevalidatePlayerFlightZonesAsync(Player player)
	{
		var staticData = _runtimeContext?.DataManager?.StaticData;
		// Java parity: CM_MOVE.notifyControllers -> ZoneUpdateService.revalidateZones updates all zone memberships after movement.
		RevalidatePlayerCreaturePvpZones(player, staticData);
		var result = PlayerZoneStateService.RevalidateFlightZones(
			player,
			staticData?.WorldMaps ?? Array.Empty<WorldMapSummary>(),
			staticData?.FlightZones,
			_runtimeContext?.WorldMapStates);
		var transition = PlayerZoneStateService.ApplyFlightZoneTransitionIntent(
			player,
			result,
			_options.Administration.FreeFlightAccessLevel);
		await ApplyFlightZoneTransitionFanoutAsync(player, result, transition);
		return result;
	}

	private void RevalidatePlayerCreaturePvpZones(Player player, StaticData? staticData)
	{
		// Java parity: Creature.revalidateZones -> MapRegion.revalidateZones -> ZoneInstance enter/leave callbacks.
		CreaturePvpZoneRevalidationService.Revalidate(
			player.ObjectId,
			player.Position,
			staticData?.CreaturePvpZones,
			_creaturePvpZoneCounterService);
	}

	private async Task ApplyFlightZoneTransitionFanoutAsync(
		Player player,
		PlayerZoneRevalidationResult revalidation,
		PlayerFlightZoneTransitionResult transition)
	{
		switch (transition.LeaveStatus)
		{
			case PlayerLeaveFlyAreaStatus.ContinueGliding:
				// Java parity: PlayerController.onLeaveFlyArea flying+gliding branch -> updateStatsAndSpeedVisually + SM_EMOTION(STOP_FLY).
				await UpdatePlayerStatsAndSpeedVisuallyAsync(player);
				await BroadcastEmotionAsync(player, new SmEmotion(player, EmotionType.StopFly));
				break;
			case PlayerLeaveFlyAreaStatus.EndedFlying:
				// Java parity: PlayerController.onLeaveFlyArea flying branch -> FlyController.endFly(true) + optional AuditLogger.log.
				await UpdatePlayerStatsAndSpeedVisuallyAsync(player);
				await BroadcastEmotionAsync(player, new SmEmotion(player, EmotionType.Land));
				if (!revalidation.IsInsideFlyZone)
					_logger.LogWarning(
						"Player {PlayerName} ({PlayerObjectId}) left fly zone in fly state at {Position}",
						player.Name,
						player.ObjectId,
						player.Position);
				break;
		}
	}

	private static bool HasEquippedPowerShard(Player player, ItemTemplateTable? itemTemplates)
	{
		// Java parity: model/gameobjects/player/Equipment.isPowerShardEquipped.
		return itemTemplates != null && player.InventoryItems.Any(item =>
			item.Location == CubeStorageId
			&& item.IsEquipped
			&& string.Equals(itemTemplates.GetItemTemplate(item.ItemId)?.ItemGroup, PowerShardItemGroup, StringComparison.Ordinal));
	}

	private async Task PersistMotionActiveAsync(int playerObjectId, int motionId, bool isActive)
	{
		if (!await _motionRepository.UpdateMotionActiveAsync(playerObjectId, motionId, isActive))
		{
			_logger.LogWarning(
				"Motion {MotionId} active={IsActive} update for player {PlayerObjectId} was not persisted",
				motionId,
				isActive,
				playerObjectId);
		}
	}

	private async Task HandleMoveAsync(Player player, CmMove packet)
	{
		// Java parity: network/aion/clientpackets/CM_MOVE.runImpl movement-state updates before World.updatePosition.
		await CancelPendingItemUseOnMoveAsync(player);
		if (!packet.IsGliding && player.IsInGlidingState())
		{
			var shouldBroadcastStopGlide = player.StopGliding();
			if (shouldBroadcastStopGlide)
				await BroadcastEmotionAsync(player, new SmEmotion(player, EmotionType.StopGlide));
			await UpdatePlayerStatsAndSpeedVisuallyAsync(player);
		}
		else
		{
			if (packet.IsGliding)
			{
				var glideResult = PlayerFlightActionService.StartGliding(player, DateTimeOffset.UtcNow);
				if (glideResult.SystemMessage != null)
					await SendPacketAsync(glideResult.SystemMessage);
				else if (glideResult.Succeeded && _connectionRegistry != null)
					await UpdatePlayerStatsAndSpeedVisuallyAsync(player);
			}
			else
				player.SetCreatureState(PlayerCreatureState.Gliding, enabled: false);
		}

		var movement = player.Movement;
		movement.Mask = packet.Type;

		if (packet.Type == MovementMask.Immediate)
		{
			movement.SetNewDirection(packet.X, packet.Y, packet.Z);
			movement.IsJumping = false;
		}
		else
		{
			if (packet.IsGliding)
			{
				movement.GlideFlag = packet.GlideFlag;
				movement.GeyserLocationId = packet.GeyserLocationId;
			}
			else
			{
				movement.GlideFlag = GlideFlag.None;
				movement.GeyserLocationId = 0;
			}

			if (packet.HasManualPosition)
			{
				movement.SetNewDirection(packet.TargetX, packet.TargetY, packet.TargetZ);
				if (!packet.IsAbsolute)
				{
					movement.VectorX = packet.VectorX;
					movement.VectorY = packet.VectorY;
					movement.VectorZ = packet.VectorZ;
				}
			}
			else if (!packet.IsAbsolute)
			{
				movement.SetNewDirection(
					packet.X + movement.VectorX,
					packet.Y + movement.VectorY,
					packet.Z + movement.VectorZ);
			}

			if (packet.IsVehicle)
			{
				movement.VehicleUnk1 = packet.VehicleUnk1;
				movement.VehicleUnk2 = packet.VehicleUnk2;
				movement.VehicleX = packet.VehicleX;
				movement.VehicleY = packet.VehicleY;
				movement.VehicleZ = packet.VehicleZ;
			}

			movement.IsJumping = packet.HasManualPosition
				&& !packet.IsAbsolute
				&& !packet.IsGliding
				&& !packet.IsVehicle
				&& packet.TargetZ > packet.Z;
		}

		// Java parity: CM_MOVE.runImpl checks isProtectionActive() before World.updatePosition and stops it if player has actually moved.
		// The z-threshold (+0.5f) in Java allows small fall drift without cancelling protection.
		var oldPosition = player.Position;
		if (player.IsProtectionActive()
			&& (oldPosition.X != packet.X || oldPosition.Y != packet.Y || oldPosition.Z > packet.Z + 0.5f))
		{
			player.StopProtectionActive();
			if (_connectionRegistry != null)
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, new SmPlayerState(player), includeSourcePlayer: true);
		}

		player.Position = player.Position with
		{
			X = packet.X,
			Y = packet.Y,
			Z = packet.Z,
			Heading = packet.Heading,
		};
		// Java parity: CM_MOVE.notifyControllers -> CreatureController.onMove/onStopMove -> ZoneUpdateService.revalidateZones.
		await RevalidatePlayerFlightZonesAsync(player);

		if (_connectionRegistry != null && (MovementMask.HasManualPosition(packet.Type) || packet.Type == MovementMask.Immediate))
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, new SmMove(player));
		await RefreshHousingVisibilityForPlayerAsync(player);
		await RefreshNpcVisibilityForPlayerAsync(player);
	}

	private static void HandleMoveInAir(Player player, CmMoveInAir packet)
	{
		// Java parity: network/aion/clientpackets/CM_MOVE_IN_AIR.runImpl position update.
		player.Movement.FlightDistance = packet.Distance;
		player.Position = new global::Aion.GameServer.World.WorldPosition(packet.WorldId, packet.X, packet.Y, packet.Z, packet.Heading);
	}

	private async Task HandleWindstreamAsync(Player player, CmWindstream packet)
	{
		// Java parity: network/aion/clientpackets/CM_WINDSTREAM.runImpl.
		switch (packet.State)
		{
			case 0:
				// Java parity: player.unsetPlayerMode(PlayerMode.RIDE).
				player.IsInRideMode = false;
				break;
			case 1: // entering windstream
				// Java parity: isUsingFlightTransporterOrWindstream() || !isFlying() guard.
				if ((player.FlightPathType != null && player.IsInState(PlayerCreatureState.Flying)) || !player.IsFlying())
					return;
				player.FlightPathType = PlayerFlightPathType.Windstream;
				player.SetCreatureState(PlayerCreatureState.Active, enabled: false);
				player.SetCreatureState(PlayerCreatureState.Gliding, enabled: false);
				player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
				player.UnsetFlyState(PlayerFlyState.Gliding);
				player.SetFlyState(PlayerFlyState.Flying);
				await BroadcastEmotionAsync(player, new SmEmotion(player, EmotionType.Windstream, packet.TeleportId, packet.Distance));
				player.TriggerFpRestore();
				// QuestEngine.onEnterWindStream deferred until quest engine is ported.
				return; // don't send SmWindstream for state 1
			case 2: // leaving windstream (gliding)
			case 3: // leaving windstream
				if (!player.IsUsingFlightPath(PlayerFlightPathType.Windstream))
					return;
				player.SetCreatureState(PlayerCreatureState.Flying, enabled: false);
				player.SetCreatureState(PlayerCreatureState.Active, enabled: true);
				player.UnsetFlyState(PlayerFlyState.Flying);
				player.UnsetFlyState(PlayerFlyState.Gliding);
				if (packet.State == 2)
					player.StartGliding(); // Java: FlyController.switchToGliding — return value ignored per Java source
				await UpdatePlayerStatsAndSpeedVisuallyAsync(player);
				player.FlightPathType = null;
				await BroadcastEmotionAsync(player, new SmEmotion(player,
					packet.State == 2 ? EmotionType.WindstreamEnd : EmotionType.WindstreamExit));
				// SM_TRANSFORM if player is transformed: deferred until transform system is ported.
				break;
			case 4:
				break;
			case 7: // start boost
			case 8: // end boost
				await BroadcastEmotionAsync(player, new SmEmotion(player,
					packet.State == 7 ? EmotionType.WindstreamStartBoost : EmotionType.WindstreamEndBoost));
				break;
			default:
				_logger.LogWarning("Unknown Windstream state #{State} from player {ObjectId}", packet.State, player.ObjectId);
				return;
		}
		await SendPacketAsync(new SmWindstream(packet.State, 1));
	}

	private async Task HandleReadExpressMailAsync(Player player, CmReadExpressMail packet)
	{
		// Java parity: network/aion/clientpackets/CM_READ_EXPRESS_MAIL.runImpl.
		switch (packet.Action)
		{
			case 0:
				await DismissPostmanAsync(player);
				break;
			case 1:
				var hasUnreadExpress = player.Mailbox.Any(mail => mail.IsUnreadExpress);
				var hasUnreadBlackCloud = player.Mailbox.Any(mail => mail.IsUnreadBlackCloud);
				if (player.HasSummonedPostman)
				{
					await SendPacketAsync(SmSystemMessage.PostmanAlreadySummoned());
				}
				else if (hasUnreadBlackCloud)
				{
					await SpawnPostmanAsync(player);
				}
				else if (hasUnreadExpress)
				{
					var now = DateTimeOffset.UtcNow;
					if (player.ExpressMailCooldownUntil > now)
					{
						await SendPacketAsync(SmSystemMessage.PostmanUnableInCooltime());
						return;
					}

					await SpawnPostmanAsync(player);
					player.ExpressMailCooldownUntil = now.AddMinutes(10);
				}
				break;
			default:
				_logger.LogWarning("Player {PlayerObjectId} sent unknown read express mail action type {Action}", player.ObjectId, packet.Action);
				break;
		}
	}

	private async Task HandleUseHouseObjectAsync(Player player, CmUseHouseObject packet)
	{
		// Java parity: network/aion/clientpackets/CM_USE_HOUSE_OBJECT delegates to PlaceableObjectController.onDialogRequest.
		var target = await FindHouseObjectUseTargetAsync(player, packet.ObjectId);
		if (target == null)
			return;
		if (!target.IsInTalkRange)
		{
			await SendPacketAsync(SmSystemMessage.HousingObjectTooFarToUse());
			return;
		}

		switch (target.Template.TypeId)
		{
			case 1:
				await HandleUseUseableItemObjectAsync(player, target);
				break;
			case 2:
				await HandleUseStorageObjectAsync(player, target);
				break;
			case 3:
				await HandleUsePostboxObjectAsync(player, target);
				break;
		}
	}

	private async Task HandleUseUseableItemObjectAsync(Player player, HouseObjectUseTarget target)
	{
		// Java parity: model/gameobjects/UseableItemObject.onUse pre-schedule validation.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null || _idFactory == null)
			return;

		var template = target.Template;
		if (!HasUseItemAction(template))
		{
			await SendPacketAsync(SmSystemMessage.HousingObjectAllCantUse());
			return;
		}

		var isOwner = target.House.OwnerObjectId == player.ObjectId;
		if (!isOwner && template.OwnerOnly)
		{
			await SendPacketAsync(SmSystemMessage.HousingObjectOnlyForOwnerValid());
			return;
		}

		if (HasActiveHouseObjectCooldown(player, target.HouseObject.ObjectId))
		{
			await SendPacketAsync(template.CooldownSeconds > 0
				? SmSystemMessage.HousingCannotUseFlowerpotCooltime()
				: SmSystemMessage.HousingObjectCantUsePerDay());
			return;
		}

		var currentUseCount = target.HouseObject.OwnerUseCount + target.HouseObject.VisitorUseCount;
		var mustGiveLastReward = MustGiveLastReward(template, target.HouseObject);
		if (template.UseCount > 0
			&& ((currentUseCount >= template.UseCount && !isOwner) || (currentUseCount > template.UseCount && isOwner)))
		{
			if (!mustGiveLastReward || !isOwner)
			{
				await SendPacketAsync(SmSystemMessage.HousingObjectAchieveUseCount());
				return;
			}
		}

		if (mustGiveLastReward && !isOwner)
		{
			await SendPacketAsync(SmSystemMessage.HousingObjectDeleteExpireTime(ChatUtil.L10n(template.NameId)));
			return;
		}

		if (string.Equals(template.Limit, "COOKING", StringComparison.OrdinalIgnoreCase) && template.UseActionRewardId > 0)
		{
			var rewardTemplate = staticData.ItemTemplates.GetItemTemplate(template.UseActionRewardId);
			if (player.InventoryItems.Any(item => item.ItemId == template.UseActionRewardId))
			{
				var rewardName = rewardTemplate?.GetClientName() ?? rewardTemplate?.Name ?? template.UseActionRewardId.ToString(System.Globalization.CultureInfo.InvariantCulture);
				await SendPacketAsync(SmSystemMessage.CannotUseAlreadyHaveRewardItem(rewardName, ChatUtil.L10n(template.NameId)));
				return;
			}
		}

		if (!ValidateUseableHouseObjectRequirement(player, template, staticData.ItemTemplates, out var failureMessage))
		{
			if (failureMessage != null)
				await SendPacketAsync(failureMessage);
			return;
		}

		if (!InventoryCapacity.HasFreeCubeSlot(player, staticData.ItemTemplates))
		{
			await SendPacketAsync(SmSystemMessage.WarehouseTooManyItemsInventory());
			return;
		}

		if (!TryOccupyHouseObject(target.HouseObject.ObjectId, player.ObjectId))
		{
			await SendPacketAsync(SmSystemMessage.HousingObjectOccupiedByOther());
			return;
		}

		var usedCount = template.UseCount == 0 ? 0 : currentUseCount + 1;
		await SendPacketAsync(SmSystemMessage.HousingObjectUse(ChatUtil.L10n(template.NameId)));
		await SendPacketAsync(new SmUseObject(player.ObjectId, target.HouseObject.ObjectId, template.DelayMilliseconds, 8));
		ScheduleHouseObjectUseCompletion(player, target, isOwner, usedCount);
	}

	private bool ValidateUseableHouseObjectRequirement(
		Player player,
		HousingObjectTemplateSummary template,
		ItemTemplateTable itemTemplates,
		out SmSystemMessage? failureMessage)
	{
		failureMessage = null;
		if (template.RequiredItemId == 0 && template.UseActionRemoveCount == 0)
			return true;
		if (template.RequiredItemId == 0 || template.UseActionRemoveCount == 0)
		{
			failureMessage = SmSystemMessage.HousingObjectAllCantUse();
			return false;
		}

		var requiredTemplate = itemTemplates.GetItemTemplate(template.RequiredItemId);
		var requiredName = requiredTemplate?.GetClientName() ?? requiredTemplate?.Name ?? template.RequiredItemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
		if (template.UseActionCheckType == 1)
		{
			var equipped = player.InventoryItems.Any(item => item.ItemId == template.RequiredItemId && item.IsEquipped);
			if (!equipped)
			{
				failureMessage = SmSystemMessage.CantUseHouseObjectItemEquip(requiredName);
				return false;
			}
			return true;
		}

		var available = player.InventoryItems
			.Where(item => item.ItemId == template.RequiredItemId && item.Location == CubeStorageId && !item.IsEquipped)
			.Sum(item => item.Count);
		if (available < template.UseActionRemoveCount)
		{
			failureMessage = SmSystemMessage.CantUseHouseObjectItemCheck(requiredName);
			return false;
		}

		return true;
	}

	private void ScheduleHouseObjectUseCompletion(Player player, HouseObjectUseTarget target, bool isOwner, int usedCount)
	{
		var delay = TimeSpan.FromMilliseconds(Math.Max(0, target.Template.DelayMilliseconds));
		if (_pendingHouseObjectUse?.Task.Cancel() == true)
			ReleaseHouseObjectOccupant(_pendingHouseObjectUse.ObjectId, player.ObjectId);

		if (_threadPoolManager == null || delay <= TimeSpan.Zero)
		{
			_ = CompleteUseableHouseObjectAsync(player, target, isOwner, usedCount, CancellationToken.None);
			return;
		}

		ScheduledTask? scheduledTask = null;
		scheduledTask = _threadPoolManager.Schedule(
			async cancellationToken =>
			{
				try
				{
					if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_activePlayer, player))
						return;

					await CompleteUseableHouseObjectAsync(player, target, isOwner, usedCount, cancellationToken);
				}
				finally
				{
					var pendingUse = _pendingHouseObjectUse;
					if (pendingUse != null && ReferenceEquals(pendingUse.Task, scheduledTask))
						_pendingHouseObjectUse = null;
				}
			},
			delay);
		_pendingHouseObjectUse = new PendingHouseObjectUse(scheduledTask, target.HouseObject.ObjectId);
	}

	private async Task CompleteUseableHouseObjectAsync(
		Player player,
		HouseObjectUseTarget target,
		bool isOwner,
		int usedCount,
		CancellationToken cancellationToken)
	{
		// Java parity: UseableItemObject scheduled HOUSE_OBJECT_USE task completion.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null || _idFactory == null)
			return;

		await SendPacketAsync(new SmUseObject(player.ObjectId, target.HouseObject.ObjectId, 0, 9), cancellationToken);
		var inventoryItems = player.InventoryItems.ToList();
		InventoryItemConsumption? consumption = null;
		if (target.Template is { RequiredItemId: > 0, UseActionRemoveCount: > 0 })
		{
			consumption = BuildItemCountConsumption(inventoryItems, target.Template.RequiredItemId, target.Template.UseActionRemoveCount);
			if (consumption == null)
				return;
		}

		var rewardId = 0;
		var deleteHouseObject = false;
		var markFinalRewardPending = false;
		if (target.Template.UseCount > 0)
		{
			if (target.Template.UseActionFinalRewardId > 0 && target.Template.UseCount + 1 == usedCount)
			{
				rewardId = target.Template.UseActionFinalRewardId;
				deleteHouseObject = true;
			}
			else if (target.Template.UseActionRewardId > 0)
			{
				rewardId = target.Template.UseActionRewardId;
				if (target.Template.UseCount == usedCount)
				{
					await SendPacketAsync(SmSystemMessage.HousingFlowerpotGoal(ChatUtil.L10n(target.Template.NameId)), cancellationToken);
					if (target.Template.UseActionFinalRewardId == 0)
						deleteHouseObject = true;
					else
						markFinalRewardPending = true;
				}
			}
		}
		else if (target.Template.UseActionRewardId > 0)
		{
			rewardId = target.Template.UseActionRewardId;
		}

		var rewardTemplate = rewardId == 0 ? null : staticData.ItemTemplates.GetItemTemplate(rewardId);
		var rewardPlan = rewardTemplate == null
			? InventoryAddPlan.Empty
			: InventoryAddService.CreateAddItemPlan(player, inventoryItems, rewardTemplate, 1, () => _idFactory.NextId(), itemTemplates: staticData.ItemTemplates);
		if (!rewardPlan.Succeeded)
		{
			await SendPacketAsync(SmSystemMessage.WarehouseTooManyItemsInventory(), cancellationToken);
			return;
		}

		var updatedHouseObject = target.HouseObject;
		if (usedCount > 0 && !deleteHouseObject)
		{
			updatedHouseObject = isOwner
				? target.HouseObject.WithUseCounts(
					target.Template,
					target.HouseObject.OwnerUseCount + 1,
					target.HouseObject.VisitorUseCount)
				: target.HouseObject.WithUseCounts(
					target.Template,
					target.HouseObject.OwnerUseCount,
					target.HouseObject.VisitorUseCount + 1);
			if (markFinalRewardPending)
			{
				var nowSeconds = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
				updatedHouseObject = updatedHouseObject.WithExpireTimeSeconds(nowSeconds, () => nowSeconds);
			}
		}

		var saved = await _housingRepository.SaveHouseObjectUseAsync(
			target.House.OwnerObjectId,
			player.ObjectId,
			deleteHouseObject ? null : updatedHouseObject,
			deleteHouseObject ? target.HouseObject.ObjectId : null,
			consumption?.Updates ?? Array.Empty<InventoryItem>(),
			consumption?.Deletes.Select(item => item.ObjectId).ToArray() ?? Array.Empty<int>(),
			rewardPlan.UpdatedItems,
			rewardPlan.AddedItems,
			cancellationToken);
		if (!saved)
			return;

		await SendConsumedItemPacketsAsync(
			player.InventoryItems,
			consumption?.Updates ?? Array.Empty<InventoryItem>(),
			consumption?.Deletes.Select(item => item.ObjectId).ToArray() ?? Array.Empty<int>(),
			staticData.ItemTemplates);
		ApplyHouseObjectUseInventoryMutation(inventoryItems, consumption, rewardPlan);
		player.InventoryItems = inventoryItems.ToArray();
		if (rewardTemplate != null)
		{
			foreach (var updatedReward in rewardPlan.UpdatedItems)
				await SendPacketAsync(
					new SmInventoryUpdateItem(
						updatedReward,
						rewardTemplate,
						SmInventoryUpdateItem.IncreaseItemCollect,
						GetGeneralInfoWarehouseRestrictionFlag(updatedReward.ItemId, staticData.ItemRestrictionCleanups)),
					cancellationToken);
			foreach (var addedReward in rewardPlan.AddedItems)
				await SendPacketAsync(
					SmInventoryAddItem.CreateItemCollect(
						addedReward,
						rewardTemplate,
						GetGeneralInfoWarehouseRestrictionFlag(addedReward.ItemId, staticData.ItemRestrictionCleanups)),
					cancellationToken);
			await SendPacketAsync(
				SmSystemMessage.HousingObjectRewardItem(ChatUtil.L10n(target.Template.NameId), rewardTemplate.GetClientName() ?? rewardTemplate.Name),
				cancellationToken);
		}

		if (!deleteHouseObject && updatedHouseObject != target.HouseObject)
			UpdateHouseObjectInRegistries(player, target, updatedHouseObject);

		await BroadcastHouseObjectUseUpdateAsync(player, target, usedCount, deleteHouseObject ? target.HouseObject : updatedHouseObject);
		if (deleteHouseObject)
		{
			await SendHouseObjectUseDeletePacketsAsync(player, target, cancellationToken);
			RemoveHouseObjectFromRegistries(player, target);
			RemoveHouseObjectCooldownFromOnlinePlayers(player, target.HouseObject.ObjectId);
			_idFactory?.ReleaseId(target.HouseObject.ObjectId);
			ReleaseHouseObjectOccupant(target.HouseObject.ObjectId, player.ObjectId);
			return;
		}

		AddHouseObjectCooldown(player, target.HouseObject.ObjectId, target.Template);
	}

	private async Task BroadcastHouseObjectUseUpdateAsync(
		Player player,
		HouseObjectUseTarget target,
		int usedCount,
		RegisteredHouseObjectSummary houseObject)
	{
		var updatePacket = new SmObjectUseUpdate(player.ObjectId, target.House.OwnerObjectId, usedCount, houseObject);
		if (_connectionRegistry == null)
		{
			await SendPacketAsync(updatePacket);
			return;
		}

		await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			new global::Aion.GameServer.World.WorldPosition(target.House.Position.WorldId, houseObject.X, houseObject.Y, houseObject.Z, (byte)houseObject.Heading),
			player.ObjectId,
			updatePacket,
			includeSourcePlayer: true);
	}

	private async Task SendHouseObjectUseDeletePacketsAsync(Player player, HouseObjectUseTarget target, CancellationToken cancellationToken)
	{
		// Java parity: HouseObject.despawnAndRemoveHouseObject(false).
		if (target.HouseObject.IsSpawnedByPlayer)
		{
			await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DespawnObject, 0, target.HouseObject.ObjectId), cancellationToken);
			var deletePacket = new SmDeleteHouseObject(target.HouseObject.ObjectId);
			if (_connectionRegistry != null)
			{
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(
					new global::Aion.GameServer.World.WorldPosition(target.House.Position.WorldId, target.HouseObject.X, target.HouseObject.Y, target.HouseObject.Z, (byte)target.HouseObject.Heading),
					player.ObjectId,
					deletePacket,
					includeSourcePlayer: true);
			}
			else
			{
				await SendPacketAsync(deletePacket, cancellationToken);
			}
		}

		await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DeleteItem, 1, target.HouseObject.ObjectId), cancellationToken);
		await SendPacketAsync(
			SmSystemMessage.HousingObjectDeleteUseCountFinal(ChatUtil.L10n(target.Template.NameId)),
			cancellationToken);
	}

	private async Task ExpireHouseObjectAsync(
		Player player,
		PlayerHouse house,
		RegisteredHouseObjectSummary houseObject,
		HousingObjectTemplateSummary? template)
	{
		// Java parity: HouseObject.onExpire -> despawnAndRemoveHouseObject(true).
		if (!await _housingRepository.DeleteHouseRegisteredObjectAsync(player.ObjectId, houseObject.ObjectId))
			return;

		await SendExpiredHouseObjectPacketsAsync(player, house, houseObject, template);
		var updatedRegistry = (house.Registry ?? HouseRegistrySummary.Empty).WithoutObject(houseObject.ObjectId);
		UpdateHouseRegistry(player, house, updatedRegistry);
		RemoveHouseObjectCooldownFromOnlinePlayers(player, houseObject.ObjectId);
		_idFactory?.ReleaseId(houseObject.ObjectId);
	}

	private async Task SendExpiredHouseObjectPacketsAsync(
		Player player,
		PlayerHouse house,
		RegisteredHouseObjectSummary houseObject,
		HousingObjectTemplateSummary? template)
	{
		if (houseObject.IsSpawnedByPlayer)
		{
			await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DespawnObject, 0, houseObject.ObjectId));
			var deletePacket = new SmDeleteHouseObject(houseObject.ObjectId);
			var housingTemplates = _runtimeContext?.DataManager?.StaticData.HousingTemplates;
			var worldId = housingTemplates?.GetAddress(house.AddressId)?.MapId ?? player.Position.WorldId;
			if (_connectionRegistry != null)
			{
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(
					new global::Aion.GameServer.World.WorldPosition(worldId, houseObject.X, houseObject.Y, houseObject.Z, (byte)houseObject.Heading),
					player.ObjectId,
					deletePacket,
					includeSourcePlayer: true);
			}
			else
			{
				await SendPacketAsync(deletePacket);
			}
		}

		await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DeleteItem, 1, houseObject.ObjectId));
		await SendPacketAsync(SmSystemMessage.HousingObjectDeleteExpireTime(GetHouseObjectName(houseObject, template)));
	}

	private static void ApplyHouseObjectUseInventoryMutation(
		List<InventoryItem> inventoryItems,
		InventoryItemConsumption? consumption,
		InventoryAddPlan rewardPlan)
	{
		if (consumption != null)
		{
			foreach (var updatedConsumedItem in consumption.Updates)
				ReplaceInventoryItem(inventoryItems, updatedConsumedItem);
			foreach (var deletedConsumedItem in consumption.Deletes)
				inventoryItems.RemoveAll(item => item.ObjectId == deletedConsumedItem.ObjectId);
		}

		foreach (var updatedReward in rewardPlan.UpdatedItems)
			ReplaceInventoryItem(inventoryItems, updatedReward);
		inventoryItems.AddRange(rewardPlan.AddedItems);
	}

	private void UpdateHouseObjectInRegistries(Player player, HouseObjectUseTarget target, RegisteredHouseObjectSummary updatedHouseObject)
	{
		var updatedRegistry = (target.House.Registry ?? HouseRegistrySummary.Empty).WithObject(updatedHouseObject);
		_world?.AddOrUpdateHouse(target.House with { Registry = updatedRegistry });
		if (target.House.OwnerObjectId == player.ObjectId)
			UpdateHouseRegistryIfOwned(player, target.House.ObjectId, updatedRegistry);
	}

	private void RemoveHouseObjectFromRegistries(Player player, HouseObjectUseTarget target)
	{
		var updatedRegistry = (target.House.Registry ?? HouseRegistrySummary.Empty).WithoutObject(target.HouseObject.ObjectId);
		_world?.AddOrUpdateHouse(target.House with { Registry = updatedRegistry });
		if (target.House.OwnerObjectId == player.ObjectId)
			UpdateHouseRegistryIfOwned(player, target.House.ObjectId, updatedRegistry);
	}

	private static void UpdateHouseRegistryIfOwned(Player player, int houseObjectId, HouseRegistrySummary registry)
	{
		player.Houses = player.Houses
			.Select(house => house.ObjectId == houseObjectId ? house with { Registry = registry } : house)
			.ToArray();
	}

	private static bool HasUseItemAction(HousingObjectTemplateSummary template)
	{
		return template.UseActionCheckType != 0
			|| template.UseActionRemoveCount != 0
			|| template.UseActionRewardId != 0
			|| template.UseActionFinalRewardId != 0;
	}

	private static bool MustGiveLastReward(HousingObjectTemplateSummary template, RegisteredHouseObjectSummary houseObject)
	{
		// Java parity: UseableItemObject.mustGiveLastReward is restored when a final-reward object has expired.
		return template.UseActionFinalRewardId > 0
			&& houseObject.ExpireTimeSeconds > 0
			&& houseObject.ExpireTimeSeconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}

	private static bool HasActiveHouseObjectCooldown(Player player, int objectId)
	{
		return player.HouseObjectCooldowns.TryGetValue(objectId, out var reuseTimeMillis)
			&& reuseTimeMillis > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	}

	private static void AddHouseObjectCooldown(Player player, int objectId, HousingObjectTemplateSummary template)
	{
		var cooldowns = player.HouseObjectCooldowns.ToDictionary(pair => pair.Key, pair => pair.Value);
		cooldowns[objectId] = template.CooldownSeconds > 0
			? DateTimeOffset.UtcNow.AddSeconds(template.CooldownSeconds).ToUnixTimeMilliseconds()
			: new DateTimeOffset(DateTime.Today.AddDays(1).AddTicks(-1)).ToUnixTimeMilliseconds();
		player.HouseObjectCooldowns = cooldowns;
	}

	private static void RemoveHouseObjectCooldown(Player player, int objectId)
	{
		if (!player.HouseObjectCooldowns.ContainsKey(objectId))
			return;

		var cooldowns = player.HouseObjectCooldowns.ToDictionary(pair => pair.Key, pair => pair.Value);
		cooldowns.Remove(objectId);
		player.HouseObjectCooldowns = cooldowns;
	}

	private void RemoveHouseObjectCooldownFromOnlinePlayers(Player owner, int objectId)
	{
		// Java parity: HouseRegistry.discard removes useable house-object cooldowns from World.forEachPlayer.
		var removedFromOwner = false;
		if (_connectionRegistry != null)
		{
			_connectionRegistry.ForEachOnlinePlayer(
				player =>
				{
					if (player.ObjectId == owner.ObjectId)
						removedFromOwner = true;
					RemoveHouseObjectCooldown(player, objectId);
				});
		}

		if (!removedFromOwner)
			RemoveHouseObjectCooldown(owner, objectId);
	}

	private static string GetHouseObjectName(RegisteredHouseObjectSummary houseObject, HousingObjectTemplateSummary? template)
	{
		return template?.NameId > 0
			? ChatUtil.L10n(template.NameId)
			: houseObject.TemplateId.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	private async Task HandleUseStorageObjectAsync(Player player, HouseObjectUseTarget target)
	{
		// Java parity: model/gameobjects/StorageObject.onUse.
		if (player.ObjectId != target.House.OwnerObjectId)
		{
			await SendPacketAsync(SmSystemMessage.HousingObjectOnlyForOwnerValid());
			return;
		}

		if (!TryOccupyHouseObject(target.HouseObject.ObjectId, player.ObjectId))
		{
			await SendPacketAsync(SmSystemMessage.HousingObjectOccupiedByOther());
			return;
		}

		await SendPacketAsync(SmSystemMessage.HousingObjectUse(ChatUtil.L10n(target.Template.NameId)));
		await SendPacketAsync(new SmObjectUseUpdate(player.ObjectId, 0, 0, target.HouseObject));
	}

	private async Task HandleUsePostboxObjectAsync(Player player, HouseObjectUseTarget target)
	{
		// Java parity: model/gameobjects/PostboxObject.onUse.
		if (!TryOccupyHouseObject(target.HouseObject.ObjectId, player.ObjectId))
		{
			await SendPacketAsync(SmSystemMessage.HousingObjectOccupiedByOther());
			return;
		}

		player.MailboxState = Player.MailboxRegularState;
		await SendPacketAsync(SmSystemMessage.HousingObjectUse(ChatUtil.L10n(target.Template.NameId)));
		await SendPacketAsync(new SmDialogWindow(
			target.HouseObject.ObjectId,
			SmDialogWindow.MailPageId,
			dialogContextId: player.MailboxState));
		await SendPacketAsync(new SmObjectUseUpdate(player.ObjectId, 0, 0, target.HouseObject));
	}

	private async Task HandleReleaseObjectAsync(Player player, CmReleaseObject packet)
	{
		// Java parity: network/aion/clientpackets/CM_RELEASE_OBJECT.
		var target = await FindHouseObjectUseTargetAsync(player, packet.TargetObjectId);
		if (target == null || !ReleaseHouseObjectOccupant(target.HouseObject.ObjectId, player.ObjectId))
			return;

		var canceledHouseObjectUse = CancelPendingHouseObjectUse(target.HouseObject.ObjectId);
		if (target.Template.TypeId == 1)
			await SendPacketAsync(new SmUseObject(player.ObjectId, target.HouseObject.ObjectId, 0, 9));
		if (target.Template.TypeId == 3 || canceledHouseObjectUse)
			await SendPacketAsync(SmSystemMessage.HousingObjectCancelUse());
	}

	private bool CancelPendingHouseObjectUse(int objectId)
	{
		var pendingUse = _pendingHouseObjectUse;
		if (pendingUse == null || pendingUse.ObjectId != objectId)
			return false;

		_pendingHouseObjectUse = null;
		return pendingUse.Task.Cancel();
	}

	private async Task<HouseObjectUseTarget?> FindHouseObjectUseTargetAsync(Player player, int objectId)
	{
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null || objectId == 0)
			return null;

		if (_world != null)
		{
			foreach (var house in _world.GetHouses())
			{
				if (TryCreateHouseObjectUseTarget(player, house, objectId, staticData.HousingObjectTemplates, out var target))
					return target;
			}
		}

		var activeHouse = GetActiveHouse(player);
		if (activeHouse == null)
			return null;

		var registry = await LoadHouseRegistryAsync(player, activeHouse);
		var houseWithRegistry = player.Houses.FirstOrDefault(house => house.ObjectId == activeHouse.ObjectId) ?? activeHouse with { Registry = registry };
		if (!WorldHouse.TryCreate(player, houseWithRegistry, staticData.HousingTemplates, out var worldHouse) || worldHouse == null)
			return null;

		return TryCreateHouseObjectUseTarget(player, worldHouse, objectId, staticData.HousingObjectTemplates, out var ownedTarget)
			? ownedTarget
			: null;
	}

	private static bool TryCreateHouseObjectUseTarget(
		Player player,
		WorldHouse house,
		int objectId,
		HousingObjectTemplateTable templates,
		out HouseObjectUseTarget? target)
	{
		target = null;
		var houseObject = house.Registry?.GetObject(objectId);
		if (houseObject == null || !houseObject.IsSpawnedByPlayer)
			return false;

		var template = templates.GetTemplate(houseObject.TemplateId);
		if (template == null)
			return false;

		target = new HouseObjectUseTarget(
			house,
			houseObject,
			template,
			IsInHouseObjectTalkRange(player, house, houseObject, template));
		return true;
	}

	private static bool IsInHouseObjectTalkRange(
		Player player,
		WorldHouse house,
		RegisteredHouseObjectSummary houseObject,
		HousingObjectTemplateSummary template)
	{
		// Java parity: PositionUtil.isInTalkRange(player, HouseObject) delegates to isInRange(..., false).
		return PositionUtilService.IsInObjectTalkRange(
			player.Position,
			new global::Aion.GameServer.World.WorldPosition(
				house.Position.WorldId,
				houseObject.X,
				houseObject.Y,
				houseObject.Z,
				(byte)houseObject.Rotation,
				house.Position.InstanceId),
			template.TalkingDistance,
			targetBoundRadius: 0,
			player.BoundRadius);
	}

	private static bool TryOccupyHouseObject(int objectId, int playerObjectId)
	{
		// Java parity: UseableHouseObject.setOccupant compare-and-set semantics.
		while (true)
		{
			if (HouseObjectOccupants.TryAdd(objectId, playerObjectId))
				return true;
			if (!HouseObjectOccupants.TryGetValue(objectId, out var occupantObjectId))
				continue;
			return occupantObjectId == playerObjectId;
		}
	}

	private static bool ReleaseHouseObjectOccupant(int objectId, int playerObjectId)
	{
		// Java parity: UseableHouseObject.releaseOccupant only frees the current player's object.
		return ((ICollection<KeyValuePair<int, int>>)HouseObjectOccupants)
			.Remove(new KeyValuePair<int, int>(objectId, playerObjectId));
	}

	private static void ReleaseHouseObjectOccupants(int playerObjectId)
	{
		foreach (var pair in HouseObjectOccupants.Where(pair => pair.Value == playerObjectId).ToArray())
			((ICollection<KeyValuePair<int, int>>)HouseObjectOccupants).Remove(pair);
	}

	private bool CanRuntimeHouseObjectExpireNow(RegisteredHouseObjectSummary houseObject, HousingObjectTemplateSummary? template)
	{
		if (template == null)
			return true;

		// Java parity: UseableHouseObject.canExpireNow waits until the current player releases the object.
		if (template.TypeId is 1 or 2 or 3 && IsHouseObjectOccupied(houseObject.ObjectId))
			return false;
		// Java parity: NpcObject.canExpireNow waits while the spawned housing NPC has a target.
		if (template.TypeId == 7 && houseObject.NpcObjectId != 0 && IsHouseNpcObjectTargeted(houseObject.NpcObjectId))
			return false;
		return true;
	}

	private static bool IsHouseObjectOccupied(int objectId)
	{
		return HouseObjectOccupants.ContainsKey(objectId);
	}

	private bool IsHouseNpcObjectTargeted(int npcObjectId)
	{
		if (_connectionRegistry == null)
			return false;

		var targeted = false;
		_connectionRegistry.ForEachOnlinePlayer(player =>
		{
			if (player.TargetObjectId == npcObjectId)
				targeted = true;
		});
		return targeted;
	}

	internal async Task SpawnPostmanAsync(Player player)
	{
		// Java parity: spawnengine/VisibleObjectSpawner.spawnPostman.
		if (_runtimeContext?.DataManager?.StaticData.NpcTemplates == null || _idFactory == null)
		{
			player.HasSummonedPostman = true;
			return;
		}

		var npcId = string.Equals(player.Race, "ELYOS", StringComparison.OrdinalIgnoreCase) ? 798100 : 798101;
		var template = _runtimeContext.DataManager.StaticData.NpcTemplates.GetNpcTemplate(npcId);
		if (template == null)
		{
			player.HasSummonedPostman = true;
			return;
		}

		var postman = PostmanNpc.Create(player, _idFactory.NextId(), template);
		player.Postman = postman;
		player.HasSummonedPostman = true;
		if (_world?.TryAddObject(postman.ObjectId, postman) == true)
			RevalidatePostmanCreaturePvpZones(postman);
		var postmanPacket = new SmNpcInfo(postman);
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(postman.Position, postman.ObjectId, postmanPacket, includeSourcePlayer: true);
		else
			await SendPacketAsync(postmanPacket);
	}

	internal async Task DismissPostmanAsync(Player player, bool notifyClient = true)
	{
		// Java parity: CM_READ_EXPRESS_MAIL action 0 deletes Player.getPostman.
		var postman = player.Postman;
		player.Postman = null;
		player.HasSummonedPostman = false;
		if (postman == null)
			return;

		if (notifyClient)
		{
			var deletePacket = new SmDelete(postman.ObjectId);
			if (_connectionRegistry != null)
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(postman.Position, postman.ObjectId, deletePacket, includeSourcePlayer: true);
			else
				await SendPacketAsync(deletePacket);
		}

		if (_world?.TryRemoveObject(postman.ObjectId, out _) == true)
			ClearPostmanCreaturePvpZones(postman.ObjectId);
		if (_idFactory != null)
			_idFactory.ReleaseId(postman.ObjectId);
	}

	private void RevalidatePostmanCreaturePvpZones(PostmanNpc postman)
	{
		// Java parity: VisibleObjectSpawner.spawnPostman -> SpawnEngine.bringIntoWorld -> World.spawn -> MapRegion.revalidateZones.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		CreaturePvpZoneRevalidationService.Revalidate(
			postman.ObjectId,
			postman.Position,
			staticData?.CreaturePvpZones,
			_creaturePvpZoneCounterService);
	}

	private void ClearPostmanCreaturePvpZones(int objectId)
	{
		// Java parity: CM_READ_EXPRESS_MAIL action 0 deletes the postman NPC and leaves its zone memberships.
		_creaturePvpZoneCounterService?.ClearCounters(objectId);
	}

	private bool IsTargetingBroker(Player player, int brokerObjectId, string action)
	{
		// Java parity: Player.isTargetingNpcWithFunction(brokerObjId, DialogAction.OPEN_VENDOR) in CM_BROKER_* runImpl methods.
		var targeting = NpcDialogTargetingService.ValidateTargetingNpcWithFunction(player, brokerObjectId, OpenVendorDialogAction, _world);
		if (targeting == NpcDialogTargetingResult.Valid)
			return true;

		_logger.LogWarning(
			"Player {PlayerObjectId} tried to {Action} without targeting broker {BrokerObjectId}; current target is {TargetObjectId}, validation result {ValidationResult}",
			player.ObjectId,
			action,
			brokerObjectId,
			player.TargetObjectId,
			targeting);
		return false;
	}

	private async Task<bool> CanOperateItemAsync(Player player, InventoryItem item, string type)
	{
		// Java parity: services/AdminService.canOperate(player, null, item, type).
		if (player.AccessLevel == 0)
			return true;
		if (player.AccessLevel >= _options.Administration.UnrestrictedItemTradeAccessLevel)
			return true;
		if (_options.Administration.OperationalItemIds.Contains(item.ItemId))
		{
			_logger.LogInformation(
				"Staff player {PlayerObjectId} used item {ItemId} via {Type} under AdminService item restriction allow-list",
				player.ObjectId,
				item.ItemId,
				type);
			return true;
		}

		_logger.LogWarning(
			"Staff player {PlayerObjectId} cannot use item {ItemId} via {Type}; item is not in AdminService item restriction allow-list",
			player.ObjectId,
			item.ItemId,
			type);
		await SendPacketAsync(new SmMessage($"You cannot use {type} with this item."));
		return false;
	}

	private static bool CanTrade(Player player)
	{
		// Java parity: restrictions/PlayerRestrictions.canTrade baseline.
		return player.IsOnline
			&& !player.IsTrading
			&& (player.LifeStats == null || player.LifeStats.CurrentHp > 0);
	}

	private async Task<PlayerBrokerItemPage> LoadBrokerMaskPageAsync(Player player, byte sortType, int pageIndex, int brokerMask)
	{
		// Java parity: services/BrokerService.showRequestedItems direct BrokerItemMask filtering path.
		if (_brokerRepository == null || brokerMask == 0)
			return new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, pageIndex, 0);

		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		if (itemTemplates == null)
			return new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, pageIndex, 0);

		var recipeTemplates = _runtimeContext?.DataManager?.StaticData.RecipeTemplates;
		var activeItems = await _brokerRepository.LoadActiveItemsAsync(player.Race);
		var filteredItems = activeItems
			.Where(
				item =>
					item.Item != null
					&& itemTemplates.GetItemTemplate(item.ItemId) is { } template
					&& BrokerItemMaskMatcher.Matches(brokerMask, template, recipeTemplates))
			.ToArray();
		var sortedItems = SortBrokerItems(filteredItems, sortType, itemTemplates).ToArray();
		var start = Math.Max(0, pageIndex) * 9;
		var pageItems = start >= sortedItems.Length
			? Array.Empty<PlayerBrokerItem>()
			: sortedItems.Skip(start).Take(45).ToArray();
		return new PlayerBrokerItemPage(pageItems, sortedItems.Length, pageIndex, 0);
	}

	private async Task<PlayerBrokerItemPage> LoadCachedBrokerPageAsync(Player player)
	{
		// Java parity: BrokerPlayerCache refresh after BrokerService.buyBrokerItem.
		if (_brokerRepository != null && player.BrokerMaskCache == 0 && player.BrokerSearchItemIds.Count > 0)
		{
			return await _brokerRepository.SearchItemsByTemplateIdsAsync(
				player.Race,
				player.BrokerSortTypeCache,
				player.BrokerStartPageCache,
				player.BrokerSearchItemIds);
		}

		return await LoadBrokerMaskPageAsync(player, player.BrokerSortTypeCache, player.BrokerStartPageCache, player.BrokerMaskCache);
	}

	private async Task HandleRegisterHouseAsync(Player player, CmRegisterHouse packet)
	{
		// Java parity: network/aion/clientpackets/CM_REGISTER_HOUSE.runImpl.
		if (!IsHouseAuctionRegistrationAllowed(_options.Housing) || packet.BidKinah <= 0)
		{
			await SendPacketAsync(SmSystemMessage.HousingCantAuctionTimeout());
			return;
		}

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var activeHouse = GetActiveAuctionHouse(player, staticData?.HousingTemplates);
		if (activeHouse == null)
			return;

		if (_options.Housing.PayEnabled && !IsHouseFeePaid(activeHouse))
		{
			await SendPacketAsync(SmSystemMessage.HousingCantAuctionOverdue());
			return;
		}

		var fee = (long)(packet.BidKinah * _options.Housing.AuctionRegistrationFeePercent);
		var kinahItem = player.InventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem == null || kinahItem.Count < fee)
		{
			await SendPacketAsync(SmSystemMessage.NotEnoughKinah(fee));
			return;
		}

		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - fee);
		var result = await _houseAuctionRepository.RegisterHouseAuctionAsync(
			player.ObjectId,
			activeHouse.ObjectId,
			packet.BidKinah,
			kinahUpdate,
			DateTime.Now);

		if (result == HouseAuctionRegistrationResult.AlreadyRegistered)
		{
			await SendPacketAsync(SmSystemMessage.HousingAuctionAlreadyRegistered());
			return;
		}

		if (result != HouseAuctionRegistrationResult.Success)
		{
			await SendPacketAsync(SmSystemMessage.HousingCantAuctionTimeout());
			return;
		}

		player.InventoryItems = player.InventoryItems
			.Select(item => item.ObjectId == kinahUpdate.ObjectId ? kinahUpdate : item)
			.ToArray();

		if (staticData?.ItemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(kinahUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
		await SendPacketAsync(SmSystemMessage.HousingAuctionMyHouse(activeHouse.AddressId));
		await SendPacketAsync(new SmReceiveBids(0));
	}

	private async Task HandlePlaceBidAsync(Player player, CmPlaceBid packet)
	{
		// Java parity: network/aion/clientpackets/CM_PLACE_BID.runImpl -> services/HousingBidService.bid.
		if (!_options.Housing.AuctionsEnabled)
			return;
		if (!await CanOwnHouseForAuctionAsync(player))
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var context = await _houseAuctionRepository.LoadHouseBidContextAsync(
			player.ObjectId,
			packet.ListIndex,
			staticData?.HousingTemplates);
		if (context == null)
		{
			await SendPacketAsync(SmSystemMessage.HousingBidFail());
			return;
		}

		if (!_houseAuctionTiming.IsBiddingTime(context.HouseObjectId))
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidTimeout());
			return;
		}

		if (player.ObjectId == context.OwnerObjectId)
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidMyHouse());
			return;
		}

		if (player.Houses.Any(house => house.IsInactive))
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidGraceHouse());
			return;
		}

		var activeHouse = player.Houses.FirstOrDefault(house => !house.IsInactive);
		if (_options.Housing.PayEnabled && activeHouse != null && !IsHouseFeePaid(activeHouse))
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidOverdue());
			return;
		}

		var minBidLevel = GetMinBidLevel(context, _options.Housing);
		if (minBidLevel > 0 && GetPlayerLevel(player) < minBidLevel)
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidLowLevel(minBidLevel));
			return;
		}

		if (context.CurrentBidderObjectId == player.ObjectId)
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidAlreadyHighest());
			return;
		}

		if (context.PlayerIsHighestBidderElsewhere)
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidOtherHouse());
			return;
		}

		var kinahItem = player.InventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem == null || kinahItem.Count < packet.BidOffer)
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidNotEnoughMoney(packet.BidOffer));
			return;
		}

		if (packet.BidOffer - context.CurrentBidKinah >= context.CurrentBidKinah * _options.Housing.AuctionBidStepLimit / 100f)
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidExcessAccount());
			return;
		}

		var refundMailObjectId = _idFactory?.NextId() ?? 0;
		if (refundMailObjectId == 0 && !context.IsCurrentBidInitialOffer && context.CurrentBidderObjectId != 0)
		{
			_logger.LogWarning(
				"Cannot create housing auction refund mail after bid on list index {ListIndex}; IDFactory is unavailable",
				packet.ListIndex);
			await SendPacketAsync(SmSystemMessage.HousingBidFail());
			return;
		}

		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - packet.BidOffer);
		// Java parity: taskmanager/tasks/housing/AuctionEndTask.tryProlongAuction before HouseBidsDAO.addBid.
		if (!_houseAuctionTiming.TryProlongAuction(context.HouseObjectId))
		{
			await SendPacketAsync(SmSystemMessage.HousingCantBidTimeout());
			return;
		}

		var result = await _houseAuctionRepository.PlaceHouseBidAsync(
			player.ObjectId,
			packet.ListIndex,
			packet.BidOffer,
			kinahUpdate,
			refundMailObjectId,
			DateTime.Now,
			staticData?.HousingTemplates);

		switch (result.Status)
		{
			case HouseAuctionPlaceBidStatus.Missing:
				await SendPacketAsync(SmSystemMessage.HousingBidFail());
				return;
			case HouseAuctionPlaceBidStatus.PriceChanged:
				await SendPacketAsync(SmSystemMessage.HousingCantBidLower());
				await SendPacketAsync(new SmReceiveBids(0));
				return;
			case HouseAuctionPlaceBidStatus.Failed:
				await SendPacketAsync(SmSystemMessage.HousingBidFail());
				return;
		}

		if (result.KinahItem != null)
		{
			player.InventoryItems = player.InventoryItems
				.Select(item => item.ObjectId == result.KinahItem.ObjectId ? result.KinahItem : item)
				.ToArray();

			if (staticData?.ItemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
				await SendPacketAsync(new SmInventoryUpdateItem(result.KinahItem, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
		}

		if (_connectionRegistry != null && result.PreviousBidRefundMail != null && result.PreviousBidderObjectId != 0)
		{
			await _connectionRegistry.SendPacketToPlayerAsync(result.PreviousBidderObjectId, SmSystemMessage.HousingBidCancel());
			await _connectionRegistry.SendPacketToPlayerAsync(result.PreviousBidderObjectId, new SmReceiveBids(0));
			await _connectionRegistry.NotifyMailReceivedAsync(result.PreviousBidderObjectId, result.PreviousBidRefundMail);
		}

		await SendPacketAsync(SmSystemMessage.HousingBidSuccess(result.AddressId));
		await SendPacketAsync(SmSystemMessage.HousingPriceChange(packet.BidOffer));
		await SendPacketAsync(new SmReceiveBids(0));
	}

	private async Task HandleHousePayRentAsync(Player player, CmHousePayRent packet)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_PAY_RENT.runImpl.
		var activeHouse = player.Houses.FirstOrDefault(house => !house.IsInactive);
		if (activeHouse == null)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var maintenanceFee = staticData?.HousingTemplates.GetAddress(activeHouse.AddressId)?.MaintenanceFee ?? 0;
		var cost = _options.Housing.PayEnabled ? maintenanceFee * packet.WeekCount : 0;
		if (cost <= 0)
		{
			await SendPacketAsync(SmSystemMessage.HousingFeeFree());
			return;
		}

		var kinahItem = player.InventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem == null || kinahItem.Count < cost)
		{
			await SendPacketAsync(SmSystemMessage.NotEnoughMoney());
			return;
		}

		var nextPay = activeHouse.NextPay ?? _houseMaintenanceTiming.GetNextRun();
		for (var counter = 0; counter < packet.WeekCount; counter++)
			nextPay = _houseMaintenanceTiming.GetNextRunAfter(nextPay);

		if (_houseMaintenanceTiming.GetPaidWeeks(nextPay) > 4)
			return;

		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - cost);
		if (!await _houseAuctionRepository.PayHouseRentAsync(player.ObjectId, activeHouse.ObjectId, nextPay, kinahUpdate))
			return;

		player.InventoryItems = player.InventoryItems
			.Select(item => item.ObjectId == kinahUpdate.ObjectId ? kinahUpdate : item)
			.ToArray();
		player.Houses = player.Houses
			.Select(house => house.ObjectId == activeHouse.ObjectId ? house with { NextPay = nextPay } : house)
			.ToArray();

		if (staticData?.ItemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(kinahUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
		await SendPacketAsync(new SmHousePayRent(packet.WeekCount));
	}

	private async Task HandleHouseSettingsAsync(Player player, CmHouseSettings packet)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_SETTINGS.runImpl.
		var activeHouse = player.Houses.FirstOrDefault(house => !house.IsInactive);
		if (activeHouse == null)
			return;

		var signNotice = packet.SignNotice.Length > PlayerHouse.SignNoticeMaxLength
			? packet.SignNotice[..PlayerHouse.SignNoticeMaxLength]
			: packet.SignNotice;
		var hasValidDoorState = PlayerHouse.IsKnownDoorState(packet.DoorState);
		var doorState = hasValidDoorState ? packet.DoorState : activeHouse.DoorState;
		var updatedHouse = activeHouse with
		{
			DoorState = doorState,
			ShowOwnerName = packet.ShowOwnerName,
			SignNotice = signNotice,
		};

		player.Houses = player.Houses
			.Select(house => house.ObjectId == activeHouse.ObjectId ? updatedHouse : house)
			.ToArray();

		// Java dirty-saves this through House.save during periodic player saves; persist immediately until that scheduler is ported.
		var saved = await _houseAuctionRepository.UpdateHouseSettingsAsync(
			player.ObjectId,
			activeHouse.ObjectId,
			PlayerHouse.CreateSettings(doorState, packet.ShowOwnerName),
			signNotice);
		if (!saved)
		{
			_logger.LogWarning(
				"House settings update for house {HouseObjectId} by player {PlayerObjectId} was not persisted",
				activeHouse.ObjectId,
				player.ObjectId);
		}

		await SendPacketAsync(new SmHouseAcquire(player.ObjectId, activeHouse.AddressId, acquire: true));
		var housingTemplates = _runtimeContext?.DataManager?.StaticData.HousingTemplates;
		var worldHouse = AddOrUpdateWorldHouse(player, updatedHouse, housingTemplates);
		var houseUpdate = worldHouse != null
			? new SmHouseUpdate(worldHouse, housingTemplates)
			: new SmHouseUpdate(player, updatedHouse, housingTemplates);
		if (_connectionRegistry != null)
		{
			if (worldHouse != null)
				await _connectionRegistry.BroadcastHouseUpdateAsync(worldHouse, housingTemplates);
			else
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, houseUpdate, includeSourcePlayer: true);
		}
		else
			await SendPacketAsync(houseUpdate);

		if (!hasValidDoorState)
			return;
		if (doorState == PlayerHouse.DoorOpen)
		{
			await SendPacketAsync(SmSystemMessage.HousingOrderOpenDoor());
		}
		else if (doorState == PlayerHouse.DoorClosedExceptFriends)
		{
			// Java parity: controllers/HouseController.kickVisitors owner notification before CM_HOUSE_SETTINGS door confirmation.
			await SendPacketAsync(SmSystemMessage.HousingOrderOutWithoutFriends());
			await SendPacketAsync(SmSystemMessage.HousingOrderCloseDoorWithoutFriends());
		}
		else if (doorState == PlayerHouse.DoorClosed)
		{
			// Java parity: controllers/HouseController.kickVisitors owner notification before CM_HOUSE_SETTINGS door confirmation.
			await SendPacketAsync(SmSystemMessage.HousingOrderOutAll());
			await SendPacketAsync(SmSystemMessage.HousingOrderCloseDoorAll());
		}
	}

	private async Task HandleHouseKickAsync(Player player, CmHouseKick packet)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_KICK.runImpl -> HouseController.kickVisitors.
		var activeHouse = player.Houses.FirstOrDefault(house => !house.IsInactive);
		if (activeHouse == null)
		{
			_logger.LogWarning("Player {PlayerObjectId} tried to kick visitors from a house without owning one", player.ObjectId);
			return;
		}

		if (packet.Option == 1)
		{
			// Java parity: owner notification after kickVisitors(..., kickFriends: false).
			await SendPacketAsync(SmSystemMessage.HousingOrderOutWithoutFriends());
		}
		else if (packet.Option == 2)
		{
			// Java parity: owner notification after kickVisitors(..., kickFriends: true).
			await SendPacketAsync(SmSystemMessage.HousingOrderOutAll());
		}
	}

	private async Task HandleHouseDecorateAsync(Player player, CmHouseDecorate packet)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_DECORATE applies registered decor or reverts a part line to default.
		var activeHouse = GetActiveHouse(player);
		var housingTemplates = _runtimeContext?.DataManager?.StaticData.HousingTemplates;
		if (activeHouse == null || housingTemplates == null)
			return;

		if (!HousingTemplateTable.TryGetDecorLine(packet.LineNumber, out var packetPartType, out var room))
			return;

		var registry = await LoadHouseRegistryAsync(player, activeHouse);
		var updatedDecorations = new List<RegisteredHouseDecorationSummary>();
		var deletedDecorationIds = new HashSet<int>();
		if (packet.ObjectId == 0)
		{
			CollectAppliedDecorDeletes(registry, housingTemplates, packetPartType, room, deletedDecorationIds);
		}
		else
		{
			var decoration = registry.GetDecoration(packet.ObjectId);
			if (decoration == null || decoration.IsDeleted)
				return;

			var part = housingTemplates.GetPart(decoration.TemplateId);
			if (part == null)
				return;

			if (decoration.Room != room)
			{
				CollectAppliedDecorDeletes(registry, housingTemplates, part.Type, room, deletedDecorationIds, decoration.ObjectId);
				var defaultDecorId = housingTemplates.GetDefaultDecorId(activeHouse.BuildingId, part.Type);
				if (defaultDecorId == decoration.TemplateId)
					deletedDecorationIds.Add(decoration.ObjectId);
				else
					updatedDecorations.Add(decoration with { Room = room });
			}
		}

		if (!await _housingRepository.SaveHouseDecorationMutationAsync(
			player.ObjectId,
			updatedDecorations,
			deletedDecorationIds.ToArray()))
		{
			return;
		}

		var updatedRegistry = registry.WithDecorationMutation(updatedDecorations, deletedDecorationIds.ToArray());
		UpdateHouseRegistry(player, activeHouse, updatedRegistry);
		var updatedHouse = player.Houses.First(house => house.ObjectId == activeHouse.ObjectId);
		if (packet.ObjectId != 0)
			await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DeleteItem, 2, packet.ObjectId));
		await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DeleteItem, 2, packet.ObjectId));
		await BroadcastHouseAppearanceAsync(player, updatedHouse);
	}

	private static void CollectAppliedDecorDeletes(
		HouseRegistrySummary registry,
		HousingTemplateTable housingTemplates,
		string partType,
		int room,
		ISet<int> deletedDecorationIds,
		int excludedObjectId = 0)
	{
		foreach (var decoration in registry.Decorations)
		{
			if (decoration.ObjectId == excludedObjectId || decoration.IsDeleted || decoration.Room != room)
				continue;

			var part = housingTemplates.GetPart(decoration.TemplateId);
			if (part != null && string.Equals(part.Type, partType, StringComparison.OrdinalIgnoreCase))
				deletedDecorationIds.Add(decoration.ObjectId);
		}
	}

	private async Task HandleHouseEditAsync(Player player, CmHouseEdit packet)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_EDIT mode entry/exit branches.
		var activeHouse = GetActiveHouse(player);
		if (activeHouse == null)
			return;

		switch (packet.Action)
		{
			case CmHouseEdit.EnterDecorationMode:
			{
				await SendPacketAsync(new SmHouseEdit(packet.Action));
				var housingTemplates = _runtimeContext?.DataManager?.StaticData.HousingTemplates;
				var registry = await LoadHouseRegistryAsync(player, activeHouse);
				await SendPacketAsync(SmHouseRegistry.CreateRegisteredObjects(registry, player.HouseObjectCooldowns));
				await SendPacketAsync(SmHouseRegistry.CreateDecorationItems(housingTemplates, activeHouse.BuildingId, registry));
				break;
			}
			case CmHouseEdit.ExitDecorationMode:
			case CmHouseEdit.EnterRenovationMode:
			case CmHouseEdit.ExitRenovationMode:
				await SendPacketAsync(new SmHouseEdit(packet.Action));
				break;
			case CmHouseEdit.AddItem:
				await HandleHouseItemRegistrationAsync(player, activeHouse, packet);
				break;
			case CmHouseEdit.SpawnObject:
				await HandleHouseObjectPlacementAsync(player, activeHouse, packet, moveExisting: false);
				break;
			case CmHouseEdit.MoveObject:
				await HandleHouseObjectPlacementAsync(player, activeHouse, packet, moveExisting: true);
				break;
			case CmHouseEdit.DeleteItem:
				await HandleHouseObjectDeleteAsync(player, activeHouse, packet);
				break;
			case CmHouseEdit.DespawnObject:
				await HandleHouseObjectDespawnAsync(player, activeHouse, packet);
				break;
			case CmHouseEdit.RenovateBuilding:
				await HandleHouseRenovationAsync(player, activeHouse, packet);
				break;
		}
	}

	private async Task HandleHouseRenovationAsync(Player player, PlayerHouse activeHouse, CmHouseEdit packet)
	{
		// Java parity: CM_HOUSE_EDIT action 16 removeRenovationCoupon then HousingService.switchHouseBuilding.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null || staticData.HousingTemplates.GetBuilding(packet.BuildingId) == null)
			return;

		var currentHouseTypeId = staticData.HousingTemplates.GetHouseTypeId(activeHouse.BuildingId);
		if (currentHouseTypeId == 0)
			return;

		var couponItemId = GetRenovationCouponItemId(player.Race, currentHouseTypeId);
		var couponConsumption = BuildItemCountConsumption(player.InventoryItems, couponItemId, 1);
		if (couponConsumption == null)
			return;

		var deletedCouponObjectIds = couponConsumption.Deletes.Select(item => item.ObjectId).ToArray();
		if (!await _housingRepository.SaveHouseRenovationAsync(
			player.ObjectId,
			activeHouse.ObjectId,
			packet.BuildingId,
			couponConsumption.Updates,
			deletedCouponObjectIds))
		{
			return;
		}

		await SendConsumedItemPacketsAsync(
			player.InventoryItems,
			couponConsumption.Updates,
			deletedCouponObjectIds,
			staticData.ItemTemplates);
		var inventoryItems = player.InventoryItems.ToList();
		foreach (var couponUpdate in couponConsumption.Updates)
			ReplaceInventoryItem(inventoryItems, couponUpdate);
		inventoryItems.RemoveAll(item => deletedCouponObjectIds.Contains(item.ObjectId));
		player.InventoryItems = inventoryItems.ToArray();

		var registry = await _housingRepository.LoadHouseRegistryAsync(
			player.ObjectId,
			packet.BuildingId,
			staticData.HousingTemplates,
			staticData.HousingObjectTemplates);
		player.Houses = player.Houses
			.Select(house => house.ObjectId == activeHouse.ObjectId
				? house with { BuildingId = packet.BuildingId, Registry = registry }
				: house)
			.ToArray();
		await BroadcastHouseAppearanceAsync(player, player.Houses.First(house => house.ObjectId == activeHouse.ObjectId));
	}

	private static int GetRenovationCouponItemId(string race, int houseTypeId)
	{
		// Java parity: CM_HOUSE_EDIT.removeRenovationCoupon race base item minus HouseType id.
		var baseItemId = string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase) ? 169661004 : 169661008;
		return baseItemId - houseTypeId;
	}

	private async Task HandleHouseItemRegistrationAsync(Player player, PlayerHouse activeHouse, CmHouseEdit packet)
	{
		// Java parity: CM_HOUSE_EDIT action 3 transfers a cube item into HouseRegistry via item DecorateAction/SummonHouseObjectAction.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null || _idFactory == null)
			return;

		var sourceItem = player.InventoryItems.FirstOrDefault(item =>
			item.ObjectId == packet.ItemObjectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
		var itemTemplate = sourceItem == null ? null : staticData.ItemTemplates.GetItemTemplate(sourceItem.ItemId);
		if (sourceItem == null || itemTemplate == null)
			return;

		var registry = await LoadHouseRegistryAsync(player, activeHouse);
		if (itemTemplate.HasHouseDecorateAction)
		{
			var decorationObjectId = _idFactory.NextId();
			var decoration = new RegisteredHouseDecorationSummary(decorationObjectId, itemTemplate.HouseDecorateTemplateId);
			if (!await _housingRepository.RegisterHouseDecorationFromInventoryAsync(player.ObjectId, sourceItem.ObjectId, decoration))
			{
				_idFactory.ReleaseId(decorationObjectId);
				return;
			}

			player.InventoryItems = player.InventoryItems
				.Where(item => item.ObjectId != sourceItem.ObjectId)
				.ToArray();
			UpdateHouseRegistry(player, activeHouse, registry.WithDecoration(decoration));
			await SendPacketAsync(new SmHouseEdit(CmHouseEdit.AddItem, 2, decoration));
			return;
		}

		if (!itemTemplate.HasHouseObjectAction)
			return;

		var objectTemplate = staticData.HousingObjectTemplates.GetTemplate(itemTemplate.HouseObjectTemplateId);
		if (objectTemplate == null)
			return;

		var objectId = _idFactory.NextId();
		var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var expireTimeSeconds = objectTemplate.UseDays > 0
			? (int?)(int)(nowSeconds + objectTemplate.UseDays * 86_400L)
			: null;
		var houseObject = HouseRegistrySummary.CreateObjectFromTemplate(
			objectId,
			objectTemplate,
			expireTimeSeconds,
			() => nowSeconds);
		if (!await _housingRepository.RegisterHouseObjectFromInventoryAsync(player.ObjectId, sourceItem.ObjectId, houseObject, expireTimeSeconds))
		{
			_idFactory.ReleaseId(objectId);
			return;
		}

		player.InventoryItems = player.InventoryItems
			.Where(item => item.ObjectId != sourceItem.ObjectId)
			.ToArray();
		UpdateHouseRegistry(player, activeHouse, registry.WithObject(houseObject));
		await SendPacketAsync(new SmHouseEdit(CmHouseEdit.AddItem, 1, houseObject.WithCooldown(player.HouseObjectCooldowns), player.ObjectId));
	}

	private async Task HandleHouseObjectPlacementAsync(Player player, PlayerHouse activeHouse, CmHouseEdit packet, bool moveExisting)
	{
		// Java parity: CM_HOUSE_EDIT action 5/6 mutates HouseObject position and sends SM_HOUSE_EDIT spawn/move packets.
		var registry = await LoadHouseRegistryAsync(player, activeHouse);
		var houseObject = registry.GetObject(packet.ItemObjectId);
		if (houseObject == null)
			return;

		var updatedObject = houseObject with
		{
			X = packet.X,
			Y = packet.Y,
			Z = packet.Z,
			Heading = ConvertAngleToHeading(packet.Rotation),
		};
		if (!await _housingRepository.SaveHouseObjectPlacementAsync(player.ObjectId, updatedObject))
			return;

		registry = registry.WithObject(updatedObject);
		UpdateHouseRegistry(player, activeHouse, registry);
		var placedObject = registry.GetSpawnedObjects(GetActiveHouse(player) ?? activeHouse, player.ObjectId)
			.FirstOrDefault(obj => obj.ObjectId == updatedObject.ObjectId);
		if (placedObject == null)
			return;

		if (moveExisting)
			await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DespawnObject, 0, updatedObject.ObjectId));
		await SendPacketAsync(new SmHouseEdit(CmHouseEdit.SpawnObject, placedObject));
		if (!moveExisting)
			await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DeleteItem, 1, updatedObject.ObjectId));
	}

	private async Task HandleHouseObjectDeleteAsync(Player player, PlayerHouse activeHouse, CmHouseEdit packet)
	{
		// Java parity: CM_HOUSE_EDIT action 4 discards an existing HouseObject from the registry.
		var registry = await LoadHouseRegistryAsync(player, activeHouse);
		var houseObject = registry.GetObject(packet.ItemObjectId);
		if (houseObject == null)
			return;

		if (!await _housingRepository.DeleteHouseRegisteredObjectAsync(player.ObjectId, packet.ItemObjectId))
			return;

		UpdateHouseRegistry(player, activeHouse, registry.WithoutObject(packet.ItemObjectId));
		RemoveHouseObjectCooldownFromOnlinePlayers(player, packet.ItemObjectId);
		_idFactory?.ReleaseId(packet.ItemObjectId);
		await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DeleteItem, 1, packet.ItemObjectId));
		await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DeleteItem, 1, packet.ItemObjectId));
	}

	private async Task HandleHouseObjectDespawnAsync(Player player, PlayerHouse activeHouse, CmHouseEdit packet)
	{
		// Java parity: CM_HOUSE_EDIT action 7 removes a spawned HouseObject from the house and places it back into edit inventory.
		var registry = await LoadHouseRegistryAsync(player, activeHouse);
		var houseObject = registry.GetObject(packet.ItemObjectId);
		if (houseObject == null)
			return;

		var updatedObject = houseObject with { X = 0, Y = 0, Z = 0, Heading = 0 };
		if (!await _housingRepository.SaveHouseObjectPlacementAsync(player.ObjectId, updatedObject))
			return;

		registry = registry.WithObject(updatedObject);
		UpdateHouseRegistry(player, activeHouse, registry);
		await SendPacketAsync(new SmHouseEdit(CmHouseEdit.DespawnObject, 0, updatedObject.ObjectId));
		await SendPacketAsync(new SmHouseEdit(CmHouseEdit.AddItem, 1, updatedObject.WithCooldown(player.HouseObjectCooldowns), player.ObjectId));
	}

	private async Task<HouseRegistrySummary> LoadHouseRegistryAsync(Player player, PlayerHouse activeHouse)
	{
		// Java parity: model/house/House.getRegistry lazy-loads PlayerRegisteredItemsDAO data for the active house owner.
		if (activeHouse.Registry != null)
			return activeHouse.Registry;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		if (staticData == null)
			return HouseRegistrySummary.Empty;

		var registry = await _housingRepository.LoadHouseRegistryAsync(
			player.ObjectId,
			activeHouse.BuildingId,
			staticData.HousingTemplates,
			staticData.HousingObjectTemplates);
		UpdateHouseRegistry(player, activeHouse, registry);
		return registry;
	}

	private void UpdateHouseRegistry(Player player, PlayerHouse activeHouse, HouseRegistrySummary registry)
	{
		var staticData = _runtimeContext?.DataManager?.StaticData;
		player.Houses = player.Houses
			.Select(house => house.ObjectId == activeHouse.ObjectId ? house with { Registry = registry } : house)
			.ToArray();
		_expirableTaskService?.RegisterHouseObjects(player, player.Houses.First(house => house.ObjectId == activeHouse.ObjectId));
		if (staticData != null)
			AddOrUpdateWorldHouse(player, player.Houses.First(house => house.ObjectId == activeHouse.ObjectId), staticData.HousingTemplates);
	}

	private async Task BroadcastHouseAppearanceAsync(Player player, PlayerHouse house)
	{
		var housingTemplates = _runtimeContext?.DataManager?.StaticData.HousingTemplates;
		var worldHouse = AddOrUpdateWorldHouse(player, house, housingTemplates);
		var houseUpdate = worldHouse != null
			? new SmHouseUpdate(worldHouse, housingTemplates)
			: new SmHouseUpdate(player, house, housingTemplates);
		if (_connectionRegistry != null)
		{
			if (worldHouse != null)
				await _connectionRegistry.BroadcastHouseUpdateAsync(worldHouse, housingTemplates);
			else
				await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, houseUpdate, includeSourcePlayer: true);
		}
		else
			await SendPacketAsync(houseUpdate);
	}

	private static int ConvertAngleToHeading(int angle)
	{
		// Java parity: utils/PositionUtil.convertAngleToHeading truncates angle / 3 into a byte.
		return (byte)(angle / 3);
	}

	private async Task<bool> CanOwnHouseForAuctionAsync(Player player)
	{
		// Java parity: services/HousingService.canOwnHouse(player, true) quest gate.
		var questId = string.Equals(player.Race, "ELYOS", StringComparison.OrdinalIgnoreCase) ? 18802 : 28802;
		if (player.Quests.Any(quest => quest.QuestId == questId && quest.IsComplete))
			return true;

		await SendPacketAsync(SmSystemMessage.HousingCantOwnNotCompleteQuest(questId));
		return false;
	}

	private static PlayerHouse? GetActiveAuctionHouse(Player player, HousingTemplateTable? housingTemplates)
	{
		// Java parity: CM_REGISTER_HOUSE uses Player.getActiveHouse and rejects HouseType.STUDIO.
		foreach (var house in player.Houses)
		{
			if (house.IsInactive)
				continue;
			if (housingTemplates?.GetHouseTypeId(house.BuildingId) == 0)
				return null;
			return house;
		}

		return null;
	}

	private static bool IsHouseAuctionRegistrationAllowed(GameServerHousingOptions housingOptions)
	{
		// Java parity: HousingBidService.isRegisteringAllowed.
		if (!housingOptions.AuctionsEnabled)
			return false;
		if (housingOptions.AuctionRegisterDays.Count < 2)
			return true;

		var today = DateTime.Now.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)DateTime.Now.DayOfWeek;
		var from = housingOptions.AuctionRegisterDays[0];
		var to = housingOptions.AuctionRegisterDays[1];
		return from > to
			? from <= today || to >= today
			: from <= today && to >= today;
	}

	private int GetPlayerLevel(Player player)
	{
		// Java parity: Player.getLevel delegates to the loaded experience table.
		return Math.Max(1, GetPlayerExperienceTable()?.GetLevelForExp(player.Exp) ?? 1);
	}

	private static int GetMinBidLevel(HouseAuctionBidContext context, GameServerHousingOptions housingOptions)
	{
		// Java parity: HousingBidService.getMinBidLevel falls back to LandSaleOptions.minLevel.
		return context.HouseTypeId switch
		{
			1 when housingOptions.HouseMinBidLevel > 0 => housingOptions.HouseMinBidLevel,
			2 when housingOptions.MansionMinBidLevel > 0 => housingOptions.MansionMinBidLevel,
			3 when housingOptions.EstateMinBidLevel > 0 => housingOptions.EstateMinBidLevel,
			4 when housingOptions.PalaceMinBidLevel > 0 => housingOptions.PalaceMinBidLevel,
			_ => context.LandMinLevel,
		};
	}

	private static bool IsHouseFeePaid(PlayerHouse house)
	{
		// Java parity: model/house/House.isFeePaid.
		return house.NextPay == null || house.NextPay.Value >= DateTime.Now;
	}

	private async Task HandleBrokerCancelRegisteredAsync(Player player, CmBrokerCancelRegistered packet)
	{
		// Java parity: network/aion/clientpackets/CM_BROKER_CANCEL_REGISTERED.runImpl -> BrokerService.cancelRegisteredItem.
		if (_brokerRepository == null)
			return;
		if (!CanTrade(player))
			return;

		var brokerItem = await _brokerRepository.LoadRegisteredItemAsync(player.ObjectId, player.Race, packet.BrokerItemObjectId);
		if (brokerItem?.Item != null)
		{
			var staticData = _runtimeContext?.DataManager?.StaticData;
			var itemTemplates = staticData?.ItemTemplates;
			var itemTemplate = itemTemplates?.GetItemTemplate(brokerItem.Item.ItemId);
			if (itemTemplate != null)
			{
				if (!InventoryCapacity.HasFreeCubeSlot(player))
				{
					await SendPacketAsync(SmSystemMessage.ExchangeFullInventory());
					return;
				}

				var returnedItem = CopyInventoryItem(
					brokerItem.Item,
					location: CubeStorageId,
					slot: FirstAvailableSlot,
					ownerId: player.ObjectId,
					isEquipped: false);
				if (await _brokerRepository.CancelRegisteredItemAsync(brokerItem, returnedItem))
				{
					player.InventoryItems = player.InventoryItems
						.Where(item => item.ObjectId != returnedItem.ObjectId)
						.Concat([returnedItem])
						.ToArray();
					await SendPacketAsync(SmInventoryAddItem.CreateBrokerReturn(
						returnedItem,
						itemTemplate,
						GetGeneralInfoWarehouseRestrictionFlag(returnedItem.ItemId, staticData?.ItemRestrictionCleanups)));
					await SendPacketAsync(SmCubeUpdate.CubeSize(player));
					await SendPacketAsync(SmBrokerService.CreateCancelRegisteredItem(packet.BrokerItemObjectId));
				}
			}
		}

		var registeredItems = await _brokerRepository.LoadRegisteredItemsAsync(player.ObjectId, player.Race);
		await SendPacketAsync(SmBrokerService.CreateRegisteredItems(registeredItems));
	}

	private async Task HandleBrokerSettleAccountAsync(Player player)
	{
		// Java parity: network/aion/clientpackets/CM_BROKER_SETTLE_ACCOUNT.runImpl -> BrokerService.settleAccount.
		if (_brokerRepository == null)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (itemTemplates == null)
			return;

		var settledItems = await _brokerRepository.LoadSettledItemsForAccountAsync(player.ObjectId, player.Race);
		var returnedItems = new List<(PlayerBrokerReturnedItem Return, ItemTemplateSummary Template)>();
		var collectedItems = new List<PlayerBrokerItem>();
		var collectedKinah = 0L;
		var freeCubeSlots = InventoryCapacity.GetFreeCubeSlots(player);
		foreach (var brokerItem in settledItems)
		{
			if (brokerItem.IsSold)
			{
				collectedItems.Add(brokerItem);
				collectedKinah += brokerItem.Price * brokerItem.ItemCount;
				continue;
			}

			if (brokerItem.Item == null || itemTemplates.GetItemTemplate(brokerItem.Item.ItemId) is not { } itemTemplate)
				continue;
			if (freeCubeSlots <= 0)
				continue;

			var returnedItem = CopyInventoryItem(
				brokerItem.Item,
				location: CubeStorageId,
				slot: FirstAvailableSlot,
				ownerId: player.ObjectId,
				isEquipped: false);
			var returnedBrokerItem = new PlayerBrokerReturnedItem(brokerItem, returnedItem);
			returnedItems.Add((returnedBrokerItem, itemTemplate));
			collectedItems.Add(brokerItem);
			freeCubeSlots--;
		}

		var kinahItem = BuildBrokerSettlementKinahItem(player, collectedKinah);
		if (collectedKinah > 0 && kinahItem == null)
			return;

		var settlement = new PlayerBrokerAccountSettlement(
			collectedItems,
			returnedItems.Select(item => item.Return).ToArray(),
			kinahItem);
		if (!await _brokerRepository.SettleAccountAsync(settlement))
			return;

		var inventoryItems = player.InventoryItems.ToList();
		foreach (var (returnedItem, itemTemplate) in returnedItems)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == returnedItem.ReturnedItem.ObjectId);
			inventoryItems.Add(returnedItem.ReturnedItem);
			player.InventoryItems = inventoryItems.ToArray();
			await SendPacketAsync(SmInventoryAddItem.CreateItemCollect(
				returnedItem.ReturnedItem,
				itemTemplate,
				GetGeneralInfoWarehouseRestrictionFlag(returnedItem.ReturnedItem.ItemId, staticData?.ItemRestrictionCleanups)));
			await SendPacketAsync(SmCubeUpdate.CubeSize(player));
		}

		if (kinahItem != null)
		{
			ReplaceInventoryItem(inventoryItems, kinahItem);
			if (!inventoryItems.Any(item => item.ObjectId == kinahItem.ObjectId))
				inventoryItems.Add(kinahItem);
			player.InventoryItems = inventoryItems.ToArray();
			if (itemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
				await SendPacketAsync(new SmInventoryUpdateItem(kinahItem, kinahTemplate, SmInventoryUpdateItem.IncreaseKinahCollect));
		}

		var page = await _brokerRepository.LoadSettledItemsAsync(player.ObjectId, player.Race, pageIndex: 0);
		player.BrokerSettlements = new PlayerBrokerSettlementSummary(page.TotalItemCount, page.SettledKinah);
		await SendPacketAsync(SmBrokerService.CreateSettledItems(page));
		if (page.TotalItemCount == 0)
			await SendPacketAsync(SmBrokerService.CreateRemoveSettledIcon());
	}

	private async Task HandleBrokerRegisterItemAsync(Player player, CmRegisterBrokerItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_REGISTER_BROKER_ITEM.runImpl -> BrokerService.registerItem.
		if (_brokerRepository == null || packet.ItemCount <= 0 || packet.Price <= 0)
			return;
		if (!CanTrade(player))
			return;

		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		var sourceItem = player.InventoryItems.FirstOrDefault(item => item.ObjectId == packet.ItemObjectId && item.Location == CubeStorageId);
		var itemTemplate = sourceItem == null ? null : itemTemplates?.GetItemTemplate(sourceItem.ItemId);
		if (sourceItem == null || itemTemplate == null || packet.ItemCount > sourceItem.Count)
			return;

		if (packet.ItemCount > 1 && packet.Price / packet.ItemCount > 999_999_999L || packet.Price > 99_999_999_999L)
		{
			await SendPacketAsync(SmSystemMessage.BrokerPriceExceedsLimit());
			return;
		}

		if (sourceItem.PackCount <= 0 && (!itemTemplate.IsTradeable || sourceItem.IsSoulBound))
			return;
		if (!await CanOperateItemAsync(player, sourceItem, "broker"))
			return;

		var registeredItems = await _brokerRepository.LoadRegisteredItemsAsync(player.ObjectId, player.Race);
		var registeredItemsCount = registeredItems.Count;
		if (registeredItemsCount > 14)
		{
			await SendPacketAsync(SmBrokerService.CreateRegisterMessage(3));
			return;
		}

		var registrationCommission = BrokerRegistrationCommissionPlanService.CreatePlan(
			packet.Price,
			packet.ItemCount,
			registeredItemsCount,
			player.Race).Commission;
		var kinahItem = player.InventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem == null || kinahItem.Count < registrationCommission)
		{
			await SendPacketAsync(SmBrokerService.CreateRegisterMessage(5));
			return;
		}

		var splittingAvailable = itemTemplate.MaxStackCount > 1 && packet.SplittingAvailable;
		var reducedSourceItem = default(InventoryItem);
		InventoryItem brokerStorageItem;
		if (itemTemplate.MaxStackCount > 1 && packet.ItemCount < sourceItem.Count)
		{
			var objectId = _idFactory?.NextId() ?? 0;
			if (objectId == 0)
				return;

			reducedSourceItem = CopyInventoryItem(sourceItem, count: sourceItem.Count - packet.ItemCount);
			brokerStorageItem = CreateNewItem(
				objectId,
				itemTemplate,
				packet.ItemCount,
				ownerId: player.ObjectId,
				location: BrokerStorageId,
				slot: FirstAvailableSlot);
		}
		else
		{
			brokerStorageItem = CopyInventoryItem(
				sourceItem,
				location: BrokerStorageId,
				count: packet.ItemCount,
				isEquipped: false);
		}

		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - registrationCommission);
		var expireTime = TruncateToSecond(DateTime.Now.AddDays(_options.Custom.BrokerRegistrationExpirationDays));
		var brokerItem = new PlayerBrokerItem(
			brokerStorageItem.ObjectId,
			brokerStorageItem.ItemId,
			brokerStorageItem.Count,
			brokerStorageItem.Creator ?? string.Empty,
			packet.Price,
			player.ObjectId,
			player.Name,
			GetBrokerRace(player.Race),
			IsSold: false,
			IsSettled: false,
			expireTime,
			TruncateToSecond(DateTime.Now),
			splittingAvailable,
			brokerStorageItem);
		if (!await _brokerRepository.RegisterItemAsync(brokerItem, brokerStorageItem, reducedSourceItem, kinahUpdate))
			return;

		var inventoryItems = player.InventoryItems.ToList();
		if (reducedSourceItem == null)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == sourceItem.ObjectId);
			await SendPacketAsync(new SmDeleteItem(sourceItem.ObjectId));
		}
		else
		{
			ReplaceInventoryItem(inventoryItems, reducedSourceItem);
			await SendPacketAsync(new SmInventoryUpdateItem(reducedSourceItem, itemTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}

		ReplaceInventoryItem(inventoryItems, kinahUpdate);
		player.InventoryItems = inventoryItems.ToArray();
		if (itemTemplates?.GetItemTemplate(KinahItemId) is { } kinahTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(kinahUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
		await SendPacketAsync(SmBrokerService.CreateRegisterItem(brokerItem, registeredItemsCount));
	}

	private async Task HandleBuyBrokerItemAsync(Player player, CmBuyBrokerItem packet)
	{
		// Java parity: network/aion/clientpackets/CM_BUY_BROKER_ITEM.runImpl -> BrokerService.buyBrokerItem.
		if (_brokerRepository == null || packet.ItemCount < 1)
			return;
		if (!CanTrade(player))
			return;

		var brokerItem = await _brokerRepository.LoadActiveItemAsync(player.Race, packet.BrokerItemObjectId);
		if (brokerItem?.Item == null)
			return;
		if (brokerItem.SellerId == player.ObjectId)
		{
			await SendPacketAsync(SmSystemMessage.VendorCannotBuyOwnRegisteredItem());
			return;
		}
		if (packet.ItemCount > brokerItem.ItemCount)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		var itemTemplate = itemTemplates?.GetItemTemplate(brokerItem.Item.ItemId);
		var kinahItem = player.InventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (itemTemplate == null || kinahItem == null)
			return;

		var totalPrice = brokerItem.Price * packet.ItemCount;
		if (!InventoryCapacity.HasFreeCubeSlot(player))
		{
			await SendPacketAsync(SmSystemMessage.FullInventory());
			return;
		}

		if (kinahItem.Count < totalPrice)
			return;

		var settleTime = TruncateToSecond(DateTime.Now);
		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - totalPrice);
		var partialPurchase = brokerItem.ItemCount > packet.ItemCount && brokerItem.SplittingAvailable;
		PlayerBrokerItem? remainingBrokerItem = null;
		InventoryItem? remainingBrokerStorageItem = null;
		InventoryItem boughtItem;
		PlayerBrokerItem soldBrokerItem;
		if (partialPurchase)
		{
			var objectId = _idFactory?.NextId() ?? 0;
			if (objectId == 0)
				return;

			remainingBrokerStorageItem = CopyInventoryItem(brokerItem.Item, count: brokerItem.ItemCount - packet.ItemCount);
			remainingBrokerItem = brokerItem with
			{
				ItemCount = brokerItem.ItemCount - packet.ItemCount,
				Item = remainingBrokerStorageItem,
			};
			boughtItem = CreateNewItem(
				objectId,
				itemTemplate,
				packet.ItemCount,
				ownerId: player.ObjectId,
				location: CubeStorageId,
				slot: FirstAvailableSlot);
			soldBrokerItem = new PlayerBrokerItem(
				boughtItem.ObjectId,
				boughtItem.ItemId,
				boughtItem.Count,
				string.Empty,
				brokerItem.Price,
				brokerItem.SellerId,
				brokerItem.SellerName,
				brokerItem.BrokerRace,
				IsSold: true,
				IsSettled: true,
				TruncateToSecond(DateTime.Now.AddDays(_options.Custom.BrokerRegistrationExpirationDays)),
				settleTime,
				brokerItem.SplittingAvailable,
				boughtItem);
		}
		else
		{
			var packCount = brokerItem.Item.PackCount > 0 ? brokerItem.Item.PackCount * -1 : brokerItem.Item.PackCount;
			boughtItem = CopyInventoryItem(
				brokerItem.Item,
				location: CubeStorageId,
				slot: FirstAvailableSlot,
				ownerId: player.ObjectId,
				isEquipped: false,
				packCount: packCount);
			soldBrokerItem = brokerItem with
			{
				IsSold = true,
				IsSettled = true,
				SettleTime = settleTime,
				Item = boughtItem,
			};
		}

		var purchase = new PlayerBrokerPurchase(soldBrokerItem, remainingBrokerItem, boughtItem, remainingBrokerStorageItem, kinahUpdate);
		if (!await _brokerRepository.BuyItemAsync(purchase))
			return;

		var inventoryItems = player.InventoryItems.ToList();
		ReplaceInventoryItem(inventoryItems, kinahUpdate);
		inventoryItems.RemoveAll(item => item.ObjectId == boughtItem.ObjectId);
		inventoryItems.Add(boughtItem);
		player.InventoryItems = inventoryItems.ToArray();
		if (itemTemplates?.GetItemTemplate(KinahItemId) is { } kinahTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(kinahUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
		await SendPacketAsync(SmInventoryAddItem.CreateBrokerBuy(
			boughtItem,
			itemTemplate,
			GetGeneralInfoWarehouseRestrictionFlag(boughtItem.ItemId, staticData?.ItemRestrictionCleanups)));
		await SendPacketAsync(SmCubeUpdate.CubeSize(player));

		var sellerSettledPage = await _brokerRepository.LoadSettledItemsAsync(brokerItem.SellerId, player.Race, pageIndex: 0);
		if (_connectionRegistry != null)
			await _connectionRegistry.NotifyBrokerSettledAsync(brokerItem.SellerId, sellerSettledPage.SettledKinah);

		await SendPacketAsync(SmBrokerService.CreateSearchedItems(await LoadCachedBrokerPageAsync(player)));
	}

	private static DateTime TruncateToSecond(DateTime value)
	{
		// Java parity: BrokerItem expiration timestamps set nanos to zero for DB key comparisons.
		return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);
	}

	private static string GetBrokerRace(string race)
	{
		return race == "ASMODIANS" ? "ASMODIAN" : race;
	}

	private InventoryItem? BuildBrokerSettlementKinahItem(Player player, long collectedKinah)
	{
		// Java parity: model/items/storage/Storage.increaseKinah default ItemUpdateType.INC_KINAH_COLLECT.
		if (collectedKinah <= 0)
			return null;

		var kinahItem = player.InventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem != null)
			return CopyInventoryItem(kinahItem, count: kinahItem.Count + collectedKinah);

		var objectId = _idFactory?.NextId() ?? 0;
		if (objectId == 0)
			return null;

		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = KinahItemId,
			Count = collectedKinah,
			OwnerId = player.ObjectId,
			Location = CubeStorageId,
			Slot = FirstAvailableSlot,
		};
	}

	private static IEnumerable<PlayerBrokerItem> SortBrokerItems(
		IReadOnlyList<PlayerBrokerItem> items,
		byte sortType,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: model/gameobjects/BrokerItem.getComparatoryByType. Sort type 1 intentionally follows Java's same name comparator.
		return sortType switch
		{
			0 or 1 => items.OrderBy(item => itemTemplates.GetItemTemplate(item.ItemId)?.Name ?? string.Empty),
			2 => items.OrderBy(item => itemTemplates.GetItemTemplate(item.ItemId)?.Level ?? 0),
			3 => items.OrderByDescending(item => itemTemplates.GetItemTemplate(item.ItemId)?.Level ?? 0),
			4 => items.OrderBy(item => item.Price),
			5 => items.OrderByDescending(item => item.Price),
			6 => items.OrderBy(GetBrokerPiecePrice),
			7 => items.OrderByDescending(GetBrokerPiecePrice),
			_ => items.OrderBy(item => item.ItemId).ThenBy(item => item.ItemObjectId),
		};
	}

	private static long GetBrokerPiecePrice(PlayerBrokerItem item)
	{
		return item.ItemCount <= 0 ? item.Price : item.Price / item.ItemCount;
	}

	private async Task HandleGetMailAttachmentAsync(Player player, CmGetMailAttachment packet)
	{
		// Java parity: services/mail/MailService.getAttachments.
		var letter = player.Mailbox.FirstOrDefault(mail => mail.Id == packet.MailObjectId);
		if (letter == null)
			return;

		switch (packet.AttachmentType)
		{
			case 0:
				if (letter.AttachedItem == null)
					return;
				if (!InventoryCapacity.HasFreeCubeSlot(player))
				{
					await SendPacketAsync(SmSystemMessage.MailTakeAllCancel());
					return;
				}

				player.InventoryItems = player.InventoryItems
					.Concat([CopyInventoryItem(letter.AttachedItem, CubeStorageId, FirstAvailableSlot)])
					.ToArray();
				player.Mailbox = player.Mailbox
					.Select(mail => mail.Id == packet.MailObjectId
						? mail with { AttachedItem = null, AttachedItemObjectId = 0, AttachedItemTemplateId = 0 }
						: mail)
					.ToArray();
				if (_mailRepository != null)
					await _mailRepository.ClearAttachedItemAsync(packet.MailObjectId, letter.AttachedItem.ObjectId, player.ObjectId);
				await SendPacketAsync(SmMailService.CreateAttachmentState(packet.MailObjectId, packet.AttachmentType));
				break;
			case 1:
				player.InventoryItems = IncreaseInventoryKinah(
					player.InventoryItems,
					player.ObjectId,
					letter.AttachedKinah,
					_idFactory?.NextId() ?? 0);
				player.Mailbox = player.Mailbox
					.Select(mail => mail.Id == packet.MailObjectId ? mail with { AttachedKinah = 0 } : mail)
					.ToArray();
				if (_mailRepository != null)
					await _mailRepository.ClearAttachedKinahAsync(packet.MailObjectId);
				await SendPacketAsync(SmMailService.CreateAttachmentState(packet.MailObjectId, packet.AttachmentType));
				break;
		}
	}

	private async Task<GameServerPacket?> HandleSendMailAsync(Player sender, CmSendMail sendMail)
	{
		// Java parity: services/mail/MailService.sendMail for non-item mail.
		if (_mailRepository == null)
			return null;
		if (sender.IsTrading)
			return null;
		if (sendMail.RecipientName.Length > 16 || sendMail.LetterTypeId == 2 || sendMail.KinahCount < 0)
			return null;
		if (sendMail.LetterTypeId is not (0 or 1))
			return null;

		var title = sendMail.Title.Length > 20 ? sendMail.Title[..20] : sendMail.Title;
		var message = sendMail.Message.Length > 1000 ? sendMail.Message[..1000] : sendMail.Message;
		var recipient = await _mailRepository.LoadRecipientAsync(sendMail.RecipientName);
		if (recipient == null)
			return SmMailService.CreateMailMessage(SmMailService.NoSuchCharacterName);
		if (!string.Equals(recipient.Race, sender.Race, StringComparison.Ordinal))
			return SmMailService.CreateMailMessage(SmMailService.MailIsOneRaceOnly);
		if (recipient.MailboxLetters >= 100)
			return SmMailService.CreateMailMessage(SmMailService.RecipientMailboxFull);
		if (await _mailRepository.IsBlockedByRecipientAsync(recipient.PlayerObjectId, sender.ObjectId))
			return SmMailService.CreateMailMessage(SmMailService.YouAreInRecipientIgnoreList);

		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		var senderItem = sendMail is { ItemObjectId: not 0, ItemCount: > 0 }
			? sender.InventoryItems.FirstOrDefault(item => item.ObjectId == sendMail.ItemObjectId && item.Location == CubeStorageId)
			: null;
		var senderItemTemplate = senderItem == null ? null : itemTemplates?.GetItemTemplate(senderItem.ItemId);
		if (sendMail is { ItemObjectId: not 0, ItemCount: > 0 })
		{
			if (senderItem == null || senderItem.Count < sendMail.ItemCount)
				return SmSystemMessage.MailSendUsedItem();
			if (senderItem.IsEquipped)
				return SmSystemMessage.MailSendCannotSendEquippedItem();
			if (senderItemTemplate == null)
				return null;
			if (!await CanOperateItemAsync(sender, senderItem, "mail"))
				return null;
		}

		var costPlan = MailSendCostPlanService.CreatePlan(
			sendMail.LetterTypeId,
			sendMail.KinahCount,
			senderItemTemplate,
			sendMail.ItemCount,
			sender.Race);
		var finalMailKinah = costPlan.FinalMailKinah;
		var kinahItem = sender.InventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem == null || kinahItem.Count < finalMailKinah)
			return SmSystemMessage.NotEnoughMoney();

		var dispositionConsumption = InventoryItemConsumption.Empty;
		if (senderItem != null
			&& senderItemTemplate != null
			&& senderItem.PackCount <= 0
			&& (!senderItemTemplate.IsTradeable || senderItem.IsSoulBound))
		{
			if (senderItemTemplate.DispositionItemId == 0 || senderItemTemplate.DispositionItemCount == 0)
				return null;

			var consumption = BuildItemCountConsumption(
				sender.InventoryItems,
				senderItemTemplate.DispositionItemId,
				senderItemTemplate.DispositionItemCount);
			if (consumption == null)
				return null;
			dispositionConsumption = consumption;
		}

		var mailId = _idFactory?.NextId() ?? 0;
		if (mailId == 0)
			return null;

		var senderKinahCount = kinahItem.Count - finalMailKinah;
		var attachedItem = BuildMailAttachment(senderItem, senderItemTemplate, recipient.PlayerObjectId, sendMail.ItemCount);
		if (senderItem != null && attachedItem == null)
			return null;
		var mail = new PlayerMail(
			mailId,
			recipient.PlayerObjectId,
			sender.Name,
			title,
			message,
			IsUnread: true,
			AttachedItemObjectId: attachedItem?.ObjectId ?? 0,
			AttachedItemTemplateId: attachedItem?.ItemId ?? 0,
			sendMail.KinahCount,
			sendMail.LetterTypeId,
			DateTime.Now,
			attachedItem);
		var stored = attachedItem == null
			? await _mailRepository.StoreSentMailAsync(mail, kinahItem.ObjectId, senderKinahCount)
			: await _mailRepository.StoreSentItemMailAsync(
				mail,
				kinahItem.ObjectId,
				senderKinahCount,
				attachedItem,
				attachedItem.ObjectId == senderItem?.ObjectId ? null : senderItem?.ObjectId,
				senderItem == null ? 0 : senderItem.Count - sendMail.ItemCount,
				dispositionConsumption.Updates,
				dispositionConsumption.Deletes.Select(item => item.ObjectId).ToArray());
		if (!stored)
			return null;

		var kinahUpdate = CopyInventoryItem(kinahItem, count: senderKinahCount);
		var inventoryItems = sender.InventoryItems.ToList();
		foreach (var itemUpdate in dispositionConsumption.Updates)
		{
			ReplaceInventoryItem(inventoryItems, itemUpdate);
			if (itemTemplates?.GetItemTemplate(itemUpdate.ItemId) is { } itemTemplate)
				await SendPacketAsync(new SmInventoryUpdateItem(itemUpdate, itemTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}

		foreach (var itemDelete in dispositionConsumption.Deletes)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == itemDelete.ObjectId);
			await SendPacketAsync(new SmDeleteItem(itemDelete.ObjectId, SmDeleteItem.UseDeleteType));
		}

		if (attachedItem != null && senderItem != null && senderItemTemplate != null)
		{
			if (attachedItem.ObjectId == senderItem.ObjectId)
			{
				inventoryItems.RemoveAll(item => item.ObjectId == senderItem.ObjectId);
				await SendPacketAsync(new SmDeleteItem(senderItem.ObjectId));
			}
			else
			{
				var reducedItem = CopyInventoryItem(senderItem, count: senderItem.Count - sendMail.ItemCount);
				ReplaceInventoryItem(inventoryItems, reducedItem);
				await SendPacketAsync(new SmInventoryUpdateItem(reducedItem, senderItemTemplate, SmInventoryUpdateItem.DecreaseItemUse));
			}
		}

		ReplaceInventoryItem(inventoryItems, kinahUpdate);
		sender.InventoryItems = inventoryItems.ToArray();
		if (itemTemplates?.GetItemTemplate(KinahItemId) is { } kinahTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(kinahUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));

		await SendPacketAsync(SmMailService.CreateMailMessage(SmMailService.MailSendSuccess));
		var notifiedOnlineRecipient = _connectionRegistry != null
			&& await _connectionRegistry.NotifyMailReceivedAsync(recipient.PlayerObjectId, mail);
		if (!notifiedOnlineRecipient && recipient.PlayerObjectId == sender.ObjectId)
			sender.Mailbox = sender.Mailbox.Concat([mail]).ToArray();
		return null;
	}

	private static InventoryItemConsumption? BuildItemCountConsumption(
		IReadOnlyList<InventoryItem> inventoryItems,
		int itemId,
		int count)
	{
		// Java parity: model/items/storage/Storage.decreaseByItemId used by MailService courier-pass disposition.
		var remaining = count;
		var updates = new List<InventoryItem>();
		var deletes = new List<InventoryItem>();
		foreach (var item in inventoryItems.Where(item => item.ItemId == itemId && item.Location == CubeStorageId).OrderBy(item => item.Slot))
		{
			if (remaining <= 0)
				break;

			var consumed = Math.Min(item.Count, remaining);
			var itemCount = item.Count - consumed;
			if (itemCount == 0)
				deletes.Add(item);
			else
				updates.Add(CopyInventoryItem(item, count: itemCount));
			remaining -= (int)consumed;
		}

		return remaining == 0 ? new InventoryItemConsumption(updates, deletes) : null;
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem replacement)
	{
		var index = items.FindIndex(item => item.ObjectId == replacement.ObjectId);
		if (index >= 0)
			items[index] = replacement;
	}

	private InventoryItem? BuildMailAttachment(InventoryItem? senderItem, ItemTemplateSummary? senderItemTemplate, int recipientObjectId, long attachedCount)
	{
		// Java parity: services/mail/MailService.sendMail attached item move/split before Letter creation.
		if (senderItem == null || attachedCount <= 0)
			return null;
		if (senderItem.Count == attachedCount)
		{
			var packCount = senderItem.PackCount > 0 ? senderItem.PackCount * -1 : senderItem.PackCount;
			return CopyInventoryItem(
				senderItem,
				ownerId: recipientObjectId,
				location: MailboxStorageId,
				isEquipped: false,
				packCount: packCount);
		}

		if (senderItem.Count <= attachedCount)
			return null;
		if (senderItemTemplate == null)
			return null;

		var objectId = _idFactory?.NextId() ?? 0;
		return objectId == 0
			? null
			: CreateNewItem(
				objectId,
				senderItemTemplate,
				attachedCount,
				ownerId: recipientObjectId,
				location: MailboxStorageId,
				slot: FirstAvailableSlot);
	}

	private static InventoryItem CreateNewItem(
		int objectId,
		ItemTemplateSummary itemTemplate,
		long count,
		int ownerId,
		int location,
		long slot)
	{
		return InventoryItemFactory.CreateNewItem(objectId, itemTemplate, count, ownerId, location, slot);
	}

	private sealed record InventoryItemConsumption(IReadOnlyList<InventoryItem> Updates, IReadOnlyList<InventoryItem> Deletes)
	{
		public static InventoryItemConsumption Empty { get; } = new(Array.Empty<InventoryItem>(), Array.Empty<InventoryItem>());
	}

	private sealed record DecomposeRewardInventoryPlan(
		IReadOnlyList<InventoryItem> UpdatedItems,
		IReadOnlyList<InventoryItem> AddedItems,
		IReadOnlyList<DecomposeRewardPacket> Packets);

	private sealed record DecomposeRewardPacket(InventoryItem Item, ItemTemplateSummary Template, bool IsNewItem);

	private static IReadOnlyList<InventoryItem> IncreaseInventoryKinah(
		IReadOnlyList<InventoryItem> inventoryItems,
		int ownerId,
		long amount,
		int fallbackObjectId)
	{
		var items = inventoryItems.ToList();
		var index = items.FindIndex(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (index >= 0)
		{
			items[index] = CopyInventoryItem(items[index], count: items[index].Count + amount);
			return items;
		}

		items.Add(
			new InventoryItem
			{
				ObjectId = fallbackObjectId,
				ItemId = KinahItemId,
				Count = amount,
				OwnerId = ownerId,
				Location = CubeStorageId,
				Slot = FirstAvailableSlot,
			});
		return items;
	}

	private static InventoryItem CopyInventoryItem(
		InventoryItem item,
		int? location = null,
		long? slot = null,
		long? count = null,
		int? ownerId = null,
		bool? isEquipped = null,
		int? packCount = null,
		int? charge = null,
		int? itemSkin = null,
		int? color = null,
		bool setColor = false,
		int? colorExpires = null,
		int? objectId = null)
	{
		var copy = new InventoryItem
		{
			ObjectId = objectId ?? item.ObjectId,
			ItemId = item.ItemId,
			Count = count ?? item.Count,
			Color = setColor ? color : item.Color,
			ColorExpires = colorExpires ?? item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = ownerId ?? item.OwnerId,
			IsEquipped = isEquipped ?? item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = slot ?? item.Slot,
			Location = location ?? item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = itemSkin ?? item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = charge ?? item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = packCount ?? item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}

	private SmStatsInfo CreateStatsInfoPacket(Player player, StaticData? staticData)
	{
		// Java parity: PlayerGameStats.updateStatsVisually emits SM_STATS_INFO after charge state changes.
		return new SmStatsInfo(
			player,
			staticData?.PlayerExperienceTable,
			_gameTimeService?.GameMinutes ?? 0,
			staticData?.ItemTemplates,
			staticData?.ItemRandomBonuses,
			staticData?.ItemSets,
			staticData?.EnchantTemplates,
			staticData?.TemperingTemplates,
			staticData?.SkillTemplates,
			staticData?.TitleTemplates);
	}

	private static SmBindPointInfo CreateBindPointPacket(Player player, StaticData? staticData)
	{
		// Java parity: services/teleport/TeleportService.sendObeliskBindPoint.
		if (player.BindPoint != null)
			return new SmBindPointInfo(player.BindPoint.MapId, player.BindPoint.X, player.BindPoint.Y, player.BindPoint.Z);

		var spawn = staticData?.PlayerInitialData.GetSpawnLocation(player.Race);
		return spawn == null
			? new SmBindPointInfo(player.Position.WorldId, player.Position.X, player.Position.Y, player.Position.Z)
			: new SmBindPointInfo(spawn.MapId, spawn.X, spawn.Y, spawn.Z);
	}

	private async Task<AccountAuthResult> AuthenticateAccountAsync(CmL2AuthLoginCheck auth)
	{
		// Java parity: CM_L2AUTH_LOGIN_CHECK asks login-server for account auth.
		if (_loginServer == null || !_loginServer.IsAuthed)
			return new AccountAuthResult(auth.AccountId, Ok: true, AccountName: $"account-{auth.AccountId}");

		try
		{
			return await _loginServer.RequestAccountAuthAsync(auth.AccountId, auth.LoginOk, auth.PlayOk1, auth.PlayOk2);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Login-server account auth request failed for account {AccountId}", auth.AccountId);
			return new AccountAuthResult(auth.AccountId, Ok: false);
		}
	}

	private async Task NotifyAccountConnectedAsync(int accountId)
	{
		// Java parity: loginserver bridge account connection notification after game auth.
		if (_loginServer == null)
			return;

		var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var remoteIp = (_client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? string.Empty;
		try
		{
			await _loginServer.NotifyAccountConnectedAsync(accountId, nowMillis, remoteIp, _macAddress, _hddSerial);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Login-server account connection notification failed for account {AccountId}", accountId);
		}
	}

	private async Task NotifyAccountDisconnectedAsync()
	{
		// Java parity: loginserver bridge account disconnect notification on client close.
		if (_accountId == 0 || _accountDisconnectNotified)
			return;

		_accountDisconnectNotified = true;
		await NotifyLoginAccountDisconnectedAsync(_accountId);
	}

	private async Task NotifyLoginAccountDisconnectedAsync(int accountId)
	{
		if (_loginServer == null)
			return;

		try
		{
			await _loginServer.NotifyAccountDisconnectedAsync(accountId);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Login-server account disconnect notification failed for account {AccountId}", accountId);
		}
	}

	private async Task<byte[]?> ReadExactOrNullAsync(int length)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await ReadAsync(buffer, offset, length - offset, TimeSpan.FromSeconds(30));
			if (read == 0)
				return null;
			offset += read;
		}

		return buffer;
	}

	private sealed record HouseObjectUseTarget(
		WorldHouse House,
		RegisteredHouseObjectSummary HouseObject,
		HousingObjectTemplateSummary Template,
		bool IsInTalkRange);

	private sealed record PendingHouseObjectUse(ScheduledTask Task, int ObjectId);

	private sealed record PendingItemUse(
		ScheduledTask Task,
		int ItemObjectId,
		int ItemTemplateId,
		string TargetItemName,
		PendingItemUseCancelMessage CancelMessage,
		int? CancelTargetObjectId,
		int CancelEndState,
		int CancelUnknown3,
		int? RemoveCooldownDelayIdOnCancel,
		bool PreserveOnEmotion,
		bool CancelAnimationToSelfOnly);

	private enum PendingItemUseCancelMessage
	{
		None,
		Item,
		EnchantItem,
		ItemIdentify,
		ItemAuthorize,
		ItemCharge,
		ItemCharge2,
		ItemReidentify,
		ManastoneSocket,
		GodstoneSocket,
		SoulBind,
		Decompose,
	}
}
