using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationInheritanceService
{
	public static ItemPurificationInheritancePlan CreateTargetItemPlan(
		InventoryItem? sourceItem,
		ItemTemplateSummary? sourceTemplate,
		ItemTemplateSummary? targetTemplate,
		int targetObjectId,
		int? rerolledRandomBonusId = null,
		ItemRandomBonusTable? itemRandomBonuses = null,
		Func<double>? randomBonusRoll = null)
	{
		// Java parity: services/item/ItemPurificationService.upgradeItem copies source item state to
		// ItemFactory.newItem(targetItemId, 1), then applies target-template caps and random-bonus set checks.
		if (sourceItem == null)
			return ItemPurificationInheritancePlan.Failed(ItemPurificationInheritanceStatus.MissingSourceItem);
		if (sourceTemplate == null)
			return ItemPurificationInheritancePlan.Failed(ItemPurificationInheritanceStatus.MissingSourceTemplate);
		if (targetTemplate == null)
			return ItemPurificationInheritancePlan.Failed(ItemPurificationInheritanceStatus.MissingTargetTemplate);

		var enchant = sourceItem.Enchant - 5;
		var amplified = sourceItem.IsAmplified && enchant >= targetTemplate.MaxEnchantLevel;
		var buffSkill = amplified && enchant >= 20 ? sourceItem.BuffSkill : 0;
		var bonusSetsEqual = itemRandomBonuses?.AreBonusSetsEqual("INVENTORY", sourceTemplate.StatBonusSetId, targetTemplate.StatBonusSetId)
			?? sourceTemplate.StatBonusSetId == targetTemplate.StatBonusSetId;
		var randomBonus = CalculateRandomBonus(
			sourceItem.RandomBonus,
			bonusSetsEqual,
			targetTemplate.StatBonusSetId,
			rerolledRandomBonusId,
			itemRandomBonuses,
			randomBonusRoll);

		var targetItem = new InventoryItem
		{
			ObjectId = targetObjectId,
			ItemId = targetTemplate.TemplateId,
			Count = 1,
			Color = sourceItem.Color,
			Creator = sourceItem.Creator,
			OwnerId = sourceItem.OwnerId,
			Location = sourceItem.Location,
			Enchant = enchant,
			EnchantBonus = sourceItem.EnchantBonus,
			FusionedItem = sourceItem.FusionedItem,
			OptionalSocket = sourceItem.OptionalSocket,
			OptionalFusionSocket = sourceItem.OptionalFusionSocket,
			TuneCount = Math.Max(0, Math.Min(sourceItem.TuneCount, targetTemplate.MaxTuneCount)),
			RandomBonus = randomBonus,
			FusionRandomBonus = sourceItem.FusionRandomBonus,
			Tempering = Math.Max(0, sourceItem.Tempering),
			IsSoulBound = sourceItem.IsSoulBound,
			IsAmplified = amplified,
			BuffSkill = buffSkill,
		};
		targetItem.ManaStones = sourceItem.ManaStones;
		targetItem.FusionStones = sourceItem.FusionStones;
		targetItem.Godstone = sourceItem.Godstone;

		return new ItemPurificationInheritancePlan(
			ItemPurificationInheritanceStatus.Created,
			targetItem,
			RandomBonusWasRerolled: sourceItem.RandomBonus > 0
				&& !bonusSetsEqual);
	}

	private static int CalculateRandomBonus(
		int sourceRandomBonus,
		bool bonusSetsEqual,
		int targetStatBonusSetId,
		int? rerolledRandomBonusId,
		ItemRandomBonusTable? itemRandomBonuses,
		Func<double>? randomBonusRoll)
	{
		if (sourceRandomBonus <= 0)
			return 0;
		if (bonusSetsEqual)
			return sourceRandomBonus;

		// Java parity: TuningAction.getRandomStatBonusIdFor(newItem) is runtime-random and data-backed.
		// The explicit reroll id remains a deterministic override for tests and future replay tooling.
		return rerolledRandomBonusId
			?? itemRandomBonuses?.SelectRandomBonusNumber("INVENTORY", targetStatBonusSetId, randomBonusRoll)
			?? 0;
	}
}

public sealed record ItemPurificationInheritancePlan(
	ItemPurificationInheritanceStatus Status,
	InventoryItem? TargetItem,
	bool RandomBonusWasRerolled)
{
	public bool Succeeded => Status == ItemPurificationInheritanceStatus.Created;

	public static ItemPurificationInheritancePlan Failed(ItemPurificationInheritanceStatus status)
	{
		return new ItemPurificationInheritancePlan(status, TargetItem: null, RandomBonusWasRerolled: false);
	}
}

public enum ItemPurificationInheritanceStatus
{
	Created,
	MissingSourceItem,
	MissingSourceTemplate,
	MissingTargetTemplate,
}
