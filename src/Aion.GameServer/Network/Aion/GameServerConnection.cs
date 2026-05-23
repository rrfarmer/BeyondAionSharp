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
	private readonly RiftPortalInteractionService? _riftPortalInteractionService;
	private readonly WorldNpcLootService? _worldNpcLootService;
	private readonly Func<Player, int, bool>? _isKnownNpc;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly SemaphoreSlim _closeLock = new(1, 1);
	private GameConnectionState _state = GameConnectionState.Connected;
	private int _accountId;
	private string _accountName = string.Empty;
	private byte _accessLevel;
	private byte _membership;
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
		RiftService? riftService = null,
		RiftPortalDialogService? riftPortalDialogService = null,
		RiftPortalUseService? riftPortalUseService = null,
		RiftInformerService? riftInformerService = null,
		VortexLocationService? vortexLocationService = null,
		WorldNpcLootService? worldNpcLootService = null,
		Func<Player, int, bool>? isKnownNpc = null,
		RiftPortalInteractionService? riftPortalInteractionService = null,
		GameCrypt? crypt = null)
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
		_worldNpcLootService = worldNpcLootService;
		_isKnownNpc = isKnownNpc;
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
		_crypt = crypt ?? new GameCrypt();
	}

	internal Player? ActivePlayer => _activePlayer;

	public GameConnectionState State => _state;

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
					_activePlayer = null;
					_accountDisconnectNotified = false;
				}
				else
				{
					_accountId = 0;
					_accountName = string.Empty;
					_accessLevel = 0;
					_membership = 0;
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
			case CmTimeCheck timeCheck:
				// Java parity: network/aion/clientpackets/CM_TIME_CHECK.runImpl sends SM_AFTER_TIME_CHECK_4_7_5 before SM_TIME_CHECK.
				await SendPacketAsync(new SmAfterTimeCheck475());
				await SendPacketAsync(new SmTimeCheck(timeCheck.NanoTime));
				break;
			case CmMayLoginIntoGame:
				await SendPacketAsync(new SmMayLoginIntoGame());
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
			case CmRevive:
				// Java parity: network/aion/clientpackets/CM_REVIVE.runImpl dispatches PlayerReviveService; deferred until revive/combat state is ported.
				break;
			case CmRejectRevive:
				// Java parity: network/aion/clientpackets/CM_REJECT_REVIVE.runImpl has no side effect.
				break;
			case CmTeleportAnimationDone:
				// Java parity: network/aion/clientpackets/CM_TELEPORT_ANIMATION_DONE.runImpl executes a pending teleport task; deferred until teleport task state is ported.
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
			case CmStartLoot startLoot:
				await HandleStartLootAsync(startLoot);
				break;
			case CmLootItem lootItem:
				await HandleLootItemAsync(lootItem);
				break;
			case CmSubzoneChange:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_SUBZONE_CHANGE.runImpl -> Player.revalidateZones.
					RevalidatePlayerFlightZones(_activePlayer);
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
			case CmCheckPak:
				// Java parity: network/aion/clientpackets/CM_CHECK_PAK.runImpl only audit-logs suspicious pak status; deferred until audit logging policy is ported.
				break;
			case CmPlayMovieEnd:
				// Java parity: network/aion/clientpackets/CM_PLAY_MOVIE_END.runImpl dispatches quest and instance movie-end hooks; deferred until those systems are ported.
				break;
			case CmShowMap:
				// Java parity: network/aion/clientpackets/CM_SHOW_MAP.runImpl action 0 dispatches ConquerorAndProtectorService.intruderScan; deferred until that system is ported.
				break;
			case CmCheckMailUnknown:
				// Java parity: network/aion/clientpackets/CM_CHECK_MAIL_UNK.runImpl is TODO/no-op.
				break;
			case CmObjectSearch:
				// Java parity: network/aion/clientpackets/CM_OBJECT_SEARCH.runImpl searches DataManager.SPAWNS_DATA; deferred until spawn search data is ported.
				break;
			case CmPlayerListener:
				// Java parity: network/aion/clientpackets/CM_PLAYER_LISTENER.runImpl dispatches WebRewardService when enabled; deferred until web rewards are ported.
				break;
			case CmDeleteQuest:
				// Java parity: network/aion/clientpackets/CM_DELETE_QUEST.runImpl cancels timed quests and dispatches QuestService.abandonQuest; deferred until quest mutation is ported.
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
			case CmClientCommandRoll commandRoll:
				if (_activePlayer != null)
					await HandleClientCommandRollAsync(_activePlayer, commandRoll);
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
					RevalidatePlayerFlightZones(_activePlayer);
				}
				break;
			case CmQuestionResponse questionResponse:
				if (_activePlayer != null)
					await HandleQuestionResponseAsync(_activePlayer, questionResponse);
				break;
			case CmShowDialog showDialog:
				if (_activePlayer != null)
					await HandleShowDialogAsync(_activePlayer, showDialog);
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
			case CmBlockSetReason blockSetReason:
				if (_activePlayer != null)
					await HandleBlockSetReasonAsync(_activePlayer, blockSetReason);
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
						await SendPacketAsync(SmMailService.CreateReadPacket(
							_activePlayer.Mailbox,
							letter,
							_runtimeContext?.DataManager?.StaticData.ItemTemplates));
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
					_activePlayer.AccessLevel = _accessLevel;
					_activePlayer.AccountMembership = _membership;
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

					var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
					if (itemTemplates != null)
					{
						foreach (var inventoryPacket in SmInventoryInfo.CreateLoginPackets(
							enterWorldResult.Player,
							itemTemplates,
							_idFactory == null ? null : () => _idFactory.NextId()))
						{
							await SendPacketAsync(inventoryPacket);
						}
					}

					var staticData = _runtimeContext?.DataManager?.StaticData;
					// Java parity: CreatureController.onAfterSpawn revalidates zones after the player enters the world.
					RevalidatePlayerFlightZones(enterWorldResult.Player);
					await SendPacketAsync(new SmChannelInfo(enterWorldResult.Player.Position, staticData?.WorldMaps ?? Array.Empty<WorldMapSummary>()));
					await SendPacketAsync(CreateBindPointPacket(enterWorldResult.Player, staticData));
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
						foreach (var warehousePacket in SmWarehouseInfo.CreateLoginPackets(enterWorldResult.Player, itemTemplates))
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

	private async Task HandleLevelReadyAsync(Player player)
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

		await SendPacketAsync(new SmPlayerInfo(player, staticData?.PlayerExperienceTable));
		await SendPacketAsync(CreateAccountPropertiesPacket());
		await SendPacketAsync(new SmMotion(player.ObjectId, player.Motions));
		await PlayerLevelReadyFlightNotifier.NotifyIfFlyingAsync(
			player,
			_connectionRegistry,
			_connectionRegistry == null
				? null
				: new PlayerVisualStatsUpdateService(_connectionRegistry, _runtimeContext, _gameTimeService));
		await SendPacketAsync(SmCubeUpdate.CubeSize(player));
	}

	private SmAccountProperties CreateAccountPropertiesPacket()
	{
		// Java parity: network/aion/serverpackets/SM_ACCOUNT_PROPERTIES uses AdminConfig.GM_PANEL.
		return new SmAccountProperties(_accessLevel >= _options.Administration.GmPanelAccessLevel);
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

	private async Task HandleDialogSelectAsync(Player player, CmDialogSelect packet)
	{
		// Java parity: network/aion/clientpackets/CM_DIALOG_SELECT.runImpl -> services/DialogService.onDialogSelect narrow charge-all branch.
		if (player.IsTrading)
			return;

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
			() => _idFactory?.NextId() ?? 0);

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
		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		if (itemTemplates == null || player.PendingChargeAllRequest != null)
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
		player.PendingChargeAllRequest = new PendingChargeAllRequest(
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

		await SendPacketAsync(
			new SmQuestionWindow(
				GetChargeAllQuestionId(chargeWay),
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
			.Where(item => selectedObjectIds.Contains(item.ObjectId) && item.Location == CubeStorageId && !item.IsEquipped)
			.ToArray())
		{
			var currentItem = inventoryItems.FirstOrDefault(item => item.ObjectId == selectedItem.ObjectId);
			if (currentItem == null)
				continue;

			var plan = ItemChargeService.CreateChargePlan(player, currentItem, itemTemplates, packet.ChargeLevel, ignoreRankRequirement: false, requirePayment: true);
			if (plan == null)
				continue;

			var chargedItem = CopyInventoryItem(currentItem, charge: plan.TargetChargePoints);
			var oldAbyssRank = player.AbyssRank.Rank;
			InventoryItem? kinahUpdate = null;
			PlayerAbyssRank? abyssRankUpdate = null;
			switch (plan.ChargeWay)
			{
				case 1:
					if (plan.PaymentAmount > 0)
					{
						var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
						if (kinahItem == null || kinahItem.Count < plan.PaymentAmount)
							continue;
						kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - plan.PaymentAmount);
					}
					break;
				case 2:
					if (plan.PaymentAmount == 0)
						break;
					if (plan.PaymentAmount > int.MaxValue || player.AbyssRank.Ap < plan.PaymentAmount)
						continue;
					abyssRankUpdate = player.AbyssRank.AddAp(-(int)plan.PaymentAmount);
					break;
				default:
					continue;
			}

			var saved = _playerEnterWorldService == null
				|| await _playerEnterWorldService.SaveItemChargeMutationAsync(player, chargedItem, kinahUpdate, abyssRankUpdate);
			if (!saved)
				continue;

			ReplaceInventoryItem(inventoryItems, chargedItem);
			if (kinahUpdate != null)
				ReplaceInventoryItem(inventoryItems, kinahUpdate);
			if (abyssRankUpdate != null)
				player.AbyssRank = abyssRankUpdate;
			player.InventoryItems = inventoryItems.ToArray();

			if (kinahUpdate != null && itemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
				await SendPacketAsync(new SmInventoryUpdateItem(kinahUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
			if (abyssRankUpdate != null)
			{
				await SendPacketAsync(SmSystemMessage.UseAbyssPoint(plan.PaymentAmount));
				await SendPacketAsync(new SmAbyssRank(player.AbyssRank));
				await ApplyAbyssRankChangedSideEffectsAsync(player, oldAbyssRank, staticData);
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
			await SendPacketAsync(new SmInventoryUpdateItem(plan.SourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseStigmaUse));
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
				await SendPacketAsync(new SmInventoryUpdateItem(plan.TargetItemUpdate, targetTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}
		else
		{
			if (plan.TargetItemUpdate != null)
				await SendPacketAsync(new SmInventoryUpdateItem(plan.TargetItemUpdate, targetTemplate, SmInventoryUpdateItem.DecreaseStigmaUse));
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
		var itemTemplates = staticData.ItemTemplates;
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
				await SendPacketAsync(new SmInventoryUpdateItem(supplementUpdate, supplementTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}
		foreach (var deletedSupplementItemObjectId in plan.DeletedSupplementItemObjectIds)
			await SendPacketAsync(new SmDeleteItem(deletedSupplementItemObjectId, SmDeleteItem.UseDeleteType));
		await SendItemUseMutationAsync(plan.SourceItemUpdate, plan.DeletedSourceItemObjectId, sourceTemplate);

		foreach (var removedSkill in plan.RemovedBuffSkills)
			await SendPacketAsync(new SmSkillRemove(removedSkill));
		foreach (var addedSkill in plan.AddedBuffSkills)
			await SendPacketAsync(new SmSkillList([addedSkill], 1300050));

		if (plan.EnchantSucceeded)
		{
			await SendPacketAsync(SmSystemMessage.EnchantItemSucceedNew(plan.ItemName, plan.NewEnchantLevel));
			if (plan.EnchantBuffSkillId != 0)
			{
				var skillTemplate = staticData.SkillTemplates.GetSkillTemplate(plan.EnchantBuffSkillId);
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
			await SendPacketAsync(new SmInventoryUpdateItem(plan.TargetItemUpdate, targetTemplate, updateType: 0));
		else if (plan.DeletedTargetItemObjectId.HasValue)
			await SendPacketAsync(new SmDeleteItem(plan.DeletedTargetItemObjectId.Value));

		if (plan.TargetDestroyed)
			await SendPacketAsync(SmSystemMessage.EnchantType1EnchantFail(plan.ItemName));

		if (plan.RefreshStats)
			await SendPacketAsync(CreateStatsInfoPacket(player, staticData));

		await BroadcastItemUsageAnimationAsync(
			player,
			new SmItemUsageAnimation(
				player.ObjectId,
				packet.StoneObjectId,
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
				PendingItemUseCancelMessage.Item => SmSystemMessage.ItemCanceled(),
				PendingItemUseCancelMessage.ItemCharge => SmSystemMessage.ItemChargeCanceled(),
				PendingItemUseCancelMessage.ItemCharge2 => SmSystemMessage.ItemCharge2Canceled(),
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
				await SendPacketAsync(new SmInventoryUpdateItem(supplementUpdate, supplementTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}
		foreach (var deletedSupplementItemObjectId in plan.DeletedSupplementItemObjectIds)
			await SendPacketAsync(new SmDeleteItem(deletedSupplementItemObjectId, SmDeleteItem.UseDeleteType));
		await SendItemUseMutationAsync(plan.SourceItemUpdate, plan.DeletedSourceItemObjectId, sourceTemplate);
		await SendPacketAsync(
			plan.SocketSucceeded
				? SmSystemMessage.GiveItemOptionSucceed(plan.ItemName)
				: SmSystemMessage.GiveItemOptionFailed(plan.ItemName));
		await SendPacketAsync(new SmInventoryUpdateItem(plan.TargetItemUpdate, targetTemplate, updateType: 0));
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
		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
		if (itemTemplates == null)
			return;

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

				await CompleteSocketGodstoneAsync(player, packet.StoneObjectId, plan, sourceTemplate, itemTemplates, cancellationToken);
			});
	}

	private async Task CompleteSocketGodstoneAsync(
		Player player,
		int stoneObjectId,
		GodstoneSocketPlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateTable itemTemplates,
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

		if (plan.SourceItemUpdate != null)
			await SendPacketAsync(new SmInventoryUpdateItem(plan.SourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		else if (plan.DeletedSourceItemObjectId.HasValue)
			await SendPacketAsync(new SmDeleteItem(plan.DeletedSourceItemObjectId.Value, SmDeleteItem.UseDeleteType));

		await SendPacketAsync(SmSystemMessage.GiveItemProcEnchantedTargetItem(plan.ItemName));
		if (itemTemplates.GetItemTemplate(plan.TargetItemUpdate.ItemId) is { } targetTemplate)
			await SendPacketAsync(new SmInventoryUpdateItem(plan.TargetItemUpdate, targetTemplate, updateType: 0));
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

		player.InventoryItems = plan.InventoryItems;
		await SendItemUseMutationAsync(plan.MaterialItemUpdate, plan.DeletedMaterialItemObjectId, materialTemplate);
		await SendItemUseMutationAsync(plan.ToolItemUpdate, plan.DeletedToolItemObjectId, toolTemplate);
		await SendPacketAsync(SmSystemMessage.ExceedSucceed(plan.ItemName));
		await SendPacketAsync(new SmInventoryUpdateItem(plan.TargetItemUpdate, targetTemplate, updateType: 0));
	}

	private async Task SendItemUseMutationAsync(InventoryItem? itemUpdate, int? deletedItemObjectId, ItemTemplateSummary? template)
	{
		if (itemUpdate != null && template != null)
			await SendPacketAsync(new SmInventoryUpdateItem(itemUpdate, template, SmInventoryUpdateItem.DecreaseItemUse));
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
		var itemTemplates = staticData?.ItemTemplates;
		if (itemTemplates == null)
			return;

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
			await SendPacketAsync(new SmInventoryUpdateItem(plan.ItemUpdate, itemTemplate, updateType: 0));
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
		if (player.PendingSoulBindRequest != null)
		{
			await SendPacketAsync(SmSystemMessage.SoulBoundCloseOtherMsgBoxAndRetry());
			return;
		}

		player.PendingSoulBindRequest = new PendingSoulBindRequest(change.SoulBindItemObjectId, change.SoulBindSlot, change.ItemName);
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

		player.InventoryItems = change.InventoryItems;
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
		// Java parity: ResponseRequester handler registered by Equipment.soulBindItem.
		var request = player.PendingSoulBindRequest;
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
			await SendPacketAsync(new SmInventoryUpdateItem(plan.ExtractItemUpdate, extractTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}

		player.InventoryItems = inventoryItems.ToArray();
		await SendPacketAsync(new SmInventoryUpdateItem(plan.TargetItemUpdate!, keepTemplate, SmInventoryUpdateItem.DecreaseItemUse));
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

		await SendConsumedItemPacketsAsync(inventoryItems, mutationPlan.UpdatedConsumedItems, mutationPlan.DeletedConsumedObjectIds, staticData.ItemTemplates);
		ApplyConsumedAndRewardInventoryMutation(inventoryItems, mutationPlan);
		player.InventoryItems = inventoryItems.ToArray();
		if (mutationPlan.RewardItemId != 0 && staticData.ItemTemplates.GetItemTemplate(mutationPlan.RewardItemId) is { } rewardTemplate)
		{
			RegisterCompositionExpirableAddedItems(player, mutationPlan.AddedRewardItems, rewardTemplate);
			if (HasRewardMutation(mutationPlan.UpdatedRewardItems, mutationPlan.AddedRewardItems))
				await SendCompositionRewardPacketsAsync(mutationPlan, rewardTemplate);
		}

		if (!mutationPlan.RewardSucceeded && mutationPlan.RewardInventoryFull)
			await SendPacketAsync(SmSystemMessage.DiceInventoryError());
		await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, toolItem.ObjectId, toolItem.ItemId, 0, 1, 0));
	}

	private async Task SendConsumedItemPacketsAsync(
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<InventoryItem> updatedConsumedItems,
		IReadOnlyList<int> deletedConsumedObjectIds,
		ItemTemplateTable itemTemplates)
	{
		foreach (var updatedItem in updatedConsumedItems)
		{
			var template = itemTemplates.GetItemTemplate(updatedItem.ItemId);
			if (template != null)
				await SendPacketAsync(new SmInventoryUpdateItem(updatedItem, template, SmInventoryUpdateItem.DecreaseItemUse));
		}

		foreach (var deletedObjectId in deletedConsumedObjectIds)
		{
			if (inventoryItems.Any(item => item.ObjectId == deletedObjectId))
				await SendPacketAsync(new SmDeleteItem(deletedObjectId, SmDeleteItem.UseDeleteType));
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

	private async Task SendCompositionRewardPacketsAsync(CompositionMutationPlan mutationPlan, ItemTemplateSummary rewardTemplate)
	{
		foreach (var updatedReward in mutationPlan.UpdatedRewardItems)
			await SendPacketAsync(new SmInventoryUpdateItem(updatedReward, rewardTemplate, SmInventoryUpdateItem.IncreaseItemCollect));
		foreach (var addedReward in mutationPlan.AddedRewardItems)
			await SendPacketAsync(SmInventoryAddItem.CreateItemCollect(addedReward, rewardTemplate));
	}

	private static int RandomInclusive(int min, int max)
	{
		return Random.Shared.Next(min, max + 1);
	}

	private async Task HandleUseItemAsync(Player player, CmUseItem packet)
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
			await HandleInventoryExpansionUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate, itemTemplates);
			return;
		}

		if (sourceTemplate.DyeAction != null)
		{
			await HandleDyeUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate, packet.TargetItemObjectId, itemTemplates);
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

		if (sourceTemplate.ChargeActionMaxLevel > 0)
			await HandleChargeUseItemAsync(player, inventoryItems, sourceItem, sourceTemplate, staticData);
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

		var validation = AssemblyItemService.CanAct(player, sourceTemplate, staticData.AssemblyItems);
		if (!validation.Succeeded || validation.AssemblyItem == null)
		{
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 2, 0));
			return;
		}

		var rewardTemplate = staticData.ItemTemplates.GetItemTemplate(validation.AssemblyItem.ItemId);
		if (rewardTemplate == null)
		{
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 2, 0));
			return;
		}

		var mutationPlan = AssemblyItemService.CreateMutationPlan(
			player,
			inventoryItems,
			validation.AssemblyItem,
			rewardTemplate,
			staticData.ItemTemplates,
			() => _idFactory?.NextId() ?? 0);
		if (!mutationPlan.Succeeded)
		{
			await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 2, 0));
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

		await SendAssemblyConsumedPartPacketsAsync(inventoryItems, mutationPlan, staticData.ItemTemplates);
		ApplyAssemblyInventoryMutation(inventoryItems, mutationPlan);
		player.InventoryItems = inventoryItems.ToArray();
		RegisterAssemblyExpirableAddedItems(player, mutationPlan.AddedRewardItems, rewardTemplate);

		await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 0));
		await SendPacketAsync(SmSystemMessage.AssemblyItemSucceeded());
		if (HasRewardMutation(mutationPlan.UpdatedRewardItems, mutationPlan.AddedRewardItems))
			await SendAssemblyRewardPacketsAsync(mutationPlan, rewardTemplate);
		if (!mutationPlan.RewardSucceeded && mutationPlan.RewardInventoryFull)
			await SendPacketAsync(SmSystemMessage.DiceInventoryError());
	}

	private async Task SendAssemblyConsumedPartPacketsAsync(
		IReadOnlyList<InventoryItem> inventoryItems,
		AssemblyItemMutationPlan mutationPlan,
		ItemTemplateTable itemTemplates)
	{
		foreach (var updatedPart in mutationPlan.UpdatedPartItems)
		{
			var template = itemTemplates.GetItemTemplate(updatedPart.ItemId);
			if (template != null)
				await SendPacketAsync(new SmInventoryUpdateItem(updatedPart, template, SmInventoryUpdateItem.DecreaseItemUse));
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

	private async Task SendAssemblyRewardPacketsAsync(AssemblyItemMutationPlan mutationPlan, ItemTemplateSummary rewardTemplate)
	{
		foreach (var updatedReward in mutationPlan.UpdatedRewardItems)
			await SendPacketAsync(new SmInventoryUpdateItem(updatedReward, rewardTemplate, SmInventoryUpdateItem.IncreaseItemCollect));
		foreach (var addedReward in mutationPlan.AddedRewardItems)
			await SendPacketAsync(SmInventoryAddItem.CreateItemCollect(addedReward, rewardTemplate));
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

		await ApplyExpExtractSourceMutationAsync(inventoryItems, sourceTemplate, mutationPlan.SourceItemUpdate, mutationPlan.DeletedSourceItemObjectId);
		player.Exp = validation.NewExp;
		await SendPacketAsync(new SmStatUpdateExp(player, staticData.PlayerExperienceTable));
		ApplyExpExtractRewardMutation(inventoryItems, mutationPlan);
		player.InventoryItems = inventoryItems.ToArray();
		RegisterExpExtractExpirableAddedItems(player, mutationPlan.AddedRewardItems, validation.RewardTemplate);

		if (HasRewardMutation(mutationPlan.UpdatedRewardItems, mutationPlan.AddedRewardItems))
			await SendExpExtractRewardPacketsAsync(mutationPlan, validation.RewardTemplate);
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
		int? deletedSourceItemObjectId)
	{
		if (deletedSourceItemObjectId.HasValue)
		{
			inventoryItems.RemoveAll(item => item.ObjectId == deletedSourceItemObjectId.Value);
			await SendPacketAsync(new SmDeleteItem(deletedSourceItemObjectId.Value, SmDeleteItem.UseDeleteType));
		}
		else if (sourceItemUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, sourceItemUpdate);
			await SendPacketAsync(new SmInventoryUpdateItem(sourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
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

	private async Task SendExpExtractRewardPacketsAsync(ExpExtractMutationPlan mutationPlan, ItemTemplateSummary rewardTemplate)
	{
		foreach (var updatedReward in mutationPlan.UpdatedRewardItems)
			await SendPacketAsync(new SmInventoryUpdateItem(updatedReward, rewardTemplate, SmInventoryUpdateItem.IncreaseItemCollect));
		foreach (var addedReward in mutationPlan.AddedRewardItems)
			await SendPacketAsync(SmInventoryAddItem.CreateItemCollect(addedReward, rewardTemplate));
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
			staticData.ItemTemplates);
		if (!plan.Succeeded || plan.AbyssRankUpdate == null)
			return;

		AddItemCooldownIfNeeded(player, sourceTemplate, removeOnCancel: false);
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveApExtractActionMutationAsync(player, plan);
		if (!saved)
			return;

		var oldAbyssRank = player.AbyssRank.Rank;
		var inventoryItems = player.InventoryItems.ToList();
		await SendApExtractConsumedItemPacketsAsync(inventoryItems, plan, sourceTemplate);
		ApplyApExtractInventoryMutation(inventoryItems, plan);
		player.InventoryItems = inventoryItems.ToArray();
		player.AbyssRank = plan.AbyssRankUpdate;
		await SendPacketAsync(SmSystemMessage.CombatMyAbyssPointGain(plan.AbyssPoints));
		await SendPacketAsync(new SmAbyssRank(player.AbyssRank));
		await ApplyAbyssRankChangedSideEffectsAsync(player, oldAbyssRank, staticData);
	}

	private async Task SendApExtractConsumedItemPacketsAsync(
		IReadOnlyList<InventoryItem> inventoryItems,
		ApExtractPlan plan,
		ItemTemplateSummary sourceTemplate)
	{
		if (inventoryItems.Any(item => item.ObjectId == plan.DeletedTargetItemObjectId))
			await SendPacketAsync(new SmDeleteItem(plan.DeletedTargetItemObjectId, SmDeleteItem.UseDeleteType));

		if (plan.SourceItemUpdate != null)
		{
			await SendPacketAsync(new SmInventoryUpdateItem(plan.SourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}
		else if (plan.DeletedSourceItemObjectId.HasValue && inventoryItems.Any(item => item.ObjectId == plan.DeletedSourceItemObjectId.Value))
		{
			await SendPacketAsync(new SmDeleteItem(plan.DeletedSourceItemObjectId.Value, SmDeleteItem.UseDeleteType));
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

		await SendExtractConsumedItemPacketsAsync(inventoryItems, plan, sourceTemplate);
		ApplyBreakItemInventoryMutation(inventoryItems, plan);
		player.InventoryItems = inventoryItems.ToArray();

		if (staticData.ItemTemplates.GetItemTemplate(plan.RewardItemId) is { } rewardTemplate)
		{
			RegisterExtractExpirableAddedItems(player, plan.AddedRewardItems, rewardTemplate);
			if (HasRewardMutation(plan.UpdatedRewardItems, plan.AddedRewardItems))
				await SendExtractRewardPacketsAsync(plan, rewardTemplate);
		}

		if (!plan.RewardSucceeded && plan.RewardInventoryFull)
			await SendPacketAsync(SmSystemMessage.DiceInventoryError());
		await SendPacketAsync(new SmItemUsageAnimation(player.ObjectId, sourceItemObjectId, sourceTemplate.TemplateId, 0, 1, 0));
	}

	private async Task SendExtractConsumedItemPacketsAsync(
		IReadOnlyList<InventoryItem> inventoryItems,
		BreakItemPlan plan,
		ItemTemplateSummary sourceTemplate)
	{
		if (inventoryItems.Any(item => item.ObjectId == plan.DeletedTargetItemObjectId))
			await SendPacketAsync(new SmDeleteItem(plan.DeletedTargetItemObjectId, SmDeleteItem.UseDeleteType));

		if (plan.SourceItemUpdate != null)
		{
			await SendPacketAsync(new SmInventoryUpdateItem(plan.SourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		}
		else if (plan.DeletedSourceItemObjectId.HasValue && inventoryItems.Any(item => item.ObjectId == plan.DeletedSourceItemObjectId.Value))
		{
			await SendPacketAsync(new SmDeleteItem(plan.DeletedSourceItemObjectId.Value, SmDeleteItem.UseDeleteType));
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

	private async Task SendExtractRewardPacketsAsync(BreakItemPlan plan, ItemTemplateSummary rewardTemplate)
	{
		foreach (var updatedReward in plan.UpdatedRewardItems)
			await SendPacketAsync(new SmInventoryUpdateItem(updatedReward, rewardTemplate, SmInventoryUpdateItem.IncreaseItemCollect));
		foreach (var addedReward in plan.AddedRewardItems)
			await SendPacketAsync(SmInventoryAddItem.CreateItemCollect(addedReward, rewardTemplate));
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

		await SendPacketAsync(SmSystemMessage.DecomposeItemSucceed(sourceTemplate.GetClientName() ?? sourceTemplate.Name));
		await ApplySourceItemMutationAsync(inventoryItems, sourceTemplate, sourceItemUpdate, deletedSourceObjectId);
		ApplyRewardInventoryMutation(inventoryItems, rewardInventoryPlan);
		player.InventoryItems = inventoryItems.ToArray();
		RegisterExpirableAddedItems(player, rewardInventoryPlan.Packets);
		await BroadcastItemUsageAnimationAsync(player, new SmItemUsageAnimation(player.ObjectId, sourceItem.ObjectId, sourceItem.ItemId, 0, 1, 0));
		await SendDecomposeRewardItemsAsync(rewardInventoryPlan.Packets);
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
		await ApplySourceItemMutationAsync(inventoryItems, sourceTemplate, sourceItemUpdate, deletedSourceObjectId);
		await SendPacketAsync(new SmSecondaryShowDecomposable(sourceItem.ObjectId, Array.Empty<ResultedItemSummary>()));
		ApplyRewardInventoryMutation(inventoryItems, rewardInventoryPlan);
		player.InventoryItems = inventoryItems.ToArray();
		RegisterExpirableAddedItems(player, rewardInventoryPlan.Packets);
		await SendDecomposeRewardItemsAsync(rewardInventoryPlan.Packets);
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

	private async Task SendDecomposeRewardItemsAsync(IReadOnlyList<DecomposeRewardPacket> rewardPackets)
	{
		foreach (var rewardPacket in rewardPackets)
		{
			if (rewardPacket.IsNewItem)
			{
				await SendPacketAsync(
					SmInventoryAddItem.CreateDecomposable(
					[
						new SmInventoryAddItem.InventoryPacketItem(rewardPacket.Item, rewardPacket.Template),
					]));
			}
			else
			{
				await SendPacketAsync(new SmInventoryUpdateItem(rewardPacket.Item, rewardPacket.Template, SmInventoryUpdateItem.IncreaseItemCollect));
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
		List<InventoryItem> inventoryItems,
		ItemTemplateSummary sourceTemplate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceObjectId)
	{
		if (deletedSourceObjectId.HasValue)
		{
			ApplySourceInventoryMutation(inventoryItems, sourceItemUpdate, deletedSourceObjectId);
			await SendPacketAsync(new SmDeleteItem(deletedSourceObjectId.Value, SmDeleteItem.UseDeleteType));
		}
		else if (sourceItemUpdate != null)
		{
			ApplySourceInventoryMutation(inventoryItems, sourceItemUpdate, deletedSourceObjectId);
			await SendPacketAsync(new SmInventoryUpdateItem(sourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
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
			await SendPacketAsync(new SmInventoryUpdateItem(sourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
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

		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveSkillLearnActionMutationAsync(
				player,
				plan.PersistedSkills,
				sourceItemUpdate,
				deletedSourceObjectId);
		if (!saved)
			return;

		player.Skills = plan.Skills;
		foreach (var packet in plan.Packets)
			await SendPacketAsync(new SmSkillList([packet.Skill], packet.MessageId));

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
		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveTitleAddActionMutationAsync(
				player,
				title,
				sourceItemUpdate,
				deletedSourceObjectId);
		if (!saved)
			return;

		player.Titles = player.Titles
			.Where(existing => existing.Id != title.Id)
			.Append(title)
			.ToArray();
		_expirableTaskService?.RegisterTitle(player, title);
		await SendPacketAsync(SmSystemMessage.CashTitle(ChatUtil.L10n(validation.TitleTemplate!.NameId)));
		await SendPacketAsync(new SmTitleInfo(player.Titles));

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
		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		int? deletedSourceObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;
		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveEmotionLearnActionMutationAsync(
				player,
				emotion,
				sourceItemUpdate,
				deletedSourceObjectId);
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
		ItemTemplateTable itemTemplates)
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
			await SendPacketAsync(new SmInventoryUpdateItem(sourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
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
		ItemTemplateTable itemTemplates)
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
			await SendPacketAsync(new SmInventoryUpdateItem(sourceItemUpdate, sourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
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

		await SendPacketAsync(new SmInventoryUpdateItem(targetItemUpdate, targetTemplate, updateType: 0));
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
			},
			cancelEndState: 2);
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
			await SendPacketAsync(new SmInventoryUpdateItem(polishPlan.SourceItemUpdate, polishPlan.SourceTemplate, SmInventoryUpdateItem.DecreaseItemUse));
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
		await SendPacketAsync(new SmInventoryUpdateItem(polishPlan.TargetItemUpdate, polishPlan.TargetTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		if (polishPlan.TargetItemUpdate.IsEquipped)
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

	private async Task HandleFriendAddAsync(Player requester, CmFriendAdd packet)
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

		if (target.PendingFriendRequest != null)
		{
			await SendPacketAsync(SmSystemMessage.BuddyListBusy());
			return;
		}

		target.PendingFriendRequest = new PendingFriendRequest(requester.ObjectId, requester.Name);
		var sent = await _connectionRegistry.SendPacketToPlayerAsync(
			target.ObjectId,
			new SmQuestionWindow(
				SmQuestionWindow.BuddyListAddBuddyRequest,
				requester.ObjectId,
				0,
				requester.Name,
				packet.Message));
		if (!sent)
			target.PendingFriendRequest = null;
	}

	private async Task HandleChargeAllQuestionResponseAsync(Player player, CmQuestionResponse packet)
	{
		// Java parity: ResponseRequester handler registered by ItemChargeService.startChargingEquippedItems.
		var request = player.PendingChargeAllRequest;
		if (request == null || packet.QuestionId != GetChargeAllQuestionId(request.ChargeWay))
			return;

		player.PendingChargeAllRequest = null;
		if (packet.Response == 0)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		var itemTemplates = staticData?.ItemTemplates;
		if (staticData == null || itemTemplates == null || request.Items.Count == 0)
			return;

		var inventoryItems = player.InventoryItems.ToList();
		var oldAbyssRank = player.AbyssRank.Rank;
		InventoryItem? kinahUpdate = null;
		PlayerAbyssRank? abyssRankUpdate = null;
		switch (request.ChargeWay)
		{
			case 1:
				if (request.PaymentAmount > 0)
				{
					var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
					if (kinahItem == null || kinahItem.Count < request.PaymentAmount)
						return;
					kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - request.PaymentAmount);
				}
				break;
			case 2:
				if (request.PaymentAmount > int.MaxValue || player.AbyssRank.Ap < request.PaymentAmount)
					return;
				if (request.PaymentAmount > 0)
					abyssRankUpdate = player.AbyssRank.AddAp(-(int)request.PaymentAmount);
				break;
			default:
				return;
		}

		var chargedItems = request.Items
			.Select(pending =>
			{
				var currentItem = inventoryItems.FirstOrDefault(item => item.ObjectId == pending.ObjectId && item.Location == CubeStorageId && item.IsEquipped);
				return currentItem == null ? null : CopyInventoryItem(currentItem, charge: pending.TargetCharge);
			})
			.Where(item => item != null)
			.Cast<InventoryItem>()
			.ToArray();
		if (chargedItems.Length == 0)
			return;

		var saved = _playerEnterWorldService == null
			|| await _playerEnterWorldService.SaveItemChargeAllMutationAsync(player, chargedItems, kinahUpdate, abyssRankUpdate);
		if (!saved)
			return;

		if (kinahUpdate != null)
		{
			ReplaceInventoryItem(inventoryItems, kinahUpdate);
			if (itemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
				await SendPacketAsync(new SmInventoryUpdateItem(kinahUpdate, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));
		}
		if (abyssRankUpdate != null)
		{
			player.AbyssRank = abyssRankUpdate;
			await SendPacketAsync(SmSystemMessage.UseAbyssPoint(request.PaymentAmount));
			await SendPacketAsync(new SmAbyssRank(player.AbyssRank));
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
		}

		player.InventoryItems = inventoryItems.ToArray();
		if (abyssRankUpdate != null)
			await ApplyAbyssRankChangedSideEffectsAsync(player, oldAbyssRank, staticData);
		await SendPacketAsync(CreateStatsInfoPacket(player, staticData));
		await SendPacketAsync(
			request.ChargeWay == 1
				? SmSystemMessage.ItemChargeAllComplete()
				: SmSystemMessage.ItemCharge2AllComplete());
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

	private async Task HandleQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		// Java parity: network/aion/clientpackets/CM_QUESTION_RESPONSE.runImpl.
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

		if (packet.QuestionId != SmQuestionWindow.BuddyListAddBuddyRequest)
			return;

		// Java parity: CM_QUESTION_RESPONSE through ResponseRequester for buddy-list request handlers.
		var request = responder.PendingFriendRequest;
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

		if (_chatServer != null)
			await _chatServer.SendPlayerLogoutAsync(player.ObjectId);
		_expirableTaskService?.UnregisterPlayer(player);
		await DismissPostmanAsync(player, notifyClient: notifyPostmanClient);
		_pendingHouseObjectUse?.Task.Cancel();
		_pendingHouseObjectUse = null;
		ReleaseHouseObjectOccupants(player.ObjectId);
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, new SmDelete(player.ObjectId));
		_connectionRegistry?.UnregisterPlayerConnection(player.ObjectId, this);
		if (_playerEnterWorldService != null)
			await _playerEnterWorldService.LeaveWorldAsync(player);
		else
			_world?.TryRemoveObject(player.ObjectId, out _);
		_activePlayer = null;
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

		// TODO Phase 6: apply PlayerController stance guard once stance state is ported.
		if (player.IsInState(PlayerCreatureState.PrivateShop)
			|| (player.IsInState(PlayerCreatureState.WeaponEquipped)
				&& packet.EmotionType is EmotionType.ChairSit or EmotionType.Jump))
			return;

		await CancelPendingItemUseOnEmotionAsync(player);
		if (packet.EmotionType == EmotionType.SelectTarget)
			return;

		// Java parity: network/aion/clientpackets/CM_EMOTION.runImpl stance guard after cancelUseItem/cancelCurrentSkill.
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

	private PlayerZoneRevalidationResult RevalidatePlayerFlightZones(Player player)
	{
		var staticData = _runtimeContext?.DataManager?.StaticData;
		return PlayerZoneStateService.RevalidateFlightZones(
			player,
			staticData?.WorldMaps ?? Array.Empty<WorldMapSummary>(),
			staticData?.FlightZones);
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

		player.Position = player.Position with
		{
			X = packet.X,
			Y = packet.Y,
			Z = packet.Z,
			Heading = packet.Heading,
		};
		// Java parity: CM_MOVE.notifyControllers -> CreatureController.onMove/onStopMove -> ZoneUpdateService.revalidateZones.
		RevalidatePlayerFlightZones(player);

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
				await SendPacketAsync(new SmInventoryUpdateItem(updatedReward, rewardTemplate, SmInventoryUpdateItem.IncreaseItemCollect), cancellationToken);
			foreach (var addedReward in rewardPlan.AddedItems)
				await SendPacketAsync(SmInventoryAddItem.CreateItemCollect(addedReward, rewardTemplate), cancellationToken);
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
		// Java parity: PositionUtil.isInTalkRange(player, HouseObject) uses template talkingDistance + 1.
		var playerPosition = player.Position;
		if (playerPosition.WorldId != house.Position.WorldId)
			return false;

		var range = template.TalkingDistance > 0
			? template.TalkingDistance + 1
			: WorldVisibility.DefaultVisibleDistance;
		var deltaX = playerPosition.X - houseObject.X;
		var deltaY = playerPosition.Y - houseObject.Y;
		var deltaZ = playerPosition.Z - houseObject.Z;
		return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ <= range * range;
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

	private async Task SpawnPostmanAsync(Player player)
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
		_world?.TryAddObject(postman.ObjectId, postman);
		var postmanPacket = new SmNpcInfo(postman);
		if (_connectionRegistry != null)
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(postman.Position, postman.ObjectId, postmanPacket, includeSourcePlayer: true);
		else
			await SendPacketAsync(postmanPacket);
	}

	private async Task DismissPostmanAsync(Player player, bool notifyClient = true)
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

		_world?.TryRemoveObject(postman.ObjectId, out _);
		if (_idFactory != null)
			_idFactory.ReleaseId(postman.ObjectId);
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
			var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
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
					await SendPacketAsync(SmInventoryAddItem.CreateBrokerReturn(returnedItem, itemTemplate));
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

		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
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
			await SendPacketAsync(SmInventoryAddItem.CreateItemCollect(returnedItem.ReturnedItem, itemTemplate));
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

		var registrationCommission = CalculateBrokerRegistrationCommission(packet.Price, packet.ItemCount, registeredItemsCount, player.Race);
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

		var itemTemplates = _runtimeContext?.DataManager?.StaticData.ItemTemplates;
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
		await SendPacketAsync(SmInventoryAddItem.CreateBrokerBuy(boughtItem, itemTemplate));
		await SendPacketAsync(SmCubeUpdate.CubeSize(player));

		var sellerSettledPage = await _brokerRepository.LoadSettledItemsAsync(brokerItem.SellerId, player.Race, pageIndex: 0);
		if (_connectionRegistry != null)
			await _connectionRegistry.NotifyBrokerSettledAsync(brokerItem.SellerId, sellerSettledPage.SettledKinah);

		await SendPacketAsync(SmBrokerService.CreateSearchedItems(await LoadCachedBrokerPageAsync(player)));
	}

	private static long CalculateBrokerRegistrationCommission(long price, long count, int registeredItemsCount, string race)
	{
		// Java parity: services/BrokerService.registerItem commission calculation; price modifiers are currently baseline 100/100/100.
		var commission = registeredItemsCount > 9
			? (long)(price * count * 0.04f)
			: (long)(price * count * 0.02f);
		return commission < 10 ? 10 : GetPriceForService(commission, race);
	}

	private static long GetPriceForService(long basePrice, string race)
	{
		// Java parity: services/trade/PricesService.getPriceForService with the currently ported baseline SM_PRICES values.
		return race is "ELYOS" or "ASMODIANS" ? basePrice : basePrice;
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

		var costFactor = sendMail.LetterTypeId == 1 ? 5 : 1;
		var baseCost = sendMail.LetterTypeId == 1 ? 500 : 10;
		var kinahMailCommission = sendMail.KinahCount > 0 ? (long)(sendMail.KinahCount * 0.01d * costFactor) : 0;
		var itemMailCommission = senderItem == null || senderItemTemplate == null
			? 0
			: (long)(senderItemTemplate.Price * GetQualityPriceRate(senderItemTemplate) * sendMail.ItemCount * costFactor);
		var finalMailKinah = baseCost + kinahMailCommission + itemMailCommission + sendMail.KinahCount;
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

	private static double GetQualityPriceRate(ItemTemplateSummary template)
	{
		// Java parity: services/mail/MailService.getQualityPriceRate.
		return template.Quality switch
		{
			"MYTHIC" or "EPIC" => 0.05d,
			"UNIQUE" or "LEGEND" => 0.04d,
			"RARE" => 0.03d,
			_ => 0.02d,
		};
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
		int? colorExpires = null)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
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
		ItemCharge,
		ItemCharge2,
		ManastoneSocket,
		GodstoneSocket,
		SoulBind,
		Decompose,
	}
}
