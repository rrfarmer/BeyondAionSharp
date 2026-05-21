using System.Globalization;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class CharacterCreationService
{
	private const int CubeStorageId = 0;
	private readonly GameServerOptions _options;
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly ICharacterSelectionRepository _selectionRepository;
	private readonly ICharacterCreationRepository _creationRepository;
	private readonly IDFactory _idFactory;
	private readonly ILogger<CharacterCreationService> _logger;

	public CharacterCreationService(
		GameServerOptions options,
		GameServerRuntimeContext runtimeContext,
		ICharacterSelectionRepository selectionRepository,
		ICharacterCreationRepository creationRepository,
		IDFactory idFactory,
		ILogger<CharacterCreationService> logger)
	{
		_options = options;
		_runtimeContext = runtimeContext;
		_selectionRepository = selectionRepository;
		_creationRepository = creationRepository;
		_idFactory = idFactory;
		_logger = logger;
	}

	public async Task<CharacterCreationResult> CreateCharacterAsync(
		CmCreateCharacter request,
		int accountId,
		string accountName,
		byte membership,
		CancellationToken cancellationToken = default)
	{
		// Java parity: network/aion/clientpackets/CM_CREATE_CHARACTER.runImpl.
		if (request.Type == 1)
			return new CharacterCreationResult(SmCreateCharacter.ResponseOpenCreationWindow);
		if (accountId == 0)
			return new CharacterCreationResult(SmCreateCharacter.ResponseDbError);

		var normalizedName = NormalizeName(request.CharacterName);
		var existingCharacters = await _selectionRepository.LoadCharactersAsync(accountId, cancellationToken);
		var validation = await ValidateBasicInfoAsync(normalizedName, request, existingCharacters, membership, cancellationToken);
		if (validation != SmCreateCharacter.ResponseOk)
			return new CharacterCreationResult(validation);

		var staticData = _runtimeContext.DataManager?.StaticData;
		var playerClass = ToClassName(request.ClassId);
		var race = ToRaceName(request.RaceId);
		var gender = ToGenderName(request.GenderId);
		if (staticData == null || playerClass == null || race == null || gender == null)
			return new CharacterCreationResult(SmCreateCharacter.ResponseDbError);

		var spawn = staticData.PlayerInitialData.GetSpawnLocation(race);
		if (spawn == null)
			return new CharacterCreationResult(SmCreateCharacter.ResponseDbError);

		var allocatedIds = new List<int>();
		var playerId = AllocateId(allocatedIds);
		var creationDate = DateTime.Now;
		var startingItems = BuildStartingItems(staticData, playerClass, allocatedIds);
		var startingSkills = BuildStartingSkills(staticData, playerClass, race);
		var visibleItems = BuildVisibleItems(startingItems);
		var character = new CharacterSelectionEntry
		{
			ObjectId = playerId,
			Name = normalizedName,
			GenderId = request.GenderId,
			RaceId = request.RaceId,
			ClassId = request.ClassId,
			Appearance = request.Appearance,
			TemplateId = 100000 + request.RaceId * 2 + request.GenderId,
			MapId = spawn.MapId,
			X = spawn.X,
			Y = spawn.Y,
			Z = spawn.Z,
			Heading = spawn.Heading,
			Level = 1,
			TitleId = -1,
			VisibleItems = visibleItems,
		};
		var record = new NewCharacterRecord(character, accountName, gender, race, playerClass, creationDate, startingItems, startingSkills);

		if (await _creationRepository.StoreNewCharacterAsync(accountId, record, cancellationToken))
			return new CharacterCreationResult(SmCreateCharacter.ResponseOk, character);

		foreach (var id in allocatedIds)
			_idFactory.ReleaseId(id);
		return new CharacterCreationResult(SmCreateCharacter.ResponseDbError);
	}

	public async Task<int> CheckNicknameAsync(string nickname, CancellationToken cancellationToken = default)
	{
		// Java parity: network/aion/clientpackets/CM_CHECK_NICKNAME.runImpl.
		var normalizedName = NormalizeName(nickname);
		if (await _creationRepository.IsNameUsedOrReservedAsync(null, normalizedName, _options.Names.ReserveOldNameDays, cancellationToken))
		{
			return _options.Core.CharacterCreationMode == 2
				? SmCreateCharacter.ResponseNameReserved
				: SmCreateCharacter.ResponseNameAlreadyUsed;
		}

		if (!IsValidName(normalizedName))
			return SmCreateCharacter.ResponseInvalidName;
		if (IsForbiddenName(normalizedName))
			return SmCreateCharacter.ResponseForbiddenCharacterName;

		return SmCreateCharacter.ResponseOk;
	}

	private async Task<int> ValidateBasicInfoAsync(
		string normalizedName,
		CmCreateCharacter request,
		IReadOnlyList<CharacterSelectionEntry> existingCharacters,
		byte membership,
		CancellationToken cancellationToken)
	{
		// Java parity: CM_CREATE_CHARACTER.validateBasicInfo.
		var maxCharacterCount = GetMaxCharacterCount(membership);
		if (existingCharacters.Count > maxCharacterCount)
			return SmCreateCharacter.ResponseServerLimitExceeded;
		var playerClass = ToClassName(request.ClassId);
		if (playerClass == null)
			return SmCreateCharacter.FailedToCreateCharacter;
		if (await _creationRepository.IsNameUsedOrReservedAsync(null, normalizedName, _options.Names.ReserveOldNameDays, cancellationToken))
		{
			return _options.Core.CharacterCreationMode == 2
				? SmCreateCharacter.ResponseNameReserved
				: SmCreateCharacter.ResponseNameAlreadyUsed;
		}
		if (!IsValidName(normalizedName))
			return SmCreateCharacter.ResponseInvalidName;
		if (IsForbiddenName(normalizedName))
			return SmCreateCharacter.ResponseForbiddenCharacterName;
		if (!IsStartingClass(playerClass))
			return SmCreateCharacter.ResponseForbiddenClass;
		if (_options.Core.CharacterCreationMode == 0 && existingCharacters.Any(character => character.RaceId != request.RaceId))
			return SmCreateCharacter.ResponseOtherRace;

		return SmCreateCharacter.ResponseOk;
	}

	private int GetMaxCharacterCount(byte membership)
	{
		// Java parity: MembershipConfig.CHARACTER_ADDITIONAL_* in CM_CREATE_CHARACTER.validateBasicInfo.
		return membership >= _options.Membership.CharacterAdditionalEnable
			? _options.Membership.CharacterAdditionalCount
			: _options.Core.CharacterLimitCount;
	}

	private string NormalizeName(string name)
	{
		// Java parity: com.aionemu.gameserver.utils.Util.convertName via AbstractCharacterEditPacket.readBasicInfo.
		if (string.IsNullOrEmpty(name) || _options.Names.AllowCustomNames)
			return name;

		return name[..1].ToUpper(CultureInfo.InvariantCulture) + name[1..].ToLower(CultureInfo.InvariantCulture);
	}

	private bool IsValidName(string name)
	{
		// Java parity: services/NameRestrictionService.isValidName.
		try
		{
			return _options.Names.CreateCharacterNameRegex().IsMatch(name);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Invalid character name regex {Pattern}", _options.Names.CharacterPattern);
			return false;
		}
	}

	private bool IsForbiddenName(string name)
	{
		// Java parity: services/NameRestrictionService.isForbidden.
		try
		{
			var forbiddenSequence = _options.Names.CreateForbiddenSequenceRegex();
			if (forbiddenSequence != null && forbiddenSequence.IsMatch(name))
				return true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Invalid forbidden sequence regex {Pattern}", _options.Names.ForbiddenSequencePattern);
		}

		return _options.Names.ForbiddenWords.Any(word => string.Equals(word, name, StringComparison.OrdinalIgnoreCase));
	}

	private IReadOnlyList<NewCharacterItem> BuildStartingItems(StaticData staticData, string playerClass, List<int> allocatedIds)
	{
		// Java parity: services/player/PlayerService.newPlayer starting item/equipment loop.
		var creationData = staticData.PlayerInitialData.GetPlayerCreationData(playerClass);
		if (creationData == null)
			return Array.Empty<NewCharacterItem>();

		var equippedSlots = 0L;
		var items = new List<NewCharacterItem>();
		foreach (var startingItem in creationData.Items)
		{
			var template = staticData.ItemTemplates.GetItemTemplate(startingItem.ItemId);
			var isEquipped = false;
			var equipmentSlot = 0L;
			if (template?.IsEquipment == true && (equippedSlots & template.ValidEquipmentSlots) == 0)
			{
				isEquipped = true;
				equipmentSlot = GetFirstSlot(template.ValidEquipmentSlots);
				equippedSlots |= equipmentSlot;
			}

			items.Add(
				new NewCharacterItem(
					AllocateId(allocatedIds),
					startingItem.ItemId,
					startingItem.Count,
					isEquipped,
					equipmentSlot,
					CubeStorageId,
					startingItem.ItemId));
		}

		return items;
	}

	private static IReadOnlyList<NewCharacterSkill> BuildStartingSkills(StaticData staticData, string playerClass, string race)
	{
		// Java parity: services/SkillLearnService.learnNewSkills during PlayerService.newPlayer.
		return staticData.SkillTree.GetAutoLearnSkills(playerClass, race, fromLevel: 1, toLevel: 1)
			.Where(template => template.SkillLevel > 0)
			.Select(template => new NewCharacterSkill(template.SkillId, template.SkillLevel))
			.ToArray();
	}

	private int AllocateId(List<int> allocatedIds)
	{
		// Java parity: utils/idfactory/IDFactory.nextId.
		var id = _idFactory.NextId();
		allocatedIds.Add(id);
		return id;
	}

	private static IReadOnlyList<VisibleCharacterItem> BuildVisibleItems(IEnumerable<NewCharacterItem> startingItems)
	{
		// Java parity: PlayerAccountData.setVisibleItems(player.getEquipment().getEquippedForAppearance()).
		return startingItems
			.Where(item => item.IsEquipped)
			.Select(item => new VisibleCharacterItem(GetEquipmentSlotType(item.EquipmentSlot), item.ItemSkin, GodStoneId: 0, Color: null))
			.Where(item => item.SlotType != 0)
			.ToArray();
	}

	private static long GetFirstSlot(long slotMask)
	{
		// Java parity: model/items/ItemSlot.getPossibleSlots first available equipment slot.
		foreach (var slot in EquipmentSlotsInJavaOrder)
		{
			if ((slotMask & slot) == slot)
				return slot;
		}

		return 0;
	}

	private static byte GetEquipmentSlotType(long slot)
	{
		// Java parity: model/items/ItemSlot.getEquipmentSlotType.
		if ((VisibleSlotMask & slot) != slot)
			return 0;

		return (slot & LeftSlotMask) == 0 || IsTwoHandedWeapon(slot) ? (byte)1 : (byte)2;
	}

	private static bool IsTwoHandedWeapon(long slot)
	{
		return (slot & MainOrSubMask) == MainOrSubMask || (slot & MainOffOrSubOffMask) == MainOffOrSubOffMask;
	}

	private static bool IsStartingClass(string playerClass)
	{
		// Java parity: model/gameobjects/player/PlayerClass.isStartingClass.
		return playerClass is "WARRIOR" or "SCOUT" or "MAGE" or "PRIEST" or "ENGINEER" or "ARTIST";
	}

	private static string? ToClassName(int classId)
	{
		// Java parity: model/gameobjects/player/PlayerClass enum ordinal ids.
		return classId switch
		{
			0 => "WARRIOR",
			1 => "GLADIATOR",
			2 => "TEMPLAR",
			3 => "SCOUT",
			4 => "ASSASSIN",
			5 => "RANGER",
			6 => "MAGE",
			7 => "SORCERER",
			8 => "SPIRIT_MASTER",
			9 => "PRIEST",
			10 => "CLERIC",
			11 => "CHANTER",
			12 => "ENGINEER",
			13 => "RIDER",
			14 => "GUNNER",
			15 => "ARTIST",
			16 => "BARD",
			_ => null,
		};
	}

	private static string? ToRaceName(int raceId)
	{
		// Java parity: model/Race client ids used by AbstractCharacterEditPacket.
		return raceId switch
		{
			0 => "ELYOS",
			1 => "ASMODIANS",
			_ => null,
		};
	}

	private static string? ToGenderName(int genderId)
	{
		// Java parity: model/Gender client ids used by AbstractCharacterEditPacket.
		return genderId switch
		{
			0 => "MALE",
			1 => "FEMALE",
			_ => null,
		};
	}

	private static readonly long[] EquipmentSlotsInJavaOrder =
	[
		MainHand,
		SubHand,
		Helmet,
		Torso,
		Gloves,
		Boots,
		EarringsLeft,
		EarringsRight,
		RingLeft,
		RingRight,
		Necklace,
		Shoulder,
		Pants,
		PowerShardRight,
		PowerShardLeft,
		Wings,
		Waist,
		MainOffHand,
		SubOffHand,
		Plume,
	];

	private const long MainHand = 1L;
	private const long SubHand = 1L << 1;
	private const long Helmet = 1L << 2;
	private const long Torso = 1L << 3;
	private const long Gloves = 1L << 4;
	private const long Boots = 1L << 5;
	private const long EarringsLeft = 1L << 6;
	private const long EarringsRight = 1L << 7;
	private const long RingLeft = 1L << 8;
	private const long RingRight = 1L << 9;
	private const long Necklace = 1L << 10;
	private const long Shoulder = 1L << 11;
	private const long Pants = 1L << 12;
	private const long PowerShardRight = 1L << 13;
	private const long PowerShardLeft = 1L << 14;
	private const long Wings = 1L << 15;
	private const long Waist = 1L << 16;
	private const long MainOffHand = 1L << 17;
	private const long SubOffHand = 1L << 18;
	private const long Plume = 1L << 19;
	private const long MainOrSubMask = MainHand | SubHand;
	private const long MainOffOrSubOffMask = MainOffHand | SubOffHand;
	private const long LeftSlotMask = SubHand | EarringsLeft | PowerShardLeft | SubOffHand;
	private const long VisibleSlotMask = MainHand | SubHand | Helmet | Torso | Gloves | Boots | EarringsLeft | EarringsRight | Necklace | Shoulder | Pants
		| PowerShardRight | PowerShardLeft | Wings | Plume;
}
