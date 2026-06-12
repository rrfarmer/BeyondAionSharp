using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class StigmaService
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const long StigmaSlot1 = 1L << 30;
	private const long StigmaSlot2 = 1L << 31;
	private const long StigmaSlot3 = 1L << 32;
	private const long AdvancedStigmaSlot1 = 1L << 33;
	private const long AdvancedStigmaSlot2 = 1L << 34;
	private const long AdvancedStigmaSlot3 = 1L << 35;
	private const long RegularStigmas = StigmaSlot1 | StigmaSlot2 | StigmaSlot3;
	private const long AdvancedStigmas = AdvancedStigmaSlot1 | AdvancedStigmaSlot2 | AdvancedStigmaSlot3;

	public static StigmaEquipResult NotifyEquipAction(
		Player player,
		InventoryItem resultItem,
		ItemTemplateSummary resultTemplate,
		long slot,
		IReadOnlyList<InventoryItem> inventoryItems,
		ItemTemplateTable itemTemplates,
		SkillTemplateTable skillTemplates,
		SkillTreeTable skillTree,
		PlayerExperienceTable? experienceTable = null,
		byte stigmaSlotQuestMembership = 10,
		GameServerPriceOptions? priceOptions = null,
		PriceInfluenceRates? influenceRates = null)
	{
		// Java parity: services/StigmaService.notifyEquipAction.
		if (resultTemplate.StigmaInfo == null)
			return StigmaEquipResult.Success(
				player.Skills,
				Array.Empty<PlayerSkill>(),
				Array.Empty<PlayerSkill>(),
				Array.Empty<string>(),
				Array.Empty<StigmaHiddenSkillDeleteMessage>(),
				kinahItemUpdate: null);

		var skills = player.Skills.ToList();
		var removedSkills = new List<PlayerSkill>();
		var removedSkillNames = new List<string>();
		var hiddenSkillDeleteMessages = new List<StigmaHiddenSkillDeleteMessage>();
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
			hiddenSkillDeleteMessages.AddRange(removal.HiddenSkillDeleteMessages);
			replace = true;
			break;
		}

		if (!replace)
		{
			if (IsRegularStigma(slot) && GetPossibleStigmaCount(player, experienceTable, stigmaSlotQuestMembership) <= GetEquippedStigmas(inventoryItems, itemTemplates).Count(stigma => IsRegularStigma(stigma.Item.Slot)))
				return StigmaEquipResult.Failed(StigmaEquipFailure.Denied);
			if (IsAdvancedStigma(slot) && GetPossibleAdvancedStigmaCount(player, experienceTable, stigmaSlotQuestMembership) <= GetEquippedStigmas(inventoryItems, itemTemplates).Count(stigma => IsAdvancedStigma(stigma.Item.Slot)))
				return StigmaEquipResult.Failed(StigmaEquipFailure.Denied);
		}

		var kinahPrice = GetStigmaEquipPrice(
			player,
			resultTemplate,
			priceOptions ?? new GameServerPriceOptions(),
			influenceRates ?? new PriceInfluenceRates());
		var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem == null || kinahItem.Count < kinahPrice)
			return StigmaEquipResult.Failed(StigmaEquipFailure.NotEnoughKinah);

		var kinahUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - kinahPrice);
		var addedSkills = AddStigmaSkills(skills, player, resultTemplate.StigmaInfo, resultItem.Enchant, skillTemplates, skillTree, experienceTable).ToList();
		addedSkills.AddRange(AddLinkedStigmaSkills(
			skills,
			player,
			GetEquippedStigmasAfterEquip(inventoryItems, itemTemplates, resultItem, resultTemplate, slot),
			skillTemplates,
			skillTree,
			experienceTable));
		return StigmaEquipResult.Success(skills, addedSkills, removedSkills, removedSkillNames, hiddenSkillDeleteMessages, kinahUpdate);
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
			return StigmaUnequipResult.Success(
				player.Skills,
				Array.Empty<PlayerSkill>(),
				Array.Empty<string>(),
				Array.Empty<StigmaHiddenSkillDeleteMessage>());

		var skills = player.Skills.ToList();
		var removal = RemoveStigmaSkills(skills, player, itemTemplate.StigmaInfo, skillTemplates, skillTree, notifyPlayer: true);
		return StigmaUnequipResult.Success(skills, removal.RemovedSkills, removal.RemovedSkillNames, removal.HiddenSkillDeleteMessages);
	}

	public static StigmaAutoLearnResult ApplyAutoLearnOnLogin(
		Player player,
		SkillTreeTable skillTree,
		PlayerExperienceTable? experienceTable,
		byte stigmaAutoLearnMembership = 10)
	{
		// Java parity: services/StigmaService.onPlayerLogin membership autolearn branch.
		if (!HasPermission(player, stigmaAutoLearnMembership))
			return StigmaAutoLearnResult.NoChange(player.Skills);

		var playerLevel = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
		var skills = player.Skills.ToList();
		var addedSkills = new List<PlayerSkill>();
		for (var level = 20; level <= playerLevel; level++)
		{
			foreach (var template in skillTree.GetTemplatesFor(player.PlayerClass.ToString(), level, player.Race.ToString()))
			{
				if (!template.IsStigma)
					continue;

				var learned = AddOrUpgradeTemporarySkill(skills, template.SkillId, template.SkillLevel, template.IsLinkedStigma ? 3 : 1);
				if (learned != null)
					addedSkills.Add(learned);
			}
		}

		return addedSkills.Count == 0
			? StigmaAutoLearnResult.NoChange(player.Skills)
			: new StigmaAutoLearnResult(true, skills, addedSkills);
	}

	public static StigmaLoginResult ApplyOnLogin(
		Player player,
		ItemTemplateTable itemTemplates,
		SkillTemplateTable skillTemplates,
		SkillTreeTable skillTree,
		PlayerExperienceTable? experienceTable,
		byte stigmaAutoLearnMembership = 10,
		byte stigmaSlotQuestMembership = 10)
	{
		// Java parity: services/StigmaService.onPlayerLogin.
		if (HasPermission(player, stigmaAutoLearnMembership))
		{
			var autoLearn = ApplyAutoLearnOnLogin(player, skillTree, experienceTable, stigmaAutoLearnMembership);
			return new StigmaLoginResult(
				autoLearn.Changed,
				player.InventoryItems,
				Array.Empty<InventoryItem>(),
				autoLearn.Skills,
				autoLearn.AddedSkills);
		}

		var inventoryItems = player.InventoryItems.ToList();
		var persistedItems = new List<InventoryItem>();
		var skills = player.Skills.ToList();
		var addedSkills = new List<PlayerSkill>();

		foreach (var equippedItem in GetEquippedStigmaSlotItems(inventoryItems).ToArray())
		{
			var item = inventoryItems.FirstOrDefault(candidate =>
				candidate.ObjectId == equippedItem.ObjectId
				&& candidate.IsEquipped
				&& IsStigmaSlot(candidate.Slot));
			if (item == null)
				continue;

			var template = itemTemplates.GetItemTemplate(item.ItemId);
			if (template?.StigmaInfo == null
				|| !IsPossibleEquippedStigma(player, item, experienceTable, stigmaSlotQuestMembership)
				|| !template.IsClassSpecific(player.PlayerClass.ToString())
				|| HasDifferentStigmaEquippedInSameSlot(inventoryItems, item))
			{
				var update = CopyInventoryItem(item, slot: 0, isEquipped: false);
				ReplaceInventoryItem(inventoryItems, update);
				persistedItems.Add(update);
				continue;
			}

			addedSkills.AddRange(AddStigmaSkills(skills, player, template.StigmaInfo, item.Enchant, skillTemplates, skillTree, experienceTable));
		}

		addedSkills.AddRange(AddLinkedStigmaSkills(
			skills,
			player,
			GetEquippedStigmas(inventoryItems, itemTemplates).ToArray(),
			skillTemplates,
			skillTree,
			experienceTable));

		return persistedItems.Count == 0 && addedSkills.Count == 0
			? StigmaLoginResult.NoChange(player)
			: new StigmaLoginResult(true, inventoryItems, persistedItems, skills, addedSkills);
	}

	public static StigmaChargePlan CreateChargePlan(
		Player player,
		int targetItemObjectId,
		int chargeStoneObjectId,
		ItemTemplateTable itemTemplates,
		SkillTemplateTable skillTemplates,
		SkillTreeTable skillTree,
		PlayerExperienceTable? experienceTable = null,
		Func<double>? rollPercent = null)
	{
		// Java parity: services/StigmaService.chargeStigma.
		var inventoryItems = player.InventoryItems.ToList();
		var chargeStone = inventoryItems.FirstOrDefault(item =>
			item.ObjectId == chargeStoneObjectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
		var stigma = inventoryItems.FirstOrDefault(item =>
			item.ObjectId == targetItemObjectId
			&& item.Location == CubeStorageId);
		if (chargeStone == null || stigma == null)
			return StigmaChargePlan.Invalid();

		var chargeStoneTemplate = itemTemplates.GetItemTemplate(chargeStone.ItemId);
		var stigmaTemplate = itemTemplates.GetItemTemplate(stigma.ItemId);
		if (chargeStoneTemplate?.StigmaInfo == null || stigmaTemplate?.StigmaInfo == null)
			return StigmaChargePlan.Invalid();
		if (stigma.ItemId != chargeStone.ItemId || chargeStone.Enchant > 0 || stigma.Enchant >= 10)
			return StigmaChargePlan.Invalid();
		if (!stigmaTemplate.StigmaInfo.Chargeable)
			return StigmaChargePlan.Invalid();

		var roll = rollPercent?.Invoke() ?? Random.Shared.NextDouble() * 100d;
		var isSuccess = roll < Math.Max(25, 100 - (stigma.Enchant * 10));
		var sourceUpdate = DecreaseItemCount(chargeStone);
		InventoryItem? targetUpdate = null;
		int? deletedTargetObjectId = null;
		var skills = player.Skills.ToList();
		var addedSkills = new List<PlayerSkill>();
		var removedSkills = new List<PlayerSkill>();
		var hiddenSkillDeleteMessages = new List<StigmaHiddenSkillDeleteMessage>();

		if (isSuccess)
		{
			targetUpdate = CopyInventoryItem(stigma, enchant: stigma.Enchant + 1);
			ReplaceInventoryItem(inventoryItems, targetUpdate);
			if (stigma.IsEquipped)
			{
				var removal = RemoveStigmaSkills(skills, player, stigmaTemplate.StigmaInfo, skillTemplates, skillTree, notifyPlayer: false);
				removedSkills.AddRange(removal.RemovedSkills);
				hiddenSkillDeleteMessages.AddRange(removal.HiddenSkillDeleteMessages);
				addedSkills.AddRange(AddStigmaSkills(skills, player, stigmaTemplate.StigmaInfo, targetUpdate.Enchant, skillTemplates, skillTree, experienceTable));
			}
		}
		else
		{
			var targetCountUpdate = DecreaseItemCount(stigma);
			if (targetCountUpdate.UpdatedItem != null)
			{
				targetUpdate = targetCountUpdate.UpdatedItem;
				ReplaceInventoryItem(inventoryItems, targetUpdate);
			}
			else
			{
				deletedTargetObjectId = targetCountUpdate.DeletedObjectId;
				inventoryItems.RemoveAll(item => item.ObjectId == deletedTargetObjectId);
			}

			if (stigma.IsEquipped)
			{
				var removal = RemoveStigmaSkills(skills, player, stigmaTemplate.StigmaInfo, skillTemplates, skillTree, notifyPlayer: false);
				removedSkills.AddRange(removal.RemovedSkills);
				hiddenSkillDeleteMessages.AddRange(removal.HiddenSkillDeleteMessages);
			}
		}

		if (sourceUpdate.UpdatedItem != null)
			ReplaceInventoryItem(inventoryItems, sourceUpdate.UpdatedItem);
		else if (sourceUpdate.DeletedObjectId.HasValue)
			inventoryItems.RemoveAll(item => item.ObjectId == sourceUpdate.DeletedObjectId);

		return new StigmaChargePlan(
			StigmaChargeResult.Success,
			isSuccess,
			GetItemName(stigmaTemplate),
			inventoryItems,
			targetUpdate,
			deletedTargetObjectId,
			sourceUpdate.UpdatedItem,
			sourceUpdate.DeletedObjectId,
			skills,
			addedSkills,
			removedSkills,
			hiddenSkillDeleteMessages);
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
				foreach (var skill in skillTree.GetTemplatesForSkill(skillTemplate.SkillId, player.PlayerClass.ToString(), player.Race.ToString()))
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

	private static IReadOnlyList<PlayerSkill> AddLinkedStigmaSkills(
		List<PlayerSkill> skills,
		Player player,
		IReadOnlyList<EquippedStigma> stigmas,
		SkillTemplateTable skillTemplates,
		SkillTreeTable skillTree,
		PlayerExperienceTable? experienceTable)
	{
		// Java parity: services/StigmaService.addLinkedStigmaSkills.
		if (stigmas.Count < 6 || stigmas.Any(stigma => stigma.Template.StigmaInfo?.Chargeable != true))
			return Array.Empty<PlayerSkill>();

		var skillId = GetLinkedStigmaLearnSkill(player, stigmas.Select(stigma => stigma.Item.ItemId).ToHashSet());
		if (skillId <= 0)
			return Array.Empty<PlayerSkill>();

		var playerLevel = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
		var linkedStigmaSkillLevel = stigmas.Min(stigma => stigma.Item.Enchant) + 1;
		var addedSkills = new List<PlayerSkill>();
		foreach (var skill in skillTree.GetSkillsForSkill(skillId, player.PlayerClass.ToString(), player.Race.ToString(), playerLevel, skillTemplates))
		{
			var learned = AddOrUpgradeTemporarySkill(skills, skill.SkillId, linkedStigmaSkillLevel, skill.IsLinkedStigma ? 3 : 1);
			if (learned != null)
				addedSkills.Add(learned);
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
			return new StigmaSkillRemoval(
				Array.Empty<PlayerSkill>(),
				Array.Empty<string>(),
				Array.Empty<StigmaHiddenSkillDeleteMessage>());

		var removedSkills = new List<PlayerSkill>();
		var removedSkillNames = new List<string>();
		foreach (var skillGroup in stigma.GainSkillGroups)
		{
			foreach (var skillTemplate in skillTemplates.GetSkillTemplatesByGroup(skillGroup))
			{
				var skillName = skillTemplate.GetClientName() ?? skillTemplate.Name;
				if (notifyPlayer && !string.IsNullOrEmpty(skillName) && !removedSkillNames.Contains(skillName, StringComparer.Ordinal))
					removedSkillNames.Add(skillName);

				foreach (var skill in skillTree.GetSkillsForSkill(skillTemplate.SkillId, player.PlayerClass.ToString(), player.Race.ToString(), playerLevel: -1, skillTemplates: skillTemplates))
				{
					var removed = RemoveSkill(skills, skill.SkillId);
					if (removed != null)
						removedSkills.Add(removed);
				}
			}
		}

		var linkedRemoval = RemoveLinkedStigmaSkills(skills, skillTemplates);
		removedSkills.AddRange(linkedRemoval.RemovedSkills);

		return new StigmaSkillRemoval(removedSkills, removedSkillNames, linkedRemoval.HiddenSkillDeleteMessages);
	}

	private static StigmaLinkedSkillRemoval RemoveLinkedStigmaSkills(List<PlayerSkill> skills, SkillTemplateTable skillTemplates)
	{
		// Java parity: services/StigmaService.removeLinkedStigmaSkills.
		var removedSkills = new List<PlayerSkill>();
		var hiddenSkillDeleteMessages = new List<StigmaHiddenSkillDeleteMessage>();
		while (true)
		{
			string? stack = null;
			var linkedStigmaSkills = new List<PlayerSkill>();
			foreach (var skill in skills.Where(skill => skill.SkillType >= 3).ToArray())
			{
				var skillTemplate = skillTemplates.GetSkillTemplate(skill.SkillId);
				var skillStack = string.IsNullOrEmpty(skillTemplate?.Stack) ? "NONE" : skillTemplate.Stack;
				stack ??= skillStack;
				if (string.Equals(skillStack, stack, StringComparison.OrdinalIgnoreCase))
					linkedStigmaSkills.Add(skill);
				if (string.Equals(stack, "NONE", StringComparison.OrdinalIgnoreCase))
					break;
			}

			if (linkedStigmaSkills.Count == 0)
				break;

			string? firstSkillName = null;
			string? secondSkillName = null;
			var skillLevel = 0;
			for (var index = 0; index < linkedStigmaSkills.Count; index++)
			{
				var skillEntry = linkedStigmaSkills[index];
				var removed = RemoveSkill(skills, skillEntry.SkillId);
				if (removed != null)
					removedSkills.Add(removed);

				var skillName = skillTemplates.GetSkillTemplate(skillEntry.SkillId)?.GetClientName();
				if (index == 0)
				{
					firstSkillName = skillName;
					skillLevel = skillEntry.SkillLevel;
				}
				else if (index == 1)
				{
					secondSkillName = skillName;
				}
			}

			hiddenSkillDeleteMessages.Add(new StigmaHiddenSkillDeleteMessage(firstSkillName, skillLevel, secondSkillName));
		}

		return new StigmaLinkedSkillRemoval(removedSkills, hiddenSkillDeleteMessages);
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

	private static ItemCountMutation DecreaseItemCount(InventoryItem item)
	{
		return item.Count > 1
			? new ItemCountMutation(CopyInventoryItem(item, count: item.Count - 1), null)
			: new ItemCountMutation(null, item.ObjectId);
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem update)
	{
		var index = items.FindIndex(item => item.ObjectId == update.ObjectId);
		if (index >= 0)
			items[index] = update;
		else
			items.Add(update);
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

	private static IEnumerable<InventoryItem> GetEquippedStigmaSlotItems(IReadOnlyList<InventoryItem> inventoryItems)
	{
		return inventoryItems.Where(item => item.Location == CubeStorageId && item.IsEquipped && IsStigmaSlot(item.Slot));
	}

	private static bool HasDifferentStigmaEquippedInSameSlot(IReadOnlyList<InventoryItem> inventoryItems, InventoryItem item)
	{
		// Java parity: services/StigmaService.onPlayerLogin double-stigma same-slot check.
		return GetEquippedStigmaSlotItems(inventoryItems)
			.Any(checkStigma =>
				checkStigma.ObjectId != item.ObjectId
				&& checkStigma.Slot == item.Slot
				&& checkStigma.ItemId != item.ItemId);
	}

	private static IReadOnlyList<EquippedStigma> GetEquippedStigmasAfterEquip(
		IReadOnlyList<InventoryItem> inventoryItems,
		ItemTemplateTable itemTemplates,
		InventoryItem resultItem,
		ItemTemplateSummary resultTemplate,
		long slot)
	{
		// Java parity: Equipment.equip calls addLinkedStigmaSkills after the target stigma is equipped.
		var stigmas = GetEquippedStigmas(inventoryItems, itemTemplates)
			.Where(stigma => stigma.Item.ObjectId != resultItem.ObjectId && stigma.Item.Slot != slot)
			.ToList();
		stigmas.Add(new EquippedStigma(CopyInventoryItem(resultItem, slot: slot, isEquipped: true), resultTemplate));
		return stigmas;
	}

	private static int GetLinkedStigmaLearnSkill(Player player, IReadOnlySet<int> equippedItemIds)
	{
		// Java parity: services/StigmaService.getLinkedStigmaLearnSkill.
		var isElyos = player.Race.ToString() == "ELYOS";
		return player.PlayerClass.ToString() switch
		{
			"GLADIATOR" => IsEquipped(equippedItemIds, 140001118) && IsEquipped(equippedItemIds, 2, 140001103, 140001104, 140001105)
				? 731
				: IsEquipped(equippedItemIds, 140001119) && IsEquipped(equippedItemIds, 2, 140001106, 140001107, 140001108)
					? 643
					: isElyos ? 662 : 661,
			"TEMPLAR" => IsEquipped(equippedItemIds, 140001134) && IsEquipped(equippedItemIds, 2, 140001120, 140001122, 140001125)
				? 2921
				: IsEquipped(equippedItemIds, 140001135) && IsEquipped(equippedItemIds, 2, 140001121, 140001123, 140001124)
					? 2918
					: 2917,
			"ASSASSIN" => IsEquipped(equippedItemIds, 140001151) && IsEquipped(equippedItemIds, 2, 140001136, 140001137, 140001140)
				? 3241
				: IsEquipped(equippedItemIds, 140001152) && IsEquipped(equippedItemIds, 2, 140001138, 140001139, 140001141)
					? 3238
					: 3244,
			"RANGER" => IsEquipped(equippedItemIds, 140001172) && IsEquipped(equippedItemIds, 2, 140001153, 140001155, 140001157)
				? 1008
				: IsEquipped(equippedItemIds, 140001173) && IsEquipped(equippedItemIds, 2, 140001154, 140001156, 140001158)
					? 938
					: isElyos ? 1065 : 1064,
			"SORCERER" => IsEquipped(equippedItemIds, 140001191) && IsEquipped(equippedItemIds, 2, 140001174, 140001178, 140001181)
				? 1342
				: IsEquipped(equippedItemIds, 140001192) && IsEquipped(equippedItemIds, 2, 140001176, 140001177, isElyos ? 140001184 : 140001185)
					? 1542
					: 1420,
			"SPIRIT_MASTER" => IsEquipped(equippedItemIds, 140001209) && IsEquipped(equippedItemIds, 2, 140001193, 140001194, 140001195)
				? 3543
				: IsEquipped(equippedItemIds, 140001210) && IsEquipped(equippedItemIds, 2, 140001196, isElyos ? 140001197 : 140001198, 140001199)
					? 3549
					: 3851,
			"CLERIC" => IsEquipped(equippedItemIds, 140001245) && IsEquipped(equippedItemIds, 2, 140001228, 140001229, isElyos ? 140001230 : 140001231)
				? 4169
				: IsEquipped(equippedItemIds, 140001246) && IsEquipped(equippedItemIds, 2, 140001232, 140001233, isElyos ? 140001234 : 140001235)
					? 3934
					: isElyos ? 3906 : 3911,
			"CHANTER" => IsEquipped(equippedItemIds, 140001226) && IsEquipped(equippedItemIds, 2, 140001211, 140001212, 140001213)
				? 1909
				: IsEquipped(equippedItemIds, 140001227) && IsEquipped(equippedItemIds, 2, 140001214, 140001215, 140001216)
					? 1903
					: 1906,
			"RIDER" => IsEquipped(equippedItemIds, 140001279) && IsEquipped(equippedItemIds, 2, 140001264, 140001265, 140001269)
				? 2858
				: IsEquipped(equippedItemIds, 140001280) && IsEquipped(equippedItemIds, 2, 140001266, 140001267, 140001268)
					? 2863
					: 2851,
			"GUNNER" => IsEquipped(equippedItemIds, 140001262) && IsEquipped(equippedItemIds, 2, 140001247, 140001248, 140001249)
				? 2370
				: IsEquipped(equippedItemIds, 140001263) && IsEquipped(equippedItemIds, 2, 140001250, 140001251, 140001252)
					? 2377
					: 2382,
			"BARD" => IsEquipped(equippedItemIds, 140001296) && IsEquipped(equippedItemIds, 2, 140001281, 140001282, 140001284)
				? 4480
				: IsEquipped(equippedItemIds, 140001297) && IsEquipped(equippedItemIds, 2, 140001283, 140001285, 140001286)
					? 4483
					: 4566,
			_ => 0,
		};
	}

	private static bool IsEquipped(IReadOnlySet<int> equippedItemIds, int itemId)
	{
		return equippedItemIds.Contains(itemId);
	}

	private static bool IsEquipped(IReadOnlySet<int> equippedItemIds, int neededCount, params int[] itemIds)
	{
		var equippedCount = itemIds.Count(equippedItemIds.Contains);
		return equippedCount == neededCount;
	}

	private static int GetPossibleStigmaCount(Player player, PlayerExperienceTable? experienceTable, byte stigmaSlotQuestMembership)
	{
		// Java parity: services/StigmaService.getPossibleStigmaCount + Player.hasPermission(MembershipConfig.STIGMA_SLOT_QUEST).
		if (HasPermission(player, stigmaSlotQuestMembership))
			return 3;

		var playerLevel = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
		if (!IsCompleteStigmaQuest(player))
			return 0;
		if (playerLevel < 30)
			return 1;
		return playerLevel < 40 ? 2 : 3;
	}

	private static int GetPossibleAdvancedStigmaCount(Player player, PlayerExperienceTable? experienceTable, byte stigmaSlotQuestMembership)
	{
		// Java parity: services/StigmaService.getPossibleAdvancedStigmaCount + Player.hasPermission(MembershipConfig.STIGMA_SLOT_QUEST).
		if (HasPermission(player, stigmaSlotQuestMembership))
			return 3;

		var playerLevel = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
		if (!IsCompleteStigmaQuest(player))
			return 0;
		if (playerLevel >= 55)
			return 3;
		if (playerLevel >= 50)
			return 2;
		return playerLevel >= 45 ? 1 : 0;
	}

	private static bool IsPossibleEquippedStigma(
		Player player,
		InventoryItem item,
		PlayerExperienceTable? experienceTable,
		byte stigmaSlotQuestMembership)
	{
		// Java parity: services/StigmaService.isPossibleEquippedStigma.
		if (IsRegularStigma(item.Slot))
			return IsAllowedStigmaSlotByCount(item.Slot, GetPossibleStigmaCount(player, experienceTable, stigmaSlotQuestMembership), StigmaSlot1, StigmaSlot2);
		if (IsAdvancedStigma(item.Slot))
			return IsAllowedStigmaSlotByCount(item.Slot, GetPossibleAdvancedStigmaCount(player, experienceTable, stigmaSlotQuestMembership), AdvancedStigmaSlot1, AdvancedStigmaSlot2);
		return false;
	}

	private static bool IsAllowedStigmaSlotByCount(long itemSlot, int allowedCount, long firstSlot, long secondSlot)
	{
		return allowedCount switch
		{
			1 => itemSlot == firstSlot,
			2 => itemSlot == firstSlot || itemSlot == secondSlot,
			>= 3 => true,
			_ => false,
		};
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

	private static bool HasPermission(Player player, byte permissionLevel)
	{
		// Java parity: model/gameobjects/player/Player.hasPermission.
		return player.AccountMembership >= permissionLevel;
	}

	private static long GetStigmaEquipPrice(
		Player player,
		ItemTemplateSummary itemTemplate,
		GameServerPriceOptions priceOptions,
		PriceInfluenceRates influenceRates)
	{
		// Java parity: services/StigmaService.notifyEquipAction selects the base kinah fee,
		// then services/trade/PricesService.getPriceForService applies global price, modifier, and tax.
		long kinahCount = 25000;
		if ((player.Race == "ASMODIANS" && player.GetPosition().WorldId == 320070000)
			|| (player.Race == "ELYOS" && player.GetPosition().WorldId == 310070000))
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

		return PricesService.GetPriceForService(kinahCount, player.Race.ToString(), priceOptions, influenceRates);
	}

	private static bool IsRegularStigma(long slot)
	{
		return (RegularStigmas & slot) == slot;
	}

	private static bool IsAdvancedStigma(long slot)
	{
		return (AdvancedStigmas & slot) == slot;
	}

	private static bool IsStigmaSlot(long slot)
	{
		return slot != 0 && (IsRegularStigma(slot) || IsAdvancedStigma(slot));
	}

	private static string NormalizeStigmaName(ItemTemplateSummary itemTemplate)
	{
		return (itemTemplate.GetClientName() ?? itemTemplate.Name).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
	}

	private static string GetItemName(ItemTemplateSummary itemTemplate)
	{
		return itemTemplate.GetClientName() ?? itemTemplate.Name;
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long? count = null, long? slot = null, bool? isEquipped = null, int? enchant = null)
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
			IsEquipped = isEquipped ?? item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = slot ?? item.Slot,
			Location = item.Location,
			Enchant = enchant ?? item.Enchant,
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

	private sealed record StigmaSkillRemoval(
		IReadOnlyList<PlayerSkill> RemovedSkills,
		IReadOnlyList<string> RemovedSkillNames,
		IReadOnlyList<StigmaHiddenSkillDeleteMessage> HiddenSkillDeleteMessages);

	private sealed record StigmaLinkedSkillRemoval(
		IReadOnlyList<PlayerSkill> RemovedSkills,
		IReadOnlyList<StigmaHiddenSkillDeleteMessage> HiddenSkillDeleteMessages);

	private sealed record ItemCountMutation(InventoryItem? UpdatedItem, int? DeletedObjectId);
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
	IReadOnlyList<StigmaHiddenSkillDeleteMessage> HiddenSkillDeleteMessages,
	InventoryItem? KinahItemUpdate)
{
	public static StigmaEquipResult Success(
		IReadOnlyList<PlayerSkill> skills,
		IReadOnlyList<PlayerSkill> addedSkills,
		IReadOnlyList<PlayerSkill> removedSkills,
		IReadOnlyList<string> removedSkillNames,
		IReadOnlyList<StigmaHiddenSkillDeleteMessage> hiddenSkillDeleteMessages,
		InventoryItem? kinahItemUpdate)
	{
		return new StigmaEquipResult(
			true,
			StigmaEquipFailure.None,
			skills,
			addedSkills,
			removedSkills,
			removedSkillNames,
			hiddenSkillDeleteMessages,
			kinahItemUpdate);
	}

	public static StigmaEquipResult Failed(StigmaEquipFailure failure)
	{
		return new StigmaEquipResult(
			false,
			failure,
			Array.Empty<PlayerSkill>(),
			Array.Empty<PlayerSkill>(),
			Array.Empty<PlayerSkill>(),
			Array.Empty<string>(),
			Array.Empty<StigmaHiddenSkillDeleteMessage>(),
			null);
	}
}

public sealed record StigmaUnequipResult(
	IReadOnlyList<PlayerSkill> Skills,
	IReadOnlyList<PlayerSkill> RemovedSkills,
	IReadOnlyList<string> RemovedSkillNames,
	IReadOnlyList<StigmaHiddenSkillDeleteMessage> HiddenSkillDeleteMessages)
{
	public static StigmaUnequipResult Success(
		IReadOnlyList<PlayerSkill> skills,
		IReadOnlyList<PlayerSkill> removedSkills,
		IReadOnlyList<string> removedSkillNames,
		IReadOnlyList<StigmaHiddenSkillDeleteMessage> hiddenSkillDeleteMessages)
	{
		return new StigmaUnequipResult(skills, removedSkills, removedSkillNames, hiddenSkillDeleteMessages);
	}
}

public sealed record StigmaHiddenSkillDeleteMessage(string? FirstSkillName, int SkillLevel, string? SecondSkillName);

public sealed record StigmaAutoLearnResult(
	bool Changed,
	IReadOnlyList<PlayerSkill> Skills,
	IReadOnlyList<PlayerSkill> AddedSkills)
{
	public static StigmaAutoLearnResult NoChange(IReadOnlyList<PlayerSkill> skills)
	{
		return new StigmaAutoLearnResult(false, skills, Array.Empty<PlayerSkill>());
	}
}

public sealed record StigmaLoginResult(
	bool Changed,
	IReadOnlyList<InventoryItem> InventoryItems,
	IReadOnlyList<InventoryItem> PersistedItems,
	IReadOnlyList<PlayerSkill> Skills,
	IReadOnlyList<PlayerSkill> AddedSkills)
{
	public static StigmaLoginResult NoChange(Player player)
	{
		return new StigmaLoginResult(false, player.InventoryItems, Array.Empty<InventoryItem>(), player.Skills, Array.Empty<PlayerSkill>());
	}
}

public enum StigmaChargeResult
{
	Invalid,
	Success,
}

public sealed record StigmaChargePlan(
	StigmaChargeResult Result,
	bool EnchantSucceeded,
	string ItemName,
	IReadOnlyList<InventoryItem> InventoryItems,
	InventoryItem? TargetItemUpdate,
	int? DeletedTargetItemObjectId,
	InventoryItem? SourceItemUpdate,
	int? DeletedSourceItemObjectId,
	IReadOnlyList<PlayerSkill> Skills,
	IReadOnlyList<PlayerSkill> AddedSkills,
	IReadOnlyList<PlayerSkill> RemovedSkills,
	IReadOnlyList<StigmaHiddenSkillDeleteMessage> HiddenSkillDeleteMessages)
{
	public static StigmaChargePlan Invalid()
	{
		return new StigmaChargePlan(
			StigmaChargeResult.Invalid,
			EnchantSucceeded: false,
			ItemName: string.Empty,
			InventoryItems: Array.Empty<InventoryItem>(),
			TargetItemUpdate: null,
			DeletedTargetItemObjectId: null,
			SourceItemUpdate: null,
			DeletedSourceItemObjectId: null,
			Skills: Array.Empty<PlayerSkill>(),
			AddedSkills: Array.Empty<PlayerSkill>(),
			RemovedSkills: Array.Empty<PlayerSkill>(),
			HiddenSkillDeleteMessages: Array.Empty<StigmaHiddenSkillDeleteMessage>());
	}
}
