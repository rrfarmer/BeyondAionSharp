using System.Globalization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class EnchantService
{
	private const int CubeStorageId = 0;
	private const int ExceedEnchantMaterialItemId = 166500002;
	private const int ExceedEnchantAlternateMaterialItemId = 166500005;
	private const int TemplateIdFamilyDivisor = 1_000_000;
	private const int SupplementTemplateIdFamilyDivisor = 100_000;
	private const int EnchantOrSupplementFamily = 166;
	private const int ManastoneFamily = 167;
	private const int SupplementFamily = 1661;
	private const int MaxBasicManastones = 6;
	private const int MaxEnchantLevel = 255;
	private const int AlphaStoneBaseLevel = 20;
	private const string EnchantmentItemGroup = "ENCHANTMENT";
	private const string ManastoneItemGroup = "MANASTONE";
	private const string SpecialManastoneItemGroup = "SPECIAL_MANASTONE";

	public static EnchantItemPlan CreateEnchantItemPlan(
		Player player,
		int targetItemObjectId,
		int enchantmentStoneObjectId,
		ItemTemplateTable itemTemplates,
		int supplementObjectId = 0,
		IReadOnlyList<float>? enchantmentStoneBaseChances = null,
		IReadOnlyList<float>? enchantmentStoneAmplifiedChances = null,
		Func<double>? rollPercent = null,
		Func<double>? criticalRollPercent = null)
	{
		// Java parity: services/EnchantService.enchantItem + enchantItemAct.
		var inventoryItems = player.InventoryItems.ToList();
		var sourceItem = FindCubeItem(inventoryItems, enchantmentStoneObjectId);
		if (sourceItem == null)
			return EnchantItemPlan.Failed(EnchantItemFailure.NoSourceItem);

		var targetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == targetItemObjectId);
		if (targetItem == null)
			return EnchantItemPlan.Failed(EnchantItemFailure.NoTargetItem);

		var sourceTemplate = itemTemplates.GetItemTemplate(sourceItem.ItemId);
		var targetTemplate = itemTemplates.GetItemTemplate(targetItem.ItemId);
		if (sourceTemplate == null
			|| targetTemplate == null
			|| !string.Equals(sourceTemplate.ItemGroup, EnchantmentItemGroup, StringComparison.Ordinal)
			|| !CanEnchantItemActionAct(sourceTemplate, targetTemplate))
		{
			return EnchantItemPlan.Failed(EnchantItemFailure.CannotAct);
		}

		var targetName = GetItemName(targetItem, itemTemplates);
		var sourceName = GetItemName(sourceItem, itemTemplates);
		if (targetTemplate.IsNoEnchant)
			return EnchantItemPlan.Failed(EnchantItemFailure.CannotAct, targetName, sourceName);

		if (targetTemplate.MaxEnchantLevel == 0 && !targetTemplate.CanExceedEnchant)
			return EnchantItemPlan.Failed(EnchantItemFailure.CannotEnchant, targetName, sourceName);

		if ((!targetItem.IsAmplified && targetItem.Enchant >= targetTemplate.MaxEnchantLevel)
			|| targetItem.Enchant >= MaxEnchantLevel)
		{
			return EnchantItemPlan.Failed(EnchantItemFailure.CannotEnchantMoreTime, targetName, sourceName);
		}

		var enchantmentStone = EnchantmentStoneInfo.GetByItemId(sourceItem.ItemId);
		if (enchantmentStone == null)
			return EnchantItemPlan.Failed(EnchantItemFailure.CannotAct, targetName, sourceName);

		if (targetItem.IsAmplified && enchantmentStone.Kind != EnchantmentStoneKind.Omega)
			return EnchantItemPlan.Failed(EnchantItemFailure.AmplifiedNeedsOmega, targetName, sourceName);

		var supplementItem = supplementObjectId == 0 ? null : FindCubeItem(inventoryItems, supplementObjectId);
		if (supplementItem != null && supplementItem.ItemId / SupplementTemplateIdFamilyDivisor != SupplementFamily)
			return EnchantItemPlan.Failed(EnchantItemFailure.InvalidSupplementItem, targetName, sourceName);

		var supplementTemplate = supplementItem == null ? null : itemTemplates.GetItemTemplate(supplementItem.ItemId);
		if (supplementItem != null && supplementTemplate == null)
			return EnchantItemPlan.Failed(EnchantItemFailure.InvalidSupplementItem, targetName, sourceName);

		if (supplementTemplate != null && !CanUseSupplementWithTarget(supplementTemplate, targetTemplate))
			return EnchantItemPlan.Failed(EnchantItemFailure.WrongSupplementLevel, targetName, sourceName);

		var enchantSucceeded = IsEnchantItemSuccess(
			player,
			sourceItem,
			sourceTemplate,
			targetItem,
			targetTemplate,
			enchantmentStone,
			supplementTemplate,
			inventoryItems,
			out var supplementUseCount,
			enchantmentStoneBaseChances,
			enchantmentStoneAmplifiedChances,
			rollPercent);

		var maxEnchant = targetTemplate.MaxEnchantLevel + targetItem.EnchantBonus;
		var currentEnchant = targetItem.Enchant;
		var isAmplified = targetItem.IsAmplified;
		var addLevel = GetEnchantAddLevel(targetItem, criticalRollPercent);
		if (enchantSucceeded)
		{
			if (!targetItem.IsAmplified && currentEnchant + addLevel > maxEnchant)
				currentEnchant = maxEnchant;
			else
				currentEnchant += addLevel;
		}
		else if (targetItem.IsAmplified)
		{
			currentEnchant = maxEnchant;
			isAmplified = false;
		}
		else if (currentEnchant > 10 && maxEnchant > 10)
		{
			currentEnchant = 10;
		}
		else if (currentEnchant > 0)
		{
			currentEnchant--;
		}

		var enchantCap = isAmplified ? MaxEnchantLevel : maxEnchant;
		var targetItemUpdate = CopyInventoryItem(
			targetItem,
			enchant: Math.Min(currentEnchant, enchantCap),
			isAmplified: isAmplified);
		int? deletedTargetItemObjectId = null;
		var targetDestroyed = false;
		if (!enchantSucceeded && targetTemplate.EnchantType > 0)
		{
			var targetMutation = DecreaseItemCount(targetItemUpdate);
			targetItemUpdate = targetMutation.UpdatedItem;
			deletedTargetItemObjectId = targetMutation.DeletedObjectId;
			targetDestroyed = true;
		}
		else if (!enchantSucceeded)
		{
			targetItemUpdate = RemoveRemainingTuningCountIfPossible(targetItemUpdate, targetTemplate);
		}

		var sourceMutation = DecreaseItemCount(sourceItem);
		var supplementMutation = supplementTemplate == null || supplementUseCount <= 0
			? SupplementCountMutation.Empty
			: DecreaseSupplementCount(inventoryItems, supplementTemplate.TemplateId, supplementUseCount);

		if (targetItemUpdate != null)
			ReplaceInventoryItem(inventoryItems, targetItemUpdate);
		else if (deletedTargetItemObjectId.HasValue)
			inventoryItems.RemoveAll(item => item.ObjectId == deletedTargetItemObjectId.Value);
		ApplySupplementMutation(inventoryItems, supplementMutation);
		ApplySourceMutation(inventoryItems, sourceMutation);

		return new EnchantItemPlan(
			EnchantItemFailure.None,
			targetName,
			sourceName,
			inventoryItems,
			targetItemUpdate,
			deletedTargetItemObjectId,
			sourceMutation.UpdatedItem,
			sourceMutation.DeletedObjectId,
			supplementMutation.UpdatedItems,
			supplementMutation.DeletedObjectIds,
			enchantSucceeded,
			targetDestroyed,
			targetItem.IsEquipped && !targetDestroyed,
			Math.Min(currentEnchant, enchantCap));
	}

	public static ManastoneSocketPlan CreateSocketManastonePlan(
		Player player,
		int targetItemObjectId,
		int manastoneObjectId,
		int targetFusedSlot,
		ItemTemplateTable itemTemplates,
		int supplementObjectId = 0,
		IReadOnlyList<float>? manastoneChances = null,
		Func<double>? rollPercent = null)
	{
		// Java parity: services/EnchantService.socketManastone + socketManastoneAct.
		var inventoryItems = player.InventoryItems.ToList();
		var sourceItem = FindCubeItem(inventoryItems, manastoneObjectId);
		if (sourceItem == null)
			return ManastoneSocketPlan.Failed(ManastoneSocketFailure.NoSourceItem);

		var targetItem = inventoryItems.FirstOrDefault(item =>
			item.ObjectId == targetItemObjectId
			&& item.Location == CubeStorageId);
		if (targetItem == null)
			return ManastoneSocketPlan.Failed(ManastoneSocketFailure.NoTargetItem);

		var sourceTemplate = itemTemplates.GetItemTemplate(sourceItem.ItemId);
		var targetTemplate = itemTemplates.GetItemTemplate(targetItem.ItemId);
		if (sourceTemplate == null || targetTemplate == null || !CanEnchantItemActionAct(sourceTemplate, targetTemplate))
			return ManastoneSocketPlan.Failed(ManastoneSocketFailure.CannotAct);

		if (!IsManastone(sourceTemplate) || string.Equals(sourceTemplate.ItemGroup, EnchantmentItemGroup, StringComparison.Ordinal))
			return ManastoneSocketPlan.Failed(ManastoneSocketFailure.CannotAct);

		var useFusionSlots = targetFusedSlot != 1;
		var targetName = GetItemName(targetItem, itemTemplates);
		var supplementItem = supplementObjectId == 0 ? null : FindCubeItem(inventoryItems, supplementObjectId);
		if (supplementItem != null && supplementItem.ItemId / SupplementTemplateIdFamilyDivisor != SupplementFamily)
			return ManastoneSocketPlan.Failed(ManastoneSocketFailure.InvalidSupplementItem, targetName);

		var supplementTemplate = supplementItem == null ? null : itemTemplates.GetItemTemplate(supplementItem.ItemId);
		if (supplementItem != null && supplementTemplate == null)
			return ManastoneSocketPlan.Failed(ManastoneSocketFailure.InvalidSupplementItem, targetName);

		if (supplementTemplate != null && !CanUseSupplementWithTarget(supplementTemplate, targetTemplate))
			return ManastoneSocketPlan.Failed(ManastoneSocketFailure.WrongSupplementLevel, targetName);

		var socketSucceeded = IsSocketManastoneSuccess(
			player,
			sourceTemplate,
			targetItem,
			targetTemplate,
			useFusionSlots,
			itemTemplates,
			inventoryItems,
			supplementTemplate,
			out var supplementUseCount,
			manastoneChances,
			rollPercent);

		var targetItemUpdate = CopyInventoryItem(targetItem);
		ItemStoneSocket? addedStone = null;
		var addedCategory = useFusionSlots ? 2 : 0;
		if (socketSucceeded)
		{
			var addPlan = ItemSocketService.CreateAddManastonePlan(targetItem, sourceItem.ItemId, useFusionSlots, itemTemplates);
			if (addPlan.ItemUpdate != null)
			{
				targetItemUpdate = addPlan.ItemUpdate;
				addedStone = addPlan.AddedStone;
				addedCategory = addPlan.AddedCategory;
			}
		}
		else
		{
			targetItemUpdate = RemoveRemainingTuningCountIfPossible(targetItemUpdate, targetTemplate);
		}

		var sourceMutation = DecreaseItemCount(sourceItem);
		var supplementMutation = supplementTemplate == null || supplementUseCount <= 0
			? SupplementCountMutation.Empty
			: DecreaseSupplementCount(inventoryItems, supplementTemplate.TemplateId, supplementUseCount);
		ReplaceInventoryItem(inventoryItems, targetItemUpdate);
		ApplySupplementMutation(inventoryItems, supplementMutation);
		ApplySourceMutation(inventoryItems, sourceMutation);

		return new ManastoneSocketPlan(
			ManastoneSocketFailure.None,
			targetName,
			inventoryItems,
			targetItemUpdate,
			sourceMutation.UpdatedItem,
			sourceMutation.DeletedObjectId,
			supplementMutation.UpdatedItems,
			supplementMutation.DeletedObjectIds,
			addedStone,
			addedCategory,
			socketSucceeded,
			targetItem.IsEquipped);
	}

	public static AmplificationPlan CreateAmplificationPlan(
		Player player,
		int targetItemObjectId,
		int materialObjectId,
		int toolObjectId,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: services/EnchantService.amplifyItem.
		var inventoryItems = player.InventoryItems.ToList();
		var targetItem = inventoryItems.FirstOrDefault(item => item.ObjectId == targetItemObjectId);
		var materialItem = FindCubeItem(inventoryItems, materialObjectId);
		var toolItem = FindCubeItem(inventoryItems, toolObjectId);
		if (targetItem == null
			|| materialItem == null
			|| toolItem == null
			|| materialItem.ObjectId == toolItem.ObjectId
			|| materialItem.ObjectId == targetItem.ObjectId
			|| toolItem.ObjectId == targetItem.ObjectId)
		{
			return AmplificationPlan.Failed(AmplificationFailure.NoTargetItem);
		}

		var targetTemplate = itemTemplates.GetItemTemplate(targetItem.ItemId);
		if (targetTemplate == null)
			return AmplificationPlan.Failed(AmplificationFailure.NoTargetItem);

		var targetName = GetItemName(targetItem, itemTemplates);
		if (targetItem.IsAmplified)
			return AmplificationPlan.Failed(AmplificationFailure.AlreadyAmplified, targetName);

		if (!targetTemplate.CanExceedEnchant)
			return AmplificationPlan.Failed(AmplificationFailure.CannotAmplify, targetName);

		var maxEnchantLevel = targetTemplate.MaxEnchantLevel + targetItem.EnchantBonus;
		if (targetItem.Enchant < maxEnchantLevel)
			return AmplificationPlan.Failed(AmplificationFailure.NeedsMaxEnchant, targetName);

		if (targetItem.ItemId != materialItem.ItemId
			&& materialItem.ItemId != ExceedEnchantMaterialItemId
			&& materialItem.ItemId != ExceedEnchantAlternateMaterialItemId)
		{
			return AmplificationPlan.Failed(AmplificationFailure.NoTargetItem);
		}

		var targetUpdate = CopyInventoryItem(targetItem, isAmplified: true);
		var materialMutation = DecreaseItemCount(materialItem);
		var toolMutation = DecreaseItemCount(toolItem);

		ReplaceInventoryItem(inventoryItems, targetUpdate);
		ApplySourceMutation(inventoryItems, materialMutation);
		ApplySourceMutation(inventoryItems, toolMutation);

		return new AmplificationPlan(
			AmplificationFailure.None,
			targetName,
			inventoryItems,
			targetUpdate,
			materialMutation.UpdatedItem,
			materialMutation.DeletedObjectId,
			toolMutation.UpdatedItem,
			toolMutation.DeletedObjectId);
	}

	private static InventoryItem? FindCubeItem(IReadOnlyList<InventoryItem> inventoryItems, int objectId)
	{
		return inventoryItems.FirstOrDefault(item =>
			item.ObjectId == objectId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped);
	}

	private static bool CanEnchantItemActionAct(ItemTemplateSummary sourceTemplate, ItemTemplateSummary targetTemplate)
	{
		// Java parity: model/templates/item/actions/EnchantItemAction.canAct final item family check.
		var sourceFamily = sourceTemplate.TemplateId / TemplateIdFamilyDivisor;
		var targetFamily = targetTemplate.TemplateId / TemplateIdFamilyDivisor;
		return (sourceFamily == ManastoneFamily || sourceFamily == EnchantOrSupplementFamily)
			&& targetFamily < 120;
	}

	private static bool IsManastone(ItemTemplateSummary template)
	{
		return string.Equals(template.ItemGroup, ManastoneItemGroup, StringComparison.Ordinal)
			|| string.Equals(template.ItemGroup, SpecialManastoneItemGroup, StringComparison.Ordinal);
	}

	private static bool CanUseSupplementWithTarget(ItemTemplateSummary supplementTemplate, ItemTemplateSummary targetTemplate)
	{
		// Java parity: model/templates/item/actions/EnchantItemAction.checkSupplementLevel.
		if (string.Equals(supplementTemplate.ItemGroup, EnchantmentItemGroup, StringComparison.Ordinal))
			return true;

		var action = supplementTemplate.EnchantAction;
		var minLevel = action?.MinLevel is > 0 ? action.MinLevel : targetTemplate.Level;
		var maxLevel = action?.MaxLevel is > 0 ? action.MaxLevel : targetTemplate.Level;
		return minLevel <= targetTemplate.Level && maxLevel >= targetTemplate.Level;
	}

	private static bool IsEnchantItemSuccess(
		Player player,
		InventoryItem enchantmentStoneItem,
		ItemTemplateSummary enchantmentStoneTemplate,
		InventoryItem targetItem,
		ItemTemplateSummary targetTemplate,
		EnchantmentStoneInfo enchantmentStone,
		ItemTemplateSummary? supplementTemplate,
		IReadOnlyList<InventoryItem> inventoryItems,
		out int supplementUseCount,
		IReadOnlyList<float>? enchantmentStoneBaseChances,
		IReadOnlyList<float>? enchantmentStoneAmplifiedChances,
		Func<double>? rollPercent)
	{
		// Java parity: services/EnchantService.enchantItem.
		supplementUseCount = 0;
		float successChance;
		if (targetItem.IsAmplified)
		{
			successChance = GetMembershipRate(player, enchantmentStoneAmplifiedChances ?? [50f, 50f]);
		}
		else
		{
			successChance = GetMembershipRate(player, enchantmentStoneBaseChances ?? [65f, 65f]);
			var itemLevel = Math.Max(targetTemplate.Level, AlphaStoneBaseLevel);
			successChance += enchantmentStone.BaseLevel - itemLevel;
			successChance += (enchantmentStone.BaseQualityId - GetQualityId(targetTemplate.Quality)) * 5;
			if (targetItem.Enchant == 0)
				successChance *= 1.2f;
			else if (targetItem.Enchant < 5)
				successChance *= 1.1f;
			else if (targetItem.Enchant >= 10)
				successChance *= 0.9f;

			if (successChance >= 80)
				successChance = 80;

			if (supplementTemplate != null)
			{
				var supplementAction = supplementTemplate.EnchantAction;
				if (supplementAction?.ManastoneOnly == true)
					return false;

				if (supplementAction != null)
					successChance += supplementAction.Chance;

				supplementUseCount = enchantmentStoneTemplate.EnchantAction?.Count ?? 1;
				if (targetItem.Enchant >= 10)
					supplementUseCount *= 2;

				var supplementCount = inventoryItems
					.Where(item =>
						item.ItemId == supplementTemplate.TemplateId
						&& item.Location == CubeStorageId
						&& !item.IsEquipped)
					.Sum(item => item.Count);
				if (supplementCount < supplementUseCount)
				{
					supplementUseCount = 0;
					return false;
				}

				if (successChance >= 95)
					successChance = 95;
			}
		}

		var roll = rollPercent?.Invoke() ?? Random.Shared.NextDouble() * 100d;
		return roll < successChance;
	}

	private static int GetEnchantAddLevel(InventoryItem targetItem, Func<double>? criticalRollPercent)
	{
		// Java parity: services/EnchantService.enchantItemAct +1/+2/+3 crit modifier.
		if (targetItem.Enchant >= 20)
			return 1;

		var roll = criticalRollPercent?.Invoke() ?? Random.Shared.NextDouble() * 100d;
		if (roll < 5)
			return 3;

		return roll < 10 ? 2 : 1;
	}

	private static bool IsSocketManastoneSuccess(
		Player player,
		ItemTemplateSummary manastoneTemplate,
		InventoryItem targetItem,
		ItemTemplateSummary targetTemplate,
		bool useFusionSlots,
		ItemTemplateTable itemTemplates,
		IReadOnlyList<InventoryItem> inventoryItems,
		ItemTemplateSummary? supplementTemplate,
		out int supplementUseCount,
		IReadOnlyList<float>? manastoneChances,
		Func<double>? rollPercent)
	{
		supplementUseCount = 0;
		var targetItemLevel = targetTemplate.Level;
		if (useFusionSlots)
		{
			var fusionTemplate = targetItem.FusionedItem == 0 ? null : itemTemplates.GetItemTemplate(targetItem.FusionedItem);
			if (fusionTemplate == null)
				return false;

			targetItemLevel = fusionTemplate.Level;
		}

		var slotLevel = (int)(10 * Math.Ceiling((targetItemLevel + 10) / 10d));
		if (manastoneTemplate.Level > slotLevel)
			return false;

		var stoneCount = useFusionSlots ? targetItem.FusionStones.Count : targetItem.ManaStones.Count;
		var socketCount = GetSocketCount(targetItem, useFusionSlots, itemTemplates, targetTemplate);
		if (stoneCount >= socketCount)
			return false;

		var successChance = GetMembershipRate(player, manastoneChances ?? [75f, 75f]);
		if (GetQualityId(manastoneTemplate.Quality) >= 2)
			successChance *= 0.8f;

		var socketDiff = stoneCount * 1.25f + 1.75f;
		successChance += (slotLevel - manastoneTemplate.Level) / socketDiff;
		if (supplementTemplate != null)
		{
			var manastoneAction = manastoneTemplate.EnchantAction;
			if (manastoneAction != null)
				supplementUseCount = manastoneAction.Count;

			var supplementAction = supplementTemplate.EnchantAction;
			var isManastoneOnly = false;
			if (supplementAction != null)
			{
				successChance += supplementAction.Chance;
				isManastoneOnly = supplementAction.ManastoneOnly;
			}

			if (isManastoneOnly)
				supplementUseCount = 1;
			else if (stoneCount > 0)
				supplementUseCount *= stoneCount + 1;

			var supplementCount = inventoryItems
				.Where(item =>
					item.ItemId == supplementTemplate.TemplateId
					&& item.Location == CubeStorageId
					&& !item.IsEquipped)
				.Sum(item => item.Count);
			if (supplementCount < supplementUseCount)
			{
				supplementUseCount = 0;
				return false;
			}
		}

		var roll = rollPercent?.Invoke() ?? Random.Shared.NextDouble() * 100d;
		return roll < successChance;
	}

	private static int GetSocketCount(
		InventoryItem item,
		bool useFusionSlots,
		ItemTemplateTable itemTemplates,
		ItemTemplateSummary targetTemplate)
	{
		// Java parity: model/gameobjects/Item.getSockets.
		if (!targetTemplate.IsWeapon && !targetTemplate.IsArmor)
			return 0;

		if (!useFusionSlots)
			return Math.Min(targetTemplate.ManastoneSlots + item.OptionalSocket, MaxBasicManastones);

		var fusionTemplate = item.FusionedItem == 0 ? null : itemTemplates.GetItemTemplate(item.FusionedItem);
		if (fusionTemplate == null)
			return 0;

		return Math.Min(fusionTemplate.ManastoneSlots + item.OptionalFusionSocket, MaxBasicManastones);
	}

	private static float GetMembershipRate(Player player, IReadOnlyList<float> rates)
	{
		// Java parity: model/gameobjects/player/Rates.get.
		if (rates.Count == 0)
			return 1f;

		return rates[Math.Min(rates.Count - 1, player.AccountMembership)];
	}

	private static int GetQualityId(string quality)
	{
		return quality switch
		{
			"JUNK" => 0,
			"COMMON" => 1,
			"RARE" => 2,
			"LEGEND" => 3,
			"UNIQUE" => 4,
			"EPIC" => 5,
			"MYTHIC" => 6,
			_ => 1,
		};
	}

	private static string GetItemName(InventoryItem item, ItemTemplateTable itemTemplates)
	{
		var template = itemTemplates.GetItemTemplate(item.ItemId);
		return template?.GetClientName()
			?? template?.Name
			?? item.ItemId.ToString(CultureInfo.InvariantCulture);
	}

	private static void ApplySourceMutation(List<InventoryItem> inventoryItems, ItemCountMutation mutation)
	{
		if (mutation.UpdatedItem != null)
			ReplaceInventoryItem(inventoryItems, mutation.UpdatedItem);
		else if (mutation.DeletedObjectId.HasValue)
			inventoryItems.RemoveAll(item => item.ObjectId == mutation.DeletedObjectId);
	}

	private static void ApplySupplementMutation(List<InventoryItem> inventoryItems, SupplementCountMutation mutation)
	{
		foreach (var update in mutation.UpdatedItems)
			ReplaceInventoryItem(inventoryItems, update);
		foreach (var deletedObjectId in mutation.DeletedObjectIds)
			inventoryItems.RemoveAll(item => item.ObjectId == deletedObjectId);
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
		long? count = null,
		bool? isAmplified = null,
		int? tuneCount = null,
		int? enchant = null,
		int? buffSkill = null)
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
			Enchant = enchant ?? item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = tuneCount ?? item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = isAmplified ?? item.IsAmplified,
			BuffSkill = buffSkill ?? item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}

	private static InventoryItem RemoveRemainingTuningCountIfPossible(InventoryItem item, ItemTemplateSummary targetTemplate)
	{
		// Java parity: model/gameobjects/Item.removeRemainingTuningCountIfPossible during socketManastoneAct failure.
		if (item.IsIdentified && targetTemplate.MaxTuneCount > 0 && item.TuneCount != targetTemplate.MaxTuneCount)
			return CopyInventoryItem(item, tuneCount: targetTemplate.MaxTuneCount);

		return item;
	}

	private static ItemCountMutation DecreaseItemCount(InventoryItem item)
	{
		return item.Count > 1
			? new ItemCountMutation(CopyInventoryItem(item, count: item.Count - 1), null)
			: new ItemCountMutation(null, item.ObjectId);
	}

	private static SupplementCountMutation DecreaseSupplementCount(
		IReadOnlyList<InventoryItem> inventoryItems,
		int itemId,
		long count)
	{
		// Java parity: model/gameobjects/player/Player.subtractSupplements + updateSupplements.
		if (count <= 0)
			return SupplementCountMutation.Empty;

		var remaining = count;
		var updates = new List<InventoryItem>();
		var deletes = new List<int>();
		foreach (var item in inventoryItems
			.Where(candidate =>
				candidate.ItemId == itemId
				&& candidate.Location == CubeStorageId
				&& !candidate.IsEquipped)
			.OrderBy(candidate => candidate.ObjectId))
		{
			if (remaining <= 0)
				break;

			var consumed = Math.Min(item.Count, remaining);
			remaining -= consumed;
			if (item.Count == consumed)
				deletes.Add(item.ObjectId);
			else
				updates.Add(CopyInventoryItem(item, count: item.Count - consumed));
		}

		return new SupplementCountMutation(updates, deletes);
	}

	private sealed record ItemCountMutation(InventoryItem? UpdatedItem, int? DeletedObjectId);

	private sealed record SupplementCountMutation(
		IReadOnlyList<InventoryItem> UpdatedItems,
		IReadOnlyList<int> DeletedObjectIds)
	{
		public static SupplementCountMutation Empty { get; } = new(Array.Empty<InventoryItem>(), Array.Empty<int>());
	}

	private sealed record EnchantmentStoneInfo(EnchantmentStoneKind Kind, int BaseLevel, int BaseQualityId)
	{
		private static readonly EnchantmentStoneInfo Alpha = new(EnchantmentStoneKind.Alpha, 20, 2);
		private static readonly EnchantmentStoneInfo Beta = new(EnchantmentStoneKind.Beta, 40, 3);
		private static readonly EnchantmentStoneInfo Gamma = new(EnchantmentStoneKind.Gamma, 55, 4);
		private static readonly EnchantmentStoneInfo Delta = new(EnchantmentStoneKind.Delta, 60, 5);
		private static readonly EnchantmentStoneInfo Epsilon = new(EnchantmentStoneKind.Epsilon, 65, 6);
		private static readonly EnchantmentStoneInfo Omega = new(EnchantmentStoneKind.Omega, 65, 6);

		public static EnchantmentStoneInfo? GetByItemId(int itemId)
		{
			// Java parity: model/enchants/EnchantmentStone.getByItemId.
			return itemId switch
			{
				166000191 => Alpha,
				166000192 => Beta,
				166000193 => Gamma,
				166000194 => Delta,
				166000195 => Epsilon,
				166020000 or 166020001 or 166020002 or 166020003 => Omega,
				>= 166000001 and <= 166000190 when itemId > 166000100 => Epsilon,
				>= 166000001 and <= 166000190 when itemId > 166000060 => Delta,
				>= 166000001 and <= 166000190 when itemId > 166000050 => Gamma,
				>= 166000001 and <= 166000190 when itemId > 166000030 => Beta,
				>= 166000001 and <= 166000190 => Alpha,
				_ => null,
			};
		}
	}

	private enum EnchantmentStoneKind
	{
		Alpha,
		Beta,
		Gamma,
		Delta,
		Epsilon,
		Omega,
	}
}

