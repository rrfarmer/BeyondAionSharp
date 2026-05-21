using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class StigmaService
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const long RegularStigmas = (1L << 30) | (1L << 31) | (1L << 32);
	private const long AdvancedStigmas = (1L << 33) | (1L << 34) | (1L << 35);

	public static StigmaEquipResult NotifyEquipAction(
		Player player,
		InventoryItem resultItem,
		ItemTemplateSummary resultTemplate,
		long slot,
		IReadOnlyList<InventoryItem> inventoryItems,
		ItemTemplateTable itemTemplates,
		SkillTemplateTable skillTemplates,
		SkillTreeTable skillTree,
		PlayerExperienceTable? experienceTable = null)
	{
		// Java parity: services/StigmaService.notifyEquipAction.
		if (resultTemplate.StigmaInfo == null)
			return StigmaEquipResult.Success(player.Skills, Array.Empty<PlayerSkill>(), Array.Empty<PlayerSkill>(), Array.Empty<string>(), kinahItemUpdate: null);

		var skills = player.Skills.ToList();
		var removedSkills = new List<PlayerSkill>();
		var removedSkillNames = new List<string>();
		var stigmaName = NormalizeStigmaName(resultTemplate);
		var replace = false;

		foreach (var equippedStigma in GetEquippedStigmas(inventoryItems, itemTemplates))
		{
			if (equippedStigma.Item.Slot != slot)
				continue;

			if (!string.Equals(stigmaName, NormalizeStigmaName(equippedStigma.Template), StringComparison.Ordinal))
				return StigmaEquipResult.Failed(StigmaEquipFailure.Denied);

			var removal = RemoveStigmaSkills(
				skills,
				player,
				equippedStigma.Template.StigmaInfo,
				skillTemplates,
				skillTree,
				notifyPlayer: equippedStigma.Item.Enchant > resultItem.Enchant);
			removedSkills.AddRange(removal.RemovedSkills);
			removedSkillNames.AddRange(removal.RemovedSkillNames);
			replace = true;
			break;
		}

		if (!replace)
		{
			if (IsRegularStigma(slot) && GetPossibleStigmaCount(player, experienceTable) <= GetEquippedStigmas(inventoryItems, itemTemplates).Count(stigma => IsRegularStigma(stigma.Item.Slot)))
				return StigmaEquipResult.Failed(StigmaEquipFailure.Denied);
			if (IsAdvancedStigma(slot) && GetPossibleAdvancedStigmaCount(player, experienceTable) <= GetEquippedStigmas(inventoryItems, itemTemplates).Count(stigma => IsAdvancedStigma(stigma.Item.Slot)))
				return StigmaEquipResult.Failed(StigmaEquipFailure.Denied);
		}

		var kinahPrice = GetStigmaEquipPrice(player, resultTemplate);
		var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem == null || kinahItem.Count < kinahPrice)
			return StigmaEquipResult.Failed(StigmaEquipFailure.NotEnoughKinah);

		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - kinahPrice);
		var addedSkills = AddStigmaSkills(skills, player, resultTemplate.StigmaInfo, resultItem.Enchant, skillTemplates, skillTree, experienceTable);
		return StigmaEquipResult.Success(skills, addedSkills, removedSkills, removedSkillNames, kinahUpdate);
	}

	public static StigmaUnequipResult NotifyUnequipAction(
		Player player,
		InventoryItem item,
		ItemTemplateSummary itemTemplate,
		SkillTemplateTable skillTemplates,
		SkillTreeTable skillTree)
	{
		// Java parity: services/StigmaService.removeStigmaSkills called by Equipment.unEquipItem.
		if (itemTemplate.StigmaInfo == null)
			return StigmaUnequipResult.Success(player.Skills, Array.Empty<PlayerSkill>(), Array.Empty<string>());

		var skills = player.Skills.ToList();
		var removal = RemoveStigmaSkills(skills, player, itemTemplate.StigmaInfo, skillTemplates, skillTree, notifyPlayer: true);
		return StigmaUnequipResult.Success(skills, removal.RemovedSkills, removal.RemovedSkillNames);
	}

	private static IReadOnlyList<PlayerSkill> AddStigmaSkills(
		List<PlayerSkill> skills,
		Player player,
		ItemStigmaInfo stigma,
		int stigmaLevel,
		SkillTemplateTable skillTemplates,
		SkillTreeTable skillTree,
		PlayerExperienceTable? experienceTable)
	{
		// Java parity: services/StigmaService.addStigmaSkills.
		var playerLevel = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
		var addedSkills = new List<PlayerSkill>();
		foreach (var skillGroup in stigma.GainSkillGroups)
		{
			foreach (var skillTemplate in skillTemplates.GetSkillTemplatesByGroup(skillGroup))
			{
				foreach (var skill in skillTree.GetTemplatesForSkill(skillTemplate.SkillId, player.PlayerClass, player.Race))
				{
					if (playerLevel < skill.MinLevel)
						continue;

					var learned = AddOrUpgradeTemporarySkill(skills, skill.SkillId, stigmaLevel + 1, skill.IsLinkedStigma ? 3 : 1);
					if (learned != null)
						addedSkills.Add(learned);
				}
			}
		}

		return addedSkills;
	}

	private static StigmaSkillRemoval RemoveStigmaSkills(
		List<PlayerSkill> skills,
		Player player,
		ItemStigmaInfo? stigma,
		SkillTemplateTable skillTemplates,
		SkillTreeTable skillTree,
		bool notifyPlayer)
	{
		// Java parity: services/StigmaService.removeStigmaSkills.
		if (stigma == null)
			return new StigmaSkillRemoval(Array.Empty<PlayerSkill>(), Array.Empty<string>());

		var removedSkills = new List<PlayerSkill>();
		var removedSkillNames = new List<string>();
		foreach (var skillGroup in stigma.GainSkillGroups)
		{
			foreach (var skillTemplate in skillTemplates.GetSkillTemplatesByGroup(skillGroup))
			{
				var skillName = skillTemplate.GetClientName() ?? skillTemplate.Name;
				if (notifyPlayer && !string.IsNullOrEmpty(skillName) && !removedSkillNames.Contains(skillName, StringComparer.Ordinal))
					removedSkillNames.Add(skillName);

				foreach (var skill in skillTree.GetSkillsForSkill(skillTemplate.SkillId, player.PlayerClass, player.Race, playerLevel: -1, skillTemplates: skillTemplates))
				{
					var removed = RemoveSkill(skills, skill.SkillId);
					if (removed != null)
						removedSkills.Add(removed);
				}
			}
		}

		foreach (var linkedSkill in skills.Where(skill => skill.SkillType >= 3).ToArray())
		{
			var removed = RemoveSkill(skills, linkedSkill.SkillId);
			if (removed != null)
				removedSkills.Add(removed);
		}

		return new StigmaSkillRemoval(removedSkills, removedSkillNames);
	}

	private static PlayerSkill? AddOrUpgradeTemporarySkill(List<PlayerSkill> skills, int skillId, int skillLevel, int skillType)
	{
		var existingIndex = skills.FindIndex(skill => skill.SkillId == skillId);
		if (existingIndex >= 0)
		{
			var existing = skills[existingIndex];
			if (skillLevel <= existing.SkillLevel)
				return null;

			var upgraded = new PlayerSkill
			{
				SkillId = existing.SkillId,
				SkillLevel = skillLevel,
				SkillType = skillType,
				CurrentXp = existing.CurrentXp,
			};
			skills[existingIndex] = upgraded;
			return upgraded;
		}

		var added = new PlayerSkill { SkillId = skillId, SkillLevel = skillLevel, SkillType = skillType };
		skills.Add(added);
		return added;
	}

	private static PlayerSkill? RemoveSkill(List<PlayerSkill> skills, int skillId)
	{
		var existingIndex = skills.FindIndex(skill => skill.SkillId == skillId);
		if (existingIndex < 0)
			return null;

		var removed = skills[existingIndex];
		skills.RemoveAt(existingIndex);
		return removed;
	}

	private static IEnumerable<EquippedStigma> GetEquippedStigmas(IReadOnlyList<InventoryItem> inventoryItems, ItemTemplateTable itemTemplates)
	{
		foreach (var item in inventoryItems.Where(item => item.Location == CubeStorageId && item.IsEquipped))
		{
			var template = itemTemplates.GetItemTemplate(item.ItemId);
			if (template?.StigmaInfo != null)
				yield return new EquippedStigma(item, template);
		}
	}

	private static int GetPossibleStigmaCount(Player player, PlayerExperienceTable? experienceTable)
	{
		// Java parity: services/StigmaService.getPossibleStigmaCount without membership override until account permission parity exists.
		var playerLevel = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
		if (!IsCompleteStigmaQuest(player))
			return 0;
		if (playerLevel < 30)
			return 1;
		return playerLevel < 40 ? 2 : 3;
	}

	private static int GetPossibleAdvancedStigmaCount(Player player, PlayerExperienceTable? experienceTable)
	{
		// Java parity: services/StigmaService.getPossibleAdvancedStigmaCount without membership override until account permission parity exists.
		var playerLevel = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
		if (!IsCompleteStigmaQuest(player))
			return 0;
		if (playerLevel >= 55)
			return 3;
		if (playerLevel >= 50)
			return 2;
		return playerLevel >= 45 ? 1 : 0;
	}

	private static bool IsCompleteStigmaQuest(Player player)
	{
		// Java parity: services/StigmaService.isCompleteQuest.
		var questId = player.Race == "ELYOS" ? 1929 : 2900;
		var transitionalQuestVar = player.Race == "ELYOS" ? 98 : 99;
		var quest = player.Quests.FirstOrDefault(state => state.QuestId == questId);
		if (quest == null)
			return false;

		return quest.IsComplete
			|| quest.IsCompletedAtLeastOnce
			|| (string.Equals(quest.Status, "START", StringComparison.Ordinal) && quest.QuestVars == transitionalQuestVar);
	}

	private static long GetStigmaEquipPrice(Player player, ItemTemplateSummary itemTemplate)
	{
		long kinahCount = 25000;
		if ((player.Race == "ASMODIANS" && player.Position.WorldId == 320070000)
			|| (player.Race == "ELYOS" && player.Position.WorldId == 310070000))
		{
			kinahCount = 1000;
		}
		else if (string.Equals(itemTemplate.Quality, "LEGEND", StringComparison.Ordinal))
		{
			kinahCount = 50000;
		}
		else if (string.Equals(itemTemplate.Quality, "UNIQUE", StringComparison.Ordinal))
		{
			kinahCount = 100000;
		}

		return GetPriceForService(kinahCount, player.Race);
	}

	private static long GetPriceForService(long basePrice, string race)
	{
		// Java parity: services/trade/PricesService.getPriceForService with the currently ported baseline SM_PRICES values.
		return race is "ELYOS" or "ASMODIANS" ? basePrice : basePrice;
	}

	private static bool IsRegularStigma(long slot)
	{
		return (RegularStigmas & slot) == slot;
	}

	private static bool IsAdvancedStigma(long slot)
	{
		return (AdvancedStigmas & slot) == slot;
	}

	private static string NormalizeStigmaName(ItemTemplateSummary itemTemplate)
	{
		return (itemTemplate.GetClientName() ?? itemTemplate.Name).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long? count = null)
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
			Slot = item.Slot,
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

	private sealed record EquippedStigma(InventoryItem Item, ItemTemplateSummary Template);

	private sealed record StigmaSkillRemoval(IReadOnlyList<PlayerSkill> RemovedSkills, IReadOnlyList<string> RemovedSkillNames);
}

