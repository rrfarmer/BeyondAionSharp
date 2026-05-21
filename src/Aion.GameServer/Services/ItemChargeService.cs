using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemChargeService
{
	public const int Level1ChargePoints = 500_000;
	public const int Level2ChargePoints = 1_000_000;

	public static ItemChargePlan? CreateChargePlan(
		Player player,
		InventoryItem item,
		ItemTemplateTable itemTemplates,
		int maxLevel,
		bool ignoreRankRequirement,
		bool requirePayment)
	{
		// Java parity: services/item/ItemChargeService.chargeItem.
		var context = CreateContext(item, itemTemplates);
		if (context?.Improvement == null)
			return null;

		var level = ignoreRankRequirement
			? maxLevel
			: Math.Min(CalculateAvailableChargeLevel(player, context), maxLevel);
		if (level <= 0)
			return null;

		var maxChargePoints = level == 1 ? Level1ChargePoints : Level2ChargePoints;
		var chargePointsToAdd = Math.Max(0, maxChargePoints - item.Charge);
		if (chargePointsToAdd <= 0)
			return null;

		var paymentAmount = requirePayment
			? GetPayAmountForService(item, context.Improvement, level)
			: 0;
		var targetChargePoints = Math.Clamp(item.Charge + chargePointsToAdd, 0, Level2ChargePoints);
		return new ItemChargePlan(
			item,
			context.Template,
			context.Improvement,
			level,
			targetChargePoints,
			paymentAmount);
	}

	public static long GetPayAmountForService(InventoryItem item, ItemImprovement improvement, int chargeLevel)
	{
		// Java parity: services/item/ItemChargeService.getPayAmountForService.
		var firstLevel = improvement.Price1 / 2d;
		var updateLevel = JavaRound(firstLevel + (improvement.Price2 - improvement.Price1) / 2d);
		double money = 0;
		var currentChargeRatio = 1f;
		switch (chargeLevel)
		{
			case 1:
				currentChargeRatio -= (float)item.Charge / Level1ChargePoints;
				money = Math.Ceiling(firstLevel * currentChargeRatio);
				break;
			case 2:
				switch (GetNextChargeLevel(item))
				{
					case 1:
						currentChargeRatio -= (float)item.Charge / Level1ChargePoints;
						money = Math.Ceiling(firstLevel * currentChargeRatio) + updateLevel;
						break;
					case 2:
						currentChargeRatio -= (float)(item.Charge - Level1ChargePoints) / (Level2ChargePoints - Level1ChargePoints);
						money = Math.Ceiling(updateLevel * currentChargeRatio);
						break;
				}
				break;
		}

		return Math.Max(0, (long)money);
	}

	public static int CalculateAvailableChargeLevel(Player player, InventoryItem item, ItemTemplateTable itemTemplates)
	{
		var context = CreateContext(item, itemTemplates);
		return context == null ? 0 : CalculateAvailableChargeLevel(player, context);
	}

	private static ItemChargeContext? CreateContext(InventoryItem item, ItemTemplateTable itemTemplates)
	{
		var template = itemTemplates.GetItemTemplate(item.ItemId);
		if (template == null)
			return null;

		var fusionTemplate = item.FusionedItem == 0 ? null : itemTemplates.GetItemTemplate(item.FusionedItem);
		var improvement = template.Improvement ?? fusionTemplate?.Improvement;
		return new ItemChargeContext(template, fusionTemplate, improvement);
	}

	private static int CalculateAvailableChargeLevel(Player player, ItemChargeContext context)
	{
		// Java parity: model/gameobjects/Item.calculateAvailableChargeLevel.
		var maxAvailableChargeLevel = CalculateMaxChargeLevel(context);
		var limitsTemplate = context.FusionTemplate is { } fusionTemplate && fusionTemplate.Level > context.Template.Level
			? fusionTemplate
			: context.Template;
		if (limitsTemplate.RecommendRank > 0)
		{
			var rankLevelDiff = Math.Max(0, limitsTemplate.RecommendRank - player.AbyssRank.Rank);
			maxAvailableChargeLevel -= rankLevelDiff;
		}

		return Math.Max(0, maxAvailableChargeLevel);
	}

	private static int CalculateMaxChargeLevel(ItemChargeContext context)
	{
		// Java parity: model/gameobjects/Item.calculateMaxChargeLevel.
		return Math.Max(
			context.Template.Improvement?.Level ?? 0,
			context.FusionTemplate?.Improvement?.Level ?? 0);
	}

	private static int GetNextChargeLevel(InventoryItem item)
	{
		var charge = item.Charge;
		if (charge < Level1ChargePoints)
			return 1;
		if (charge < Level2ChargePoints)
			return 2;
		throw new ArgumentOutOfRangeException(nameof(item), $"Invalid charge level {charge}");
	}

	private static long JavaRound(double value)
	{
		return (long)Math.Floor(value + 0.5d);
	}

	private sealed record ItemChargeContext(
		ItemTemplateSummary Template,
		ItemTemplateSummary? FusionTemplate,
		ItemImprovement? Improvement);
}

public sealed record ItemChargePlan(
	InventoryItem Item,
	ItemTemplateSummary Template,
	ItemImprovement Improvement,
	int Level,
	int TargetChargePoints,
	long PaymentAmount)
{
	public int ChargeWay => Improvement.ChargeWay;
}