public enum EnchantItemFailure
{
	None,
	NoSourceItem,
	NoTargetItem,
	CannotAct,
	CannotEnchant,
	CannotEnchantMoreTime,
	AmplifiedNeedsOmega,
	InvalidSupplementItem,
	WrongSupplementLevel,
}

public sealed record EnchantItemPlan(
	EnchantItemFailure Failure,
	string ItemName,
	string EnchantmentStoneName,
	IReadOnlyList<InventoryItem> InventoryItems,
	InventoryItem? TargetItemUpdate,
	int? DeletedTargetItemObjectId,
	InventoryItem? SourceItemUpdate,
	int? DeletedSourceItemObjectId,
	IReadOnlyList<InventoryItem> SupplementItemUpdates,
	IReadOnlyList<int> DeletedSupplementItemObjectIds,
	bool EnchantSucceeded,
	bool TargetDestroyed,
	bool RefreshStats,
	int NewEnchantLevel)
{
	public bool Succeeded => Failure == EnchantItemFailure.None;

	public static EnchantItemPlan Failed(EnchantItemFailure failure, string itemName = "", string enchantmentStoneName = "")
	{
		return new EnchantItemPlan(
			failure,
			itemName,
			enchantmentStoneName,
			Array.Empty<InventoryItem>(),
			TargetItemUpdate: null,
			DeletedTargetItemObjectId: null,
			SourceItemUpdate: null,
			DeletedSourceItemObjectId: null,
			SupplementItemUpdates: Array.Empty<InventoryItem>(),
			DeletedSupplementItemObjectIds: Array.Empty<int>(),
			EnchantSucceeded: false,
			TargetDestroyed: false,
			RefreshStats: false,
			NewEnchantLevel: 0);
	}
}

