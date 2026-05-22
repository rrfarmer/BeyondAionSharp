using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class IdianPolishService
{
	public const int FullPolishCharge = 1_000_000;
	public const int LowPolishChargeThreshold = 300_000;
	private const int CubeStorageId = 0;
	private const long MainOffHand = 1L << 17;
	private const long SubOffHand = 1L << 18;

	public static IdianPolishPlan CreatePolishPlan(
		InventoryItem sourceItem,
		InventoryItem? targetItem,
		ItemTemplateTable itemTemplates,
		ItemRandomBonusTable itemRandomBonuses,
		Func<double>? random = null)
	{
		// Java parity: model/templates/item/actions/PolishAction.canAct + act.
		var sourceTemplate = itemTemplates.GetItemTemplate(sourceItem.ItemId);
		if (sourceTemplate == null || sourceTemplate.PolishSetId <= 0 || sourceItem.Count <= 0)
			return IdianPolishPlan.Failed(IdianPolishResult.NotPolishItem, sourceTemplate, null);

		if (targetItem == null)
			return IdianPolishPlan.Failed(IdianPolishResult.InvalidTarget, sourceTemplate, null);

		var targetTemplate = itemTemplates.GetItemTemplate(targetItem.ItemId);
		if (targetTemplate == null)
			return IdianPolishPlan.Failed(IdianPolishResult.InvalidTarget, sourceTemplate, null);

		if (sourceTemplate.Level > targetTemplate.Level)
			return IdianPolishPlan.Failed(IdianPolishResult.WrongLevel, sourceTemplate, targetTemplate);

		if (!targetItem.IsIdentified)
			return IdianPolishPlan.Failed(IdianPolishResult.NeedIdentify, sourceTemplate, targetTemplate);

		// Java also checks !player.isInAttackMode(); that player mode is not modeled in the C# player state yet.
		if (!targetTemplate.IsWeapon || !targetTemplate.CanPolish)
			return IdianPolishPlan.Failed(IdianPolishResult.InvalidTarget, sourceTemplate, targetTemplate);

		var sourceUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, count: sourceItem.Count - 1) : null;
		var deleteSourceItem = sourceItem.Count <= 1;
		var bonusNumber = itemRandomBonuses.SelectRandomBonusNumber("POLISH", sourceTemplate.PolishSetId, random);
		if (bonusNumber == 0)
		{
			return new IdianPolishPlan(
				IdianPolishResult.NoRandomBonus,
				sourceTemplate,
				targetTemplate,
				sourceUpdate,
				deleteSourceItem,
				null);
		}

		var targetUpdate = CopyInventoryItem(targetItem);
		targetUpdate.IdianStone = new PlayerIdianStone(sourceItem.ItemId, bonusNumber, FullPolishCharge);
		return new IdianPolishPlan(
			IdianPolishResult.Success,
			sourceTemplate,
			targetTemplate,
			sourceUpdate,
			deleteSourceItem,
			targetUpdate);
	}

	public static IdianPolishBurnResult? DecreasePolishCharge(
		InventoryItem item,
		ItemTemplateSummary template,
		int skillValue = 0,
		bool isAttacked = false)
	{
		// Java parity: model/items/IdianStone.decreasePolishCharge.
		if (item.IdianStone is not { PolishCharge: > 0 } idianStone)
			return null;

		var burnAmount = skillValue != 0
			? skillValue
			: isAttacked ? template.IdianInfo?.BurnDefend ?? 0 : template.IdianInfo?.BurnAttack ?? 0;
		if (burnAmount <= 0)
			return null;

		var previousCharge = idianStone.PolishCharge;
		var nextCharge = Math.Max(0, previousCharge - burnAmount);
		var itemUpdate = CopyInventoryItem(item);
		itemUpdate.IdianStone = nextCharge == 0
			? null
			: idianStone with { PolishCharge = nextCharge };

		var updateKind = nextCharge == 0
			? IdianPolishBurnUpdateKind.Exhausted
			: nextCharge <= LowPolishChargeThreshold && previousCharge > LowPolishChargeThreshold
				? IdianPolishBurnUpdateKind.LowCharge
				: IdianPolishBurnUpdateKind.None;

		return new IdianPolishBurnResult(itemUpdate, updateKind, burnAmount);
	}

	public static IdianPolishBurnPlan BurnEquippedWeaponPolishCharge(
		Player player,
		ItemTemplateTable itemTemplates,
		int skillValue)
	{
		// Java parity: skillengine/condition/PolishChargeCondition.validate.
		var inventoryItems = player.InventoryItems.ToList();
		var burns = new List<IdianPolishBurnResult>();
		foreach (var item in inventoryItems.ToArray())
		{
			if (item.Location != CubeStorageId
				|| !item.IsEquipped
				|| item.IdianStone == null
				|| (item.Slot & (MainOffHand | SubOffHand)) != 0)
			{
				continue;
			}

			var template = itemTemplates.GetItemTemplate(item.ItemId);
			if (template is not { IsWeapon: true })
				continue;

			var burn = DecreasePolishCharge(item, template, skillValue: skillValue);
			if (burn == null)
				continue;

			ReplaceInventoryItem(inventoryItems, burn.ItemUpdate);
			burns.Add(burn);
		}

		return burns.Count == 0
			? IdianPolishBurnPlan.NoChange()
			: new IdianPolishBurnPlan(true, inventoryItems, burns);
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

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem update)
	{
		var index = items.FindIndex(item => item.ObjectId == update.ObjectId);
		if (index >= 0)
			items[index] = update;
		else
			items.Add(update);
	}
}

public enum IdianPolishResult
{
	Success,
	NotPolishItem,
	WrongLevel,
	NeedIdentify,
	InvalidTarget,
	NoRandomBonus,
}

public sealed record IdianPolishPlan(
	IdianPolishResult Result,
	ItemTemplateSummary? SourceTemplate,
	ItemTemplateSummary? TargetTemplate,
	InventoryItem? SourceItemUpdate,
	bool DeleteSourceItem,
	InventoryItem? TargetItemUpdate)
{
	public static IdianPolishPlan Failed(
		IdianPolishResult result,
		ItemTemplateSummary? sourceTemplate,
		ItemTemplateSummary? targetTemplate)
	{
		return new IdianPolishPlan(result, sourceTemplate, targetTemplate, null, false, null);
	}
}

public enum IdianPolishBurnUpdateKind
{
	None,
	LowCharge,
	Exhausted,
}

public sealed record IdianPolishBurnResult(
	InventoryItem ItemUpdate,
	IdianPolishBurnUpdateKind UpdateKind,
	int BurnAmount);

public sealed record IdianPolishBurnPlan(
	bool Changed,
	IReadOnlyList<InventoryItem> InventoryItems,
	IReadOnlyList<IdianPolishBurnResult> Burns)
{
	public static IdianPolishBurnPlan NoChange()
	{
		return new IdianPolishBurnPlan(false, Array.Empty<InventoryItem>(), Array.Empty<IdianPolishBurnResult>());
	}
}
