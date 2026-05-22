using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ExpExtractService
{
	private const int CubeStorageId = 0;

	public const int UsageDelayMilliseconds = 5000;

	public static ExpExtractValidation Validate(Player player, ItemTemplateSummary sourceTemplate, StaticData staticData)
	{
		// Java parity: model/templates/item/actions/ExpExtractAction.canAct.
		var action = sourceTemplate.ExpExtractAction;
		if (action == null)
			return ExpExtractValidation.Fail(ExpExtractFailure.MissingAction);

		var rewardTemplate = staticData.ItemTemplates.GetItemTemplate(action.ItemId);
		if (rewardTemplate == null)
			return ExpExtractValidation.Fail(ExpExtractFailure.MissingRewardTemplate);

		if (!InventoryCapacity.HasFreeCubeSlot(player, staticData.ItemTemplates))
			return ExpExtractValidation.Fail(ExpExtractFailure.InventoryFull);

		var level = Math.Max(1, staticData.PlayerExperienceTable.GetLevelForExp(player.Exp));
		var startExp = staticData.PlayerExperienceTable.GetStartExpForLevel(level);
		var requiredExp = GetRequiredExp(action, staticData.PlayerExperienceTable, level);
		var newExp = player.Exp - requiredExp;
		if (newExp < startExp)
			return ExpExtractValidation.Fail(ExpExtractFailure.NotEnoughExp);

		return ExpExtractValidation.Success(action, rewardTemplate, requiredExp, newExp);
	}

	public static ExpExtractMutationPlan CreateMutationPlan(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		ItemTemplateSummary sourceTemplate,
		ExpExtractValidation validation,
		ItemTemplateTable itemTemplates,
		Func<int> nextObjectId)
	{
		// Java parity: ExpExtractAction.run decreases by item id, sets exp, then ItemService.addItem(reward, 1).
		if (!validation.Succeeded || validation.RewardTemplate == null)
			return ExpExtractMutationPlan.Fail(validation.Failure);

		var sourceItem = inventoryItems.FirstOrDefault(item => item.ItemId == sourceTemplate.TemplateId && item.Location == CubeStorageId && !item.IsEquipped);
		if (sourceItem == null)
			return ExpExtractMutationPlan.Fail(ExpExtractFailure.MissingSourceItem);

		var workingItems = inventoryItems.ToList();
		InventoryItem? sourceItemUpdate = null;
		int? deletedSourceItemObjectId = null;
		if (sourceItem.Count > 1)
		{
			sourceItemUpdate = CopyInventoryItem(sourceItem, sourceItem.Count - 1);
			ReplaceInventoryItem(workingItems, sourceItemUpdate);
		}
		else
		{
			deletedSourceItemObjectId = sourceItem.ObjectId;
			workingItems.RemoveAll(item => item.ObjectId == sourceItem.ObjectId);
		}

		var addPlan = InventoryAddService.CreateAddItemPlan(
			player,
			workingItems,
			validation.RewardTemplate,
			1,
			nextObjectId,
			allowInventoryOverflow: false,
			itemTemplates);

		return ExpExtractMutationPlan.Success(
			sourceItemUpdate,
			deletedSourceItemObjectId,
			addPlan.Succeeded ? addPlan.UpdatedItems : Array.Empty<InventoryItem>(),
			addPlan.Succeeded ? addPlan.AddedItems : Array.Empty<InventoryItem>(),
			addPlan.RemainingCount);
	}

	private static long GetRequiredExp(ItemExpExtractActionInfo action, PlayerExperienceTable experienceTable, int level)
	{
		if (!action.IsPercent)
			return action.Cost;

		return Math.Max(1, GetExpNeed(experienceTable, level) * action.Cost / 100L);
	}

	private static long GetExpNeed(PlayerExperienceTable experienceTable, int level)
	{
		if (level <= 0 || level >= experienceTable.MaxLevel)
			return 0;

		return experienceTable.GetStartExpForLevel(level + 1) - experienceTable.GetStartExpForLevel(level);
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem replacement)
	{
		var index = items.FindIndex(item => item.ObjectId == replacement.ObjectId);
		if (index >= 0)
			items[index] = replacement;
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
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
}

public sealed record ExpExtractValidation(
	bool Succeeded,
	ExpExtractFailure Failure,
	ItemExpExtractActionInfo? Action,
	ItemTemplateSummary? RewardTemplate,
	long RequiredExp,
	long NewExp)
{
	public static ExpExtractValidation Success(
		ItemExpExtractActionInfo action,
		ItemTemplateSummary rewardTemplate,
		long requiredExp,
		long newExp)
	{
		return new ExpExtractValidation(true, ExpExtractFailure.None, action, rewardTemplate, requiredExp, newExp);
	}

	public static ExpExtractValidation Fail(ExpExtractFailure failure)
	{
		return new ExpExtractValidation(false, failure, null, null, 0, 0);
	}
}

public sealed record ExpExtractMutationPlan(
	bool Succeeded,
	ExpExtractFailure Failure,
	InventoryItem? SourceItemUpdate,
	int? DeletedSourceItemObjectId,
	IReadOnlyList<InventoryItem> UpdatedRewardItems,
	IReadOnlyList<InventoryItem> AddedRewardItems,
	long RewardRemainingCount)
{
	public bool RewardSucceeded => RewardRemainingCount == 0;

	public static ExpExtractMutationPlan Success(
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		long rewardRemainingCount)
	{
		return new ExpExtractMutationPlan(
			true,
			ExpExtractFailure.None,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			updatedRewardItems,
			addedRewardItems,
			rewardRemainingCount);
	}

	public static ExpExtractMutationPlan Fail(ExpExtractFailure failure)
	{
		return new ExpExtractMutationPlan(
			false,
			failure,
			null,
			null,
			Array.Empty<InventoryItem>(),
			Array.Empty<InventoryItem>(),
			0);
	}
}

public enum ExpExtractFailure
{
	None,
	MissingAction,
	MissingRewardTemplate,
	InventoryFull,
	NotEnoughExp,
	MissingSourceItem,
}