public enum AmplificationFailure
{
	None,
	NoTargetItem,
	AlreadyAmplified,
	CannotAmplify,
	NeedsMaxEnchant,
}

public sealed record AmplificationPlan(
	AmplificationFailure Failure,
	string ItemName,
	IReadOnlyList<InventoryItem> InventoryItems,
	InventoryItem? TargetItemUpdate,
	InventoryItem? MaterialItemUpdate,
	int? DeletedMaterialItemObjectId,
	InventoryItem? ToolItemUpdate,
	int? DeletedToolItemObjectId)
{
	public bool Succeeded => Failure == AmplificationFailure.None;

	public static AmplificationPlan Failed(AmplificationFailure failure, string itemName = "")
	{
		return new AmplificationPlan(
			failure,
			itemName,
			Array.Empty<InventoryItem>(),
			TargetItemUpdate: null,
			MaterialItemUpdate: null,
			DeletedMaterialItemObjectId: null,
			ToolItemUpdate: null,
			DeletedToolItemObjectId: null);
	}
}

public enum ManastoneSocketFailure
{
	None,
	NoSourceItem,
	NoTargetItem,
	CannotAct,
	InvalidSupplementItem,
	WrongSupplementLevel,
}

public sealed record ManastoneSocketPlan(
	ManastoneSocketFailure Failure,
	string ItemName,
	IReadOnlyList<InventoryItem> InventoryItems,
	InventoryItem? TargetItemUpdate,
	InventoryItem? SourceItemUpdate,
	int? DeletedSourceItemObjectId,
	IReadOnlyList<InventoryItem> SupplementItemUpdates,
	IReadOnlyList<int> DeletedSupplementItemObjectIds,
	ItemStoneSocket? AddedStone,
	int AddedCategory,
	bool SocketSucceeded,
	bool RefreshStats)
{
	public bool Succeeded => Failure == ManastoneSocketFailure.None;

	public static ManastoneSocketPlan Failed(ManastoneSocketFailure failure, string itemName = "")
	{
		return new ManastoneSocketPlan(
			failure,
			itemName,
			Array.Empty<InventoryItem>(),
			TargetItemUpdate: null,
			SourceItemUpdate: null,
			DeletedSourceItemObjectId: null,
			SupplementItemUpdates: Array.Empty<InventoryItem>(),
			DeletedSupplementItemObjectIds: Array.Empty<int>(),
			AddedStone: null,
			AddedCategory: 0,
			SocketSucceeded: false,
			RefreshStats: false);
	}
}
