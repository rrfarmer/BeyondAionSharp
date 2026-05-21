using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class EquipmentService
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const long MainHand = 1L;
	private const long SubHand = 1L << 1;
	private const long MainOffHand = 1L << 17;
	private const long SubOffHand = 1L << 18;
	private const long MainOrSub = MainHand | SubHand;
	private const long MainOffOrSubOff = MainOffHand | SubOffHand;
	private const long RightHand = MainHand | MainOffHand;
	private const long LeftHand = SubHand | SubOffHand;

	public static EquipmentChangeResult ChangeEquipment(
		Player player,
		byte action,
		long slotRead,
		int itemObjectId,
		ItemTemplateTable itemTemplates,
		SkillTemplateTable? skillTemplates,
		PlayerExperienceTable? experienceTable = null,
		bool soulBindConfirmed = false,
		SkillTreeTable? skillTree = null)
	{
		// Java parity: network/aion/clientpackets/CM_EQUIP_ITEM.runImpl action routing.
		return action switch
		{
			0 => EquipItem(player, slotRead, itemObjectId, itemTemplates, skillTemplates, experienceTable, soulBindConfirmed, skillTree),
			1 => UnEquipItem(player, itemObjectId, itemTemplates, skillTemplates, skillTree),
			2 => SwitchHands(player, itemTemplates),
			_ => EquipmentChangeResult.NoChange(),
		};
	}

	private static EquipmentChangeResult EquipItem(
		Player player,
		long slotRead,
		int itemObjectId,
		ItemTemplateTable itemTemplates,
		SkillTemplateTable? skillTemplates,
		PlayerExperienceTable? experienceTable,
		bool soulBindConfirmed,
		SkillTreeTable? skillTree)
	{
		// Java parity: model/gameobjects/player/Equipment.equipItem.
		var inventoryItems = player.InventoryItems.ToList();
		var item = inventoryItems.FirstOrDefault(candidate =>
			candidate.ObjectId == itemObjectId
			&& candidate.Location == CubeStorageId
			&& !candidate.IsEquipped);
		if (item == null)
			return EquipmentChangeResult.NoChange();

		var template = itemTemplates.GetItemTemplate(item.ItemId);
		if (template is not { IsEquipment: true })
			return EquipmentChangeResult.NoChange();

		var slot = template.IsTwoHandWeapon
			? MainOrSub
			: IsOneHandWeapon(template) && !HasDualWieldEffect(player, skillTemplates)
				? MainHand
				: slotRead;

		var validationFailure = ValidateEquipRestrictions(player, template, experienceTable);
		if (validationFailure != null)
			return validationFailure;

		if (!CanEquipIntoSlot(player, inventoryItems, itemTemplates, template, slot, out var inventoryFull))
			return inventoryFull ? EquipmentChangeResult.FullInventoryFailure() : EquipmentChangeResult.NoChange();

		if (!HasRequiredEquipSkill(player, template))
			return EquipmentChangeResult.MissingRequiredSkillFailure();

		if (!item.IsIdentified)
			return EquipmentChangeResult.UnidentifiedItemFailure();

		var addedSkills = Array.Empty<PlayerSkill>();
		var removedSkills = Array.Empty<PlayerSkill>();
		var removedSkillNames = Array.Empty<string>();
		IReadOnlyList<PlayerSkill>? updatedSkills = null;
		InventoryItem? kinahItemUpdate = null;
		if (template.StigmaInfo != null && skillTemplates != null && skillTree != null)
		{
			var stigma = StigmaService.NotifyEquipAction(
				player,
				item,
				template,
				slot,
				inventoryItems,
				itemTemplates,
				skillTemplates,
				skillTree,
				experienceTable);
			if (!stigma.Allowed)
				return stigma.Failure == StigmaEquipFailure.NotEnoughKinah
					? EquipmentChangeResult.StigmaNotEnoughKinahFailure()
					: EquipmentChangeResult.StigmaDeniedFailure();

			updatedSkills = stigma.Skills;
			addedSkills = stigma.AddedSkills.ToArray();
			removedSkills = stigma.RemovedSkills.ToArray();
			removedSkillNames = stigma.RemovedSkillNames.ToArray();
			kinahItemUpdate = stigma.KinahItemUpdate;
			if (kinahItemUpdate != null)
				ReplaceInventoryItem(inventoryItems, kinahItemUpdate);
		}

		if (template.IsSoulBound && !item.IsSoulBound)
		{
			if (!soulBindConfirmed)
				return EquipmentChangeResult.SoulBindRequiredFailure(item.ObjectId, slot, GetItemName(template));

			item = CopyInventoryItem(item, isSoulBound: true);
			ReplaceInventoryItem(inventoryItems, item);
		}

		var updates = new List<InventoryItem>();
		var persisted = new List<InventoryItem>();
		foreach (var unequipped in UnEquipSlots(inventoryItems, GetUnequipSlots(inventoryItems, slot, itemTemplates)))
		{
			updates.Add(unequipped);
			persisted.Add(unequipped);
		}

		var equippedItem = CopyInventoryItem(item, slot: slot, isEquipped: true);
		ReplaceInventoryItem(inventoryItems, equippedItem);
		updates.Add(equippedItem);
		persisted.Add(equippedItem);

		return EquipmentChangeResult.Success(
			inventoryItems,
			persisted,
			updates,
			kinahItemUpdate,
			updatedSkills,
			addedSkills,
			removedSkills,
			removedSkillNames);
	}

	private static EquipmentChangeResult? ValidateEquipRestrictions(
		Player player,
		ItemTemplateSummary template,
		PlayerExperienceTable? experienceTable)
	{
		// Java parity: model/gameobjects/player/Equipment.equipItem validation before inventory slot mutation.
		if (!template.IsClassSpecific(player.PlayerClass))
			return EquipmentChangeResult.InvalidClassFailure();

		var playerLevel = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
		var requiredLevel = template.GetRequiredLevel(player.PlayerClass);
		if (requiredLevel == -1 || requiredLevel > playerLevel)
			return EquipmentChangeResult.TooLowLevelFailure(GetItemName(template), requiredLevel);

		var maxLevel = template.GetMaxLevelRestrict(player.PlayerClass);
		if (maxLevel != 0 && playerLevel > maxLevel)
			return EquipmentChangeResult.TooHighLevelFailure(GetItemName(template), maxLevel);

		if (!template.IsRacePermitted(player.Race))
			return EquipmentChangeResult.InvalidRaceFailure();

		if (!template.IsGenderPermitted(player.Gender))
			return EquipmentChangeResult.InvalidGenderFailure();

		if (!template.VerifyRank(player.AbyssRank.Rank))
			return EquipmentChangeResult.InvalidRankFailure(PlayerAbyssRank.GetRankL10n(player.Race, template.MinRank));

		return null;
	}

	private static bool HasRequiredEquipSkill(Player player, ItemTemplateSummary template)
	{
		// Java parity: model/gameobjects/player/Equipment.checkAvailableEquipSkills.
		var requiredSkills = template.RequiredEquipSkills;
		return requiredSkills.Count == 0
			|| requiredSkills.Any(requiredSkill => player.Skills.Any(skill => skill.SkillId == requiredSkill));
	}

	private static EquipmentChangeResult UnEquipItem(
		Player player,
		int itemObjectId,
		ItemTemplateTable itemTemplates,
		SkillTemplateTable? skillTemplates,
		SkillTreeTable? skillTree)
	{
		// Java parity: model/gameobjects/player/Equipment.unEquipItem(int, boolean).
		if (InventoryCapacity.GetFreeCubeSlots(player) <= 0)
			return EquipmentChangeResult.FullInventoryFailure();

		var inventoryItems = player.InventoryItems.ToList();
		var item = GetEquippedItemByObjectId(inventoryItems, itemObjectId);
		if (item == null)
			return EquipmentChangeResult.FullInventoryFailure();

		var itemTemplate = itemTemplates.GetItemTemplate(item.ItemId);
		IReadOnlyList<PlayerSkill>? updatedSkills = null;
		var removedSkills = Array.Empty<PlayerSkill>();
		var removedSkillNames = Array.Empty<string>();
		if (itemTemplate?.StigmaInfo != null && skillTemplates != null && skillTree != null)
		{
			var stigma = StigmaService.NotifyUnequipAction(player, item, itemTemplate, skillTemplates, skillTree);
			updatedSkills = stigma.Skills;
			removedSkills = stigma.RemovedSkills.ToArray();
			removedSkillNames = stigma.RemovedSkillNames.ToArray();
		}

		var slotsToUnequip = item.Slot;
		if (item.Slot == MainHand)
		{
			var offHand = GetEquippedItemBySlot(inventoryItems, SubHand);
			if (offHand != null
				&& offHand.ObjectId != item.ObjectId
				&& itemTemplates.GetItemTemplate(offHand.ItemId) is { IsWeapon: true })
			{
				if (InventoryCapacity.GetFreeCubeSlots(player) < 2)
					return EquipmentChangeResult.FullInventoryFailure();
				slotsToUnequip |= SubHand;
			}
		}

		var updates = UnEquipSlots(inventoryItems, slotsToUnequip).ToArray();
		if (updates.Length == 0)
			return EquipmentChangeResult.FullInventoryFailure();

		return EquipmentChangeResult.Success(
			inventoryItems,
			updates,
			updates,
			kinahItemUpdate: null,
			skills: updatedSkills,
			addedSkills: Array.Empty<PlayerSkill>(),
			removedSkills: removedSkills,
			removedStigmaSkillNames: removedSkillNames);
	}

	private static EquipmentChangeResult SwitchHands(Player player, ItemTemplateTable itemTemplates)
	{
		// Java parity: model/gameobjects/player/Equipment.switchHands.
		var inventoryItems = player.InventoryItems.ToList();
		var equippedWeapons = new List<InventoryItem>();
		AddDistinct(equippedWeapons, GetEquippedItemBySlot(inventoryItems, MainHand));
		AddDistinct(equippedWeapons, GetEquippedItemBySlot(inventoryItems, SubHand));
		AddDistinct(equippedWeapons, GetEquippedItemBySlot(inventoryItems, MainOffHand));
		AddDistinct(equippedWeapons, GetEquippedItemBySlot(inventoryItems, SubOffHand));

		equippedWeapons = equippedWeapons
			.Where(item => itemTemplates.GetItemTemplate(item.ItemId) is { IsWeapon: true })
			.ToList();
		if (equippedWeapons.Count == 0)
			return EquipmentChangeResult.Success(inventoryItems, Array.Empty<InventoryItem>(), Array.Empty<InventoryItem>());

		var updates = new List<InventoryItem>();
		foreach (var item in equippedWeapons)
		{
			var unequipped = CopyInventoryItem(item, isEquipped: false);
			updates.Add(unequipped);
		}

		var persisted = new List<InventoryItem>();
		foreach (var item in equippedWeapons)
		{
			var switchedSlot = SwitchHandBits(item.Slot);
			var switched = CopyInventoryItem(item, slot: switchedSlot, isEquipped: true);
			ReplaceInventoryItem(inventoryItems, switched);
			updates.Add(switched);
			persisted.Add(switched);
		}

		return EquipmentChangeResult.Success(inventoryItems, persisted, updates);
	}

	private static bool CanEquipIntoSlot(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		ItemTemplateTable itemTemplates,
		ItemTemplateSummary template,
		long slot,
		out bool inventoryFull)
	{
		inventoryFull = false;
		var targetSlots = SlotsFor(slot);
		if (targetSlots.Length == 0)
			return false;

		if ((targetSlots.Length == 2 && !template.IsTwoHandWeapon) || targetSlots.Length > 2)
			return false;

		if ((MainOffOrSubOff & slot) != 0)
			return false;

		if (template.ValidEquipmentSlots == 0 || (template.ValidEquipmentSlots & slot) != slot)
			return false;

		if (InventoryCapacity.GetFreeCubeSlots(player) <= 0 && IsTwoHandedWeaponSlot(slot))
		{
			foreach (var targetSlot in targetSlots)
			{
				var equipped = GetEquippedItemBySlot(inventoryItems, targetSlot);
				var equippedTemplate = equipped == null ? null : itemTemplates.GetItemTemplate(equipped.ItemId);
				if (equipped == null || equippedTemplate?.IsTwoHandWeapon == true)
					return true;
			}

			inventoryFull = true;
			return false;
		}

		return true;
	}

	private static long GetUnequipSlots(IReadOnlyList<InventoryItem> inventoryItems, long itemSlotToEquip, ItemTemplateTable itemTemplates)
	{
		if (itemSlotToEquip is MainHand or SubHand)
		{
			var equippedItem = GetEquippedItemBySlot(inventoryItems, itemSlotToEquip);
			if (equippedItem != null && itemTemplates.GetItemTemplate(equippedItem.ItemId) is { IsTwoHandWeapon: true })
				return MainOrSub;
		}

		return itemSlotToEquip;
	}

	private static IEnumerable<InventoryItem> UnEquipSlots(List<InventoryItem> inventoryItems, long slots)
	{
		var updatedByObjectId = new HashSet<int>();
		foreach (var slot in SlotsFor(slots))
		{
			var item = GetEquippedItemBySlot(inventoryItems, slot);
			if (item == null || !updatedByObjectId.Add(item.ObjectId))
				continue;

			var update = CopyInventoryItem(item, slot: 0, isEquipped: false);
			ReplaceInventoryItem(inventoryItems, update);
			yield return update;
		}
	}

	private static bool HasDualWieldEffect(Player player, SkillTemplateTable? skillTemplates)
	{
		// Java parity: skillengine/effect/WeaponDualEffect.hasDualWieldEffect fallback for not-yet-spawned players.
		if (skillTemplates == null)
			return false;

		return player.Skills.Any(skill =>
			skillTemplates.GetSkillTemplate(skill.SkillId)?.WeaponDual.Count > 0);
	}

	private static bool IsOneHandWeapon(ItemTemplateSummary template)
	{
		return template.IsWeapon && !template.IsTwoHandWeapon;
	}

	private static bool IsTwoHandedWeaponSlot(long slot)
	{
		return (slot & MainOrSub) == MainOrSub || (slot & MainOffOrSubOff) == MainOffOrSubOff;
	}

	private static string GetItemName(ItemTemplateSummary template)
	{
		return template.GetClientName() ?? template.Name;
	}

	private static InventoryItem? GetEquippedItemByObjectId(IReadOnlyList<InventoryItem> inventoryItems, int objectId)
	{
		return inventoryItems.FirstOrDefault(item => item.Location == CubeStorageId && item.IsEquipped && item.ObjectId == objectId);
	}

	private static InventoryItem? GetEquippedItemBySlot(IReadOnlyList<InventoryItem> inventoryItems, long slot)
	{
		return inventoryItems.FirstOrDefault(item =>
			item.Location == CubeStorageId
			&& item.IsEquipped
			&& SlotsFor(item.Slot).Contains(slot));
	}

	private static void AddDistinct(List<InventoryItem> items, InventoryItem? item)
	{
		if (item != null && items.All(existing => existing.ObjectId != item.ObjectId))
			items.Add(item);
	}

	private static long SwitchHandBits(long slot)
	{
		var switched = slot;
		if ((switched & RightHand) != 0)
			switched ^= RightHand;
		if ((switched & LeftHand) != 0)
			switched ^= LeftHand;
		return switched;
	}

	private static long[] SlotsFor(long slotMask)
	{
		ReadOnlySpan<long> slots =
		[
			1L,
			1L << 1,
			1L << 2,
			1L << 3,
			1L << 4,
			1L << 5,
			1L << 6,
			1L << 7,
			1L << 8,
			1L << 9,
			1L << 10,
			1L << 11,
			1L << 12,
			1L << 13,
			1L << 14,
			1L << 15,
			1L << 16,
			1L << 17,
			1L << 18,
			1L << 19,
			1L << 30,
			1L << 31,
			1L << 32,
			1L << 33,
			1L << 34,
			1L << 35,
		];

		var result = new List<long>();
		foreach (var slot in slots)
		{
			if ((slotMask & slot) == slot)
				result.Add(slot);
		}

		return result.ToArray();
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem update)
	{
		var index = items.FindIndex(item => item.ObjectId == update.ObjectId);
		if (index >= 0)
			items[index] = update;
		else
			items.Add(update);
	}

	private static InventoryItem CopyInventoryItem(
		InventoryItem item,
		long? slot = null,
		bool? isEquipped = null,
		bool? isSoulBound = null)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = isEquipped ?? item.IsEquipped,
			IsSoulBound = isSoulBound ?? item.IsSoulBound,
			Slot = slot ?? item.Slot,
			Location = item.Location,
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
}