public enum StigmaEquipFailure
{
	None,
	Denied,
	NotEnoughKinah,
}

public sealed record StigmaEquipResult(
	bool Allowed,
	StigmaEquipFailure Failure,
	IReadOnlyList<PlayerSkill> Skills,
	IReadOnlyList<PlayerSkill> AddedSkills,
	IReadOnlyList<PlayerSkill> RemovedSkills,
	IReadOnlyList<string> RemovedSkillNames,
	InventoryItem? KinahItemUpdate)
{
	public static StigmaEquipResult Success(
		IReadOnlyList<PlayerSkill> skills,
		IReadOnlyList<PlayerSkill> addedSkills,
		IReadOnlyList<PlayerSkill> removedSkills,
		IReadOnlyList<string> removedSkillNames,
		InventoryItem? kinahItemUpdate)
	{
		return new StigmaEquipResult(true, StigmaEquipFailure.None, skills, addedSkills, removedSkills, removedSkillNames, kinahItemUpdate);
	}

	public static StigmaEquipResult Failed(StigmaEquipFailure failure)
	{
		return new StigmaEquipResult(false, failure, Array.Empty<PlayerSkill>(), Array.Empty<PlayerSkill>(), Array.Empty<PlayerSkill>(), Array.Empty<string>(), null);
	}
}

public sealed record StigmaUnequipResult(
	IReadOnlyList<PlayerSkill> Skills,
	IReadOnlyList<PlayerSkill> RemovedSkills,
	IReadOnlyList<string> RemovedSkillNames)
{
	public static StigmaUnequipResult Success(
		IReadOnlyList<PlayerSkill> skills,
		IReadOnlyList<PlayerSkill> removedSkills,
		IReadOnlyList<string> removedSkillNames)
	{
		return new StigmaUnequipResult(skills, removedSkills, removedSkillNames);
	}
}
