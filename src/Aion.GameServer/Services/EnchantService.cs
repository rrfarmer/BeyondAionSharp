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
	private const int EnchantOrSupplementFamily = 166;
	private const int ManastoneFamily = 167;
	private const int MaxBasicManastones = 6;
	private const string EnchantmentItemGroup = "ENCHANTMENT";
	private const string ManastoneItemGroup = "MANASTONE";
	private const string SpecialManastoneItemGroup = "SPECIAL_MANASTONE";

	public static ManastoneSocketPlan CreateSocketManastonePlan(
		Player player,
		int targetItemObjectId,
		int manastoneObjectId,
		int targetFusedSlot,
		ItemTemplateTable itemTemplates,
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
		var socketSucceeded = IsSocketManastoneSuccess(
			player,
			sourceTemplate,
			targetItem,
			targetTemplate,
			useFusionSlots,
			itemTemplates,
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

		var sourceMutation = DecreaseItemCount(sourceItem);
		ReplaceInventoryItem(inventoryItems, targetItemUpdate);
		ApplySourceMutation(inventoryItems, sourceMutation);

		return new ManastoneSocketPlan(
			ManastoneSocketFailure.None,
			targetName,
			inventoryItems,
			targetItemUpdate,
			sourceMutation.UpdatedItem,
			sourceMutation.DeletedObjectId,
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

	private static bool IsSocketManastoneSuccess(
		Player player,
		ItemTemplateSummary manastoneTemplate,
		InventoryItem targetItem,
		ItemTemplateSummary targetTemplate,
		bool useFusionSlots,
		ItemTemplateTable itemTemplates,
		IReadOnlyList<float>? manastoneChances,
		Func<double>? rollPercent)
	{
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

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem update)
	{
		var index = items.FindIndex(item => item.ObjectId == update.ObjectId);
		if (index >= 0)
			items[index] = update;
		else
			items.Add(update);
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long? count = null, bool? isAmplified = null)
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
			IsAmplified = isAmplified ?? item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}

	private static ItemCountMutation DecreaseItemCount(InventoryItem item)
	{
		return item.Count > 1
			? new ItemCountMutation(CopyInventoryItem(item, count: item.Count - 1), null)
			: new ItemCountMutation(null, item.ObjectId);
	}

	private sealed record ItemCountMutation(InventoryItem? UpdatedItem, int? DeletedObjectId);
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
}

public sealed record ManastoneSocketPlan(
	ManastoneSocketFailure Failure,
	string ItemName,
	IReadOnlyList<InventoryItem> InventoryItems,
	InventoryItem? TargetItemUpdate,
	InventoryItem? SourceItemUpdate,
	int? DeletedSourceItemObjectId,
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
			AddedStone: null,
			AddedCategory: 0,
			SocketSucceeded: false,
			RefreshStats: false);
	}
}