public enum EquipmentChangeFailure
{
	None,
	InventoryFull,
	InvalidClass,
	TooLowLevel,
	TooHighLevel,
	InvalidRace,
	InvalidGender,
	InvalidRank,
	MissingRequiredSkill,
	UnidentifiedItem,
	SoulBindRequired,
	StigmaDenied,
	StigmaNotEnoughKinah,
}

public sealed record EquipmentChangeResult(
	bool Changed,
	bool InventoryFull,
	bool BroadcastAppearance,
	bool RefreshStats,
	IReadOnlyList<InventoryItem> InventoryItems,
	IReadOnlyList<InventoryItem> PersistedItems,
	IReadOnlyList<InventoryItem> InventoryUpdateItems,
	EquipmentChangeFailure Failure = EquipmentChangeFailure.None,
	int RequiredLevel = 0,
	int MaxLevel = 0,
	string RankName = "",
	string ItemName = "",
	int SoulBindItemObjectId = 0,
	long SoulBindSlot = 0,
	InventoryItem? KinahItemUpdate = null,
	IReadOnlyList<PlayerSkill>? Skills = null,
	IReadOnlyList<PlayerSkill>? AddedSkills = null,
	IReadOnlyList<PlayerSkill>? RemovedSkills = null,
	IReadOnlyList<string>? RemovedStigmaSkillNames = null)
{
	public IReadOnlyList<PlayerSkill> FinalSkills => Skills ?? Array.Empty<PlayerSkill>();

	public IReadOnlyList<PlayerSkill> SkillListUpdates => AddedSkills ?? Array.Empty<PlayerSkill>();

	public IReadOnlyList<PlayerSkill> SkillRemoveUpdates => RemovedSkills ?? Array.Empty<PlayerSkill>();

	public IReadOnlyList<string> StigmaSkillRemoveMessages => RemovedStigmaSkillNames ?? Array.Empty<string>();

	public static EquipmentChangeResult NoChange()
	{
		return new EquipmentChangeResult(
			Changed: false,
			InventoryFull: false,
			BroadcastAppearance: false,
			RefreshStats: false,
			InventoryItems: Array.Empty<InventoryItem>(),
			PersistedItems: Array.Empty<InventoryItem>(),
			InventoryUpdateItems: Array.Empty<InventoryItem>());
	}

	public static EquipmentChangeResult FullInventoryFailure()
	{
		return NoChange() with { InventoryFull = true, Failure = EquipmentChangeFailure.InventoryFull };
	}

	public static EquipmentChangeResult InvalidClassFailure()
	{
		return NoChange() with { Failure = EquipmentChangeFailure.InvalidClass };
	}

	public static EquipmentChangeResult TooLowLevelFailure(string itemName, int requiredLevel)
	{
		return NoChange() with
		{
			Failure = EquipmentChangeFailure.TooLowLevel,
			ItemName = itemName,
			RequiredLevel = requiredLevel,
		};
	}

	public static EquipmentChangeResult TooHighLevelFailure(string itemName, int maxLevel)
	{
		return NoChange() with
		{
			Failure = EquipmentChangeFailure.TooHighLevel,
			ItemName = itemName,
			MaxLevel = maxLevel,
		};
	}

	public static EquipmentChangeResult InvalidRaceFailure()
	{
		return NoChange() with { Failure = EquipmentChangeFailure.InvalidRace };
	}

	public static EquipmentChangeResult InvalidGenderFailure()
	{
		return NoChange() with { Failure = EquipmentChangeFailure.InvalidGender };
	}

	public static EquipmentChangeResult InvalidRankFailure(string rankName)
	{
		return NoChange() with { Failure = EquipmentChangeFailure.InvalidRank, RankName = rankName };
	}

	public static EquipmentChangeResult MissingRequiredSkillFailure()
	{
		return NoChange() with { Failure = EquipmentChangeFailure.MissingRequiredSkill };
	}

	public static EquipmentChangeResult UnidentifiedItemFailure()
	{
		// Java parity: model/gameobjects/player/Equipment.equip logs and silently rejects unidentified items.
		return NoChange() with { Failure = EquipmentChangeFailure.UnidentifiedItem };
	}

	public static EquipmentChangeResult SoulBindRequiredFailure(int itemObjectId, long slot, string itemName)
	{
		// Java parity: model/gameobjects/player/Equipment.soulBindItem response request.
		return NoChange() with
		{
			Failure = EquipmentChangeFailure.SoulBindRequired,
			SoulBindItemObjectId = itemObjectId,
			SoulBindSlot = slot,
			ItemName = itemName,
		};
	}

	public static EquipmentChangeResult StigmaDeniedFailure()
	{
		// Java parity: services/StigmaService.notifyEquipAction audit-only denials return null from Equipment.equipItem.
		return NoChange() with { Failure = EquipmentChangeFailure.StigmaDenied };
	}

	public static EquipmentChangeResult StigmaNotEnoughKinahFailure()
	{
		return NoChange() with { Failure = EquipmentChangeFailure.StigmaNotEnoughKinah };
	}

	public static EquipmentChangeResult Success(
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<InventoryItem> persistedItems,
		IReadOnlyList<InventoryItem> inventoryUpdateItems,
		InventoryItem? kinahItemUpdate = null,
		IReadOnlyList<PlayerSkill>? skills = null,
		IReadOnlyList<PlayerSkill>? addedSkills = null,
		IReadOnlyList<PlayerSkill>? removedSkills = null,
		IReadOnlyList<string>? removedStigmaSkillNames = null)
	{
		return new EquipmentChangeResult(
			Changed: true,
			InventoryFull: false,
			BroadcastAppearance: true,
			RefreshStats: true,
			InventoryItems: inventoryItems,
			PersistedItems: persistedItems,
			InventoryUpdateItems: inventoryUpdateItems,
			KinahItemUpdate: kinahItemUpdate,
			Skills: skills,
			AddedSkills: addedSkills,
			RemovedSkills: removedSkills,
			RemovedStigmaSkillNames: removedStigmaSkillNames);
	}
}
