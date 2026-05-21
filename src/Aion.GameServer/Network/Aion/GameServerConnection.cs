using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Aion.GameServer.Configuration;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
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
	private const int MaxBlockedUsers = 100;
	private static readonly TimeSpan ClientPingInterval = TimeSpan.FromMilliseconds(180000);
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
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly IDFactory? _idFactory;
	private readonly GameTimeService? _gameTimeService;
	private readonly GameWorld? _world;
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
	private int _corruptPackets;
	private DateTimeOffset? _lastPingTime;

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
		IGameClientConnectionRegistry? connectionRegistry = null,
		IDFactory? idFactory = null,
		GameTimeService? gameTimeService = null,
		GameWorld? world = null,
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
		_connectionRegistry = connectionRegistry;
		_idFactory = idFactory;
		_gameTimeService = gameTimeService;
		_world = world;
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
			case CmMove move:
				if (_activePlayer != null)
					await HandleMoveAsync(_activePlayer, move);
				break;
			case CmMoveInAir moveInAir:
				if (_activePlayer != null)
					HandleMoveInAir(_activePlayer, moveInAir);
				break;
			case CmQuestionResponse questionResponse:
				if (_activePlayer != null)
					await HandleQuestionResponseAsync(_activePlayer, questionResponse);
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
			case CmBlockAdd blockAdd:
				if (_activePlayer != null)
					await HandleBlockAddAsync(_activePlayer, blockAdd);
				break;
			case CmChatAuth chatAuth:
				if (_activePlayer != null)
					await HandleChatAuthAsync(_activePlayer, chatAuth);
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
			case CmShowFriendList:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_SHOW_FRIENDLIST.runImpl -> SM_FRIEND_LIST.
					var staticData = _runtimeContext?.DataManager?.StaticData;
					await SendPacketAsync(new SmFriendList(_activePlayer.Friends, staticData?.PlayerExperienceTable));
				}
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
					_connectionRegistry?.RegisterPlayerConnection(_activePlayer.ObjectId, this);
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
					await SendPacketAsync(new SmChannelInfo(enterWorldResult.Player.Position, staticData?.WorldMaps ?? Array.Empty<WorldMapSummary>()));
					await SendPacketAsync(CreateBindPointPacket(enterWorldResult.Player, staticData));
					await SendPacketAsync(new SmPlayerSpawn(enterWorldResult.Player));
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
					await SendPacketAsync(new SmStatsInfo(enterWorldResult.Player, staticData?.PlayerExperienceTable, _gameTimeService?.GameMinutes ?? 0));
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

					// Java parity: services/HousingService.onPlayerLogin sends house owner profile info.
					await SendPacketAsync(new SmHouseOwnerInfo(enterWorldResult.Player));
				}
				break;
		}
	}

	private async Task HandleLevelReadyAsync(Player player)
	{
		// Java parity: network/aion/clientpackets/CM_LEVEL_READY.runImpl baseline packets after client map load.
		var staticData = _runtimeContext?.DataManager?.StaticData;
		await SendPacketAsync(new SmPlayerInfo(player, staticData?.PlayerExperienceTable));
		await SendPacketAsync(CreateAccountPropertiesPacket());
		await SendPacketAsync(new SmMotion(player.ObjectId, player.Motions));
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

	private async Task HandleQuestionResponseAsync(Player responder, CmQuestionResponse packet)
	{
		// Java parity: network/aion/clientpackets/CM_QUESTION_RESPONSE.runImpl for buddy-list request handlers.
		if (packet.QuestionId != SmQuestionWindow.BuddyListAddBuddyRequest)
			return;

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
		return new PlayerFriend(
			player.ObjectId,
			player.Name,
			player.Exp,
			player.PlayerClass,
			player.Gender,
			player.Position.WorldId,
			player.FriendListStatus == 0 ? player.LastOnline : null,
			string.Empty,
			string.Empty,
			player.FriendListStatus != 0 || player.IsOnline);
	}

	private static PlayerFriend? UpdateFriendSnapshot(Player friendPlayer, Player activePlayer, byte activeStatus)
	{
		// Java parity: friendPlayer.getFriendList().getFriend(activePlayerId).setPCD(activePlayer.getCommonData()).
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
					IsOnline = activeStatus != 0,
				};
				return updatedFriend;
			})
			.ToArray();

		return updatedFriend;
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
		await DismissPostmanAsync(player, notifyClient: notifyPostmanClient);
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

	private async Task HandleMoveAsync(Player player, CmMove packet)
	{
		// Java parity: network/aion/clientpackets/CM_MOVE.runImpl movement-state updates before World.updatePosition.
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

		if (_connectionRegistry != null && (MovementMask.HasManualPosition(packet.Type) || packet.Type == MovementMask.Immediate))
			await _connectionRegistry.BroadcastToVisiblePlayersAsync(player.Position, player.ObjectId, new SmMove(player));
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
		// TODO Phase 6: replace this object-id guard with real NPC template function + KnownList visibility validation.
		if (brokerObjectId > 0 && player.TargetObjectId == brokerObjectId)
			return true;

		_logger.LogWarning(
			"Player {PlayerObjectId} tried to {Action} without targeting broker {BrokerObjectId}; current target is {TargetObjectId}",
			player.ObjectId,
			action,
			brokerObjectId,
			player.TargetObjectId);
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

		if (!IsHouseAuctionBiddingTime())
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

	private static bool IsHouseAuctionBiddingTime()
	{
		// Java parity: HousingBidService.isBiddingTime default Sunday-noon cutoff; prolongation state is not ported yet.
		var now = DateTime.Now;
		return now.DayOfWeek != DayOfWeek.Sunday || now.Hour < 12;
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
		// Java parity: services/item/ItemFactory.newItem(itemId, count) default item state and count clamp.
		var expireTime = itemTemplate.ExpireTimeMinutes == 0
			? 0
			: (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + itemTemplate.ExpireTimeMinutes * 60 - 1;

		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemTemplate.TemplateId,
			Count = CalculateNewItemCount(itemTemplate, count),
			OwnerId = ownerId,
			Location = location,
			Slot = slot,
			ExpireTime = expireTime,
			ActivationCount = itemTemplate.ActivationCount,
			TuneCount = itemTemplate.CanTune ? -1 : 0,
			IsAmplified = itemTemplate.EnchantType == 1,
		};
	}

	private static long CalculateNewItemCount(ItemTemplateSummary itemTemplate, long count)
	{
		// Java parity: ItemFactory.calculateCount exempts kinah from max-stack clamping.
		return count > itemTemplate.MaxStackCount && itemTemplate.TemplateId != KinahItemId
			? itemTemplate.MaxStackCount
			: count;
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
		int? packCount = null)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count ?? item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
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
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
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
}
