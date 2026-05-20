using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Aion.GameServer.Configuration;
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
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Network.Aion;

public sealed class GameServerConnection : BaseClientConnection
{
	private const int MaxCorruptPacketsBeforeDisconnect = 3;
	private const int CubeStorageId = 0;
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
	private readonly IDFactory? _idFactory;
	private readonly GameTimeService? _gameTimeService;
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
		IDFactory? idFactory = null,
		GameTimeService? gameTimeService = null,
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
		_idFactory = idFactory;
		_gameTimeService = gameTimeService;
		_crypt = crypt ?? new GameCrypt();
	}

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
			case CmSendMail sendMail:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_SEND_MAIL.runImpl -> MailService.sendMail.
					var mailMessageId = GetSendMailShellResponse(sendMail);
					if (mailMessageId != null)
						await SendPacketAsync(SmMailService.CreateMailMessage(mailMessageId.Value));
				}
				break;
			case CmCheckMailList checkMailList:
				if (_activePlayer != null)
				{
					// Java parity: network/aion/clientpackets/CM_CHECK_MAIL_LIST.runImpl -> MailService.sendMailList.
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
					await SendPacketAsync(SmMailService.CreateDeletePacket(_activePlayer.Mailbox, deleteMail.MailObjectIds));
				}
				break;
			case CmEnterWorld enterWorld:
				var enterWorldResult = _playerEnterWorldService == null
					? new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError)
					: await _playerEnterWorldService.EnterWorldAsync(_accountId, enterWorld.ObjectId);
				if (enterWorldResult.Message == EnterWorldCheckMessage.Ok)
					_state = GameConnectionState.InGame;
				_activePlayer = enterWorldResult.Message == EnterWorldCheckMessage.Ok ? enterWorldResult.Player : null;
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

				player.InventoryItems = player.InventoryItems
					.Concat([CopyInventoryItem(letter.AttachedItem, CubeStorageId, FirstAvailableSlot)])
					.ToArray();
				player.Mailbox = player.Mailbox
					.Select(mail => mail.Id == packet.MailObjectId
						? mail with { AttachedItem = null, AttachedItemObjectId = 0, AttachedItemTemplateId = 0 }
						: mail)
					.ToArray();
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
				await SendPacketAsync(SmMailService.CreateAttachmentState(packet.MailObjectId, packet.AttachmentType));
				break;
		}
	}

	private static int? GetSendMailShellResponse(CmSendMail sendMail)
	{
		// Java parity: services/mail/MailService.sendMail early validation; full recipient lookup/persistence is still deferred.
		if (sendMail.RecipientName.Length > 16 || sendMail.LetterTypeId == 2 || sendMail.KinahCount < 0)
			return null;
		if (sendMail.LetterTypeId is not (0 or 1))
			return null;
		return SmMailService.NoSuchCharacterName;
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
		long? count = null)
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
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
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
			PackCount = item.PackCount,
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
