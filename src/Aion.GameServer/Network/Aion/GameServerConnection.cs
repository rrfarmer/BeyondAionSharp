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
	private readonly GamePacketProcessor<string> _packetProcessor;
	private readonly GameCrypt _crypt;
	private readonly GameServerOptions _options;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly GameLoginServer? _loginServer;
	private readonly ICharacterSelectionRepository _characterSelectionRepository;
	private readonly CharacterCreationService? _characterCreationService;
	private readonly PlayerEnterWorldService? _playerEnterWorldService;
	private readonly IMailRepository? _mailRepository;
	private readonly IBrokerRepository? _brokerRepository;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly IDFactory? _idFactory;
	private readonly GameTimeService? _gameTimeService;
	private readonly GameWorld? _world;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly SemaphoreSlim _closeLock = new(1, 1);
	private GameConnectionState _state = GameConnectionState.Connected;
	private int _accountId;
	private string _accountName = string.Empty;
	private byte _membership;
	private Player? _activePlayer;
	private bool _accountDisconnectNotified;
	private string _macAddress = string.Empty;
	private string _hddSerial = string.Empty;
	private int _corruptPackets;

	public GameServerConnection(
		ILogger logger,
		TcpClient client,
		string clientId,
		GamePacketProcessor<string> packetProcessor,
		GameServerOptions? options = null,
		GameServerRuntimeContext? runtimeContext = null,
		GameLoginServer? loginServer = null,
		ICharacterSelectionRepository? characterSelectionRepository = null,
		CharacterCreationService? characterCreationService = null,
		PlayerEnterWorldService? playerEnterWorldService = null,
		IMailRepository? mailRepository = null,
		IBrokerRepository? brokerRepository = null,
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
		_characterSelectionRepository = characterSelectionRepository ?? new EmptyCharacterSelectionRepository();
		_characterCreationService = characterCreationService;
		_playerEnterWorldService = playerEnterWorldService;
		_mailRepository = mailRepository;
		_brokerRepository = brokerRepository;
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
			if (_activePlayer != null)
			{
				await DismissPostmanAsync(_activePlayer, notifyClient: false);
				_connectionRegistry?.UnregisterPlayerConnection(_activePlayer.ObjectId, this);
				if (_playerEnterWorldService != null)
					await _playerEnterWorldService.LeaveWorldAsync(_activePlayer);
				else
					_world?.TryRemoveObject(_activePlayer.ObjectId, out _);
				_activePlayer = null;
			}

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
					_membership = authResult.Membership;
					_activePlayer = null;
					_accountDisconnectNotified = false;
				}
				else
				{
					_accountId = 0;
					_accountName = string.Empty;
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
			case CmMayLoginIntoGame:
				await SendPacketAsync(new SmMayLoginIntoGame());
				break;
			case CmMove move:
				if (_activePlayer != null)
					await HandleMoveAsync(_activePlayer, move);
				break;
			case CmMoveInAir moveInAir:
				if (_activePlayer != null)
					HandleMoveInAir(_activePlayer, moveInAir);
				break;
			case CmCharacterList characterList:
				await SendPacketAsync(new SmAccountProperties());
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
			case CmCharacterPasskey characterPasskey:
				await SendPacketAsync(new SmCharacterSelect(type: 2, messageType: characterPasskey.Type, wrongCount: 0));
				break;
			case CmBrokerSellWindow brokerSellWindow:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_BROKER_SELL_WINDOW.runImpl -> BrokerService.showSellWindow.
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
			case CmBrokerRegistered:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_BROKER_REGISTERED.runImpl -> BrokerService.showRegisteredItems.
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
					var page = _brokerRepository == null
						? new PlayerBrokerItemPage(Array.Empty<PlayerBrokerItem>(), 0, brokerSettleList.StartPageIndex, _activePlayer.BrokerSettlements.EarnedKinah)
						: await _brokerRepository.LoadSettledItemsAsync(_activePlayer.ObjectId, _activePlayer.Race, brokerSettleList.StartPageIndex);
					await SendPacketAsync(SmBrokerService.CreateSettledItems(page));
				}
				break;
			case CmBrokerCancelRegistered:
				if (_activePlayer != null)
					await HandleBrokerCancelRegisteredAsync(_activePlayer, (CmBrokerCancelRegistered)packet);
				break;
			case CmBrokerSettleAccount:
				if (_activePlayer != null)
					await HandleBrokerSettleAccountAsync(_activePlayer);
				break;
			case CmRegisterBrokerItem:
				if (_activePlayer != null)
					await HandleBrokerRegisterItemAsync(_activePlayer, (CmRegisterBrokerItem)packet);
				break;
			case CmBuyBrokerItem:
				if (_activePlayer != null)
					await HandleBuyBrokerItemAsync(_activePlayer, (CmBuyBrokerItem)packet);
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
			case CmEnterWorld enterWorld:
				var enterWorldResult = _playerEnterWorldService == null
					? new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError)
					: await _playerEnterWorldService.EnterWorldAsync(_accountId, enterWorld.ObjectId);
				if (enterWorldResult.Message == EnterWorldCheckMessage.Ok)
					_state = GameConnectionState.InGame;
				_activePlayer = enterWorldResult.Message == EnterWorldCheckMessage.Ok ? enterWorldResult.Player : null;
				if (_activePlayer != null)
					_connectionRegistry?.RegisterPlayerConnection(_activePlayer.ObjectId, this);
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

	private async Task HandleBrokerCancelRegisteredAsync(Player player, CmBrokerCancelRegistered packet)
	{
		// Java parity: network/aion/clientpackets/CM_BROKER_CANCEL_REGISTERED.runImpl -> BrokerService.cancelRegisteredItem.
		if (_brokerRepository == null)
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
			brokerStorageItem = new InventoryItem
			{
				ObjectId = objectId,
				ItemId = sourceItem.ItemId,
				Count = packet.ItemCount,
				OwnerId = player.ObjectId,
				Location = BrokerStorageId,
				Slot = FirstAvailableSlot,
			};
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
			boughtItem = new InventoryItem
			{
				ObjectId = objectId,
				ItemId = brokerItem.ItemId,
				Count = packet.ItemCount,
				OwnerId = player.ObjectId,
				Location = CubeStorageId,
				Slot = FirstAvailableSlot,
			};
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
		var attachedItem = BuildMailAttachment(senderItem, recipient.PlayerObjectId, sendMail.ItemCount);
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

	private InventoryItem? BuildMailAttachment(InventoryItem? senderItem, int recipientObjectId, long attachedCount)
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

		var objectId = _idFactory?.NextId() ?? 0;
		return objectId == 0
			? null
			: new InventoryItem
			{
				ObjectId = objectId,
				ItemId = senderItem.ItemId,
				Count = attachedCount,
				OwnerId = recipientObjectId,
				Slot = FirstAvailableSlot,
				Location = MailboxStorageId,
			};
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
