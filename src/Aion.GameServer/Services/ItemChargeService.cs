using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemChargeService
{
	public const int Level1ChargePoints = 500_000;
	public const int Level2ChargePoints = 1_000_000;
	public const int ChargeBarStepPoints = 50_000;

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

	public static IReadOnlyList<ItemChargePlan> CreateChargeAllPlans(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		ItemTemplateTable itemTemplates,
		int chargeWay)
	{
		// Java parity: services/item/ItemChargeService.filterItemsToCondition(player, null, chargeWay).
		return inventoryItems
			.Where(item => item.Location == 0 && item.IsEquipped && item.Charge < Level2ChargePoints)
			.Select(item => CreateChargePlan(
				player,
				item,
				itemTemplates,
				maxLevel: 2,
				ignoreRankRequirement: false,
				requirePayment: true))
			.Where(plan => plan is { } && plan.ChargeWay == chargeWay)
			.Cast<ItemChargePlan>()
			.ToArray();
	}

	public static ItemChargeAbyssPointPaymentPlan CreateAbyssPointPaymentPlan(
		Player? player,
		long requiredAbyssPoints,
		AbyssPointsAddOptions? options = null)
	{
		// Java parity: services/item/ItemChargeService.processAPPayment checks current AP before
		// delegating to AbyssPointsService.addAp; callers must not rely on AP clamping as a spend guard.
		if (requiredAbyssPoints <= 0)
			return ItemChargeAbyssPointPaymentPlan.NoPaymentRequired();
		if (player == null)
			return ItemChargeAbyssPointPaymentPlan.NoPlayer(requiredAbyssPoints);
		if (requiredAbyssPoints > int.MaxValue)
			return ItemChargeAbyssPointPaymentPlan.PaymentTooLarge(player.AbyssRank.Ap, requiredAbyssPoints);
		if (player.AbyssRank.Ap < requiredAbyssPoints)
			return ItemChargeAbyssPointPaymentPlan.InsufficientAbyssPoints(player.AbyssRank.Ap, requiredAbyssPoints);

		var abyssPointsPlan = AbyssPointsService.CreateAddApPlan(player, -(int)requiredAbyssPoints, options);
		return ItemChargeAbyssPointPaymentPlan.Ready(player.AbyssRank.Ap, requiredAbyssPoints, abyssPointsPlan);
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

	public static ItemChargeUpdateResult? DecreaseChargePoints(
		InventoryItem item,
		ItemImprovement improvement,
		bool isAttacked)
	{
		// Java parity: model/items/ChargeInfo attack/attacked/dotattacked observers.
		var burnAmount = isAttacked ? improvement.BurnDefend : improvement.BurnAttack;
		return burnAmount <= 0 ? null : UpdateChargePoints(item, -burnAmount);
	}

	public static ItemChargeUpdateResult? UpdateChargePoints(InventoryItem item, int pointsToAdd)
	{
		// Java parity: model/items/ChargeInfo.updateChargePoints.
		if (pointsToAdd == 0)
			return null;

		var previousCharge = Math.Clamp(item.Charge, 0, Level2ChargePoints);
		var nextCharge = Math.Clamp(previousCharge + pointsToAdd, 0, Level2ChargePoints);
		if (nextCharge == previousCharge)
			return null;

		var itemUpdate = CopyInventoryItem(item, charge: nextCharge);
		return new ItemChargeUpdateResult(
			itemUpdate,
			ChargeBarChanged: GetChargeBarStep(previousCharge) != GetChargeBarStep(nextCharge),
			PointsDelta: nextCharge - previousCharge);
	}

	public static ItemChargeBurnPlan BurnEquippedChargePoints(
		Player player,
		ItemTemplateTable itemTemplates,
		ItemChargeObserverEvent observerEvent,
		int skillId)
	{
		// Java parity: model/stats/listeners/ItemEquipmentListener attaches ChargeInfo as a DOT_ATTACK_DEFEND observer.
		if (observerEvent != ItemChargeObserverEvent.DotAttacked && skillId != 0)
			return ItemChargeBurnPlan.NoChange();

		var inventoryItems = player.InventoryItems.ToList();
		var burns = new List<ItemChargeUpdateResult>();
		foreach (var item in inventoryItems.ToArray())
		{
			if (item.Location != 0 || !item.IsEquipped || item.Charge <= 0)
				continue;

			var context = CreateContext(item, itemTemplates);
			if (context?.Improvement == null)
				continue;

			var burn = DecreaseChargePoints(
				item,
				context.Improvement,
				isAttacked: observerEvent != ItemChargeObserverEvent.Attack);
			if (burn == null)
				continue;

			ReplaceInventoryItem(inventoryItems, burn.ItemUpdate);
			burns.Add(burn);
		}

		return burns.Count == 0
			? ItemChargeBurnPlan.NoChange()
			: new ItemChargeBurnPlan(true, inventoryItems, burns);
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

	private static int GetChargeBarStep(int charge)
	{
		return Math.Clamp(charge, 0, Level2ChargePoints) / ChargeBarStepPoints;
	}

	private static long JavaRound(double value)
	{
		return (long)Math.Floor(value + 0.5d);
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, int? charge = null)
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
			Charge = charge ?? item.Charge,
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

public sealed record ItemChargeAbyssPointPaymentPlan(
	ItemChargeAbyssPointPaymentStatus Status,
	int CurrentAbyssPoints,
	long RequiredAbyssPoints,
	AbyssPointsAddPlan? AbyssPointsPlan)
{
	public bool Succeeded => Status is ItemChargeAbyssPointPaymentStatus.Ready or ItemChargeAbyssPointPaymentStatus.NoPaymentRequired;

	public static ItemChargeAbyssPointPaymentPlan Ready(int currentAbyssPoints, long requiredAbyssPoints, AbyssPointsAddPlan abyssPointsPlan)
	{
		return new ItemChargeAbyssPointPaymentPlan(
			ItemChargeAbyssPointPaymentStatus.Ready,
			currentAbyssPoints,
			requiredAbyssPoints,
			abyssPointsPlan);
	}

	public static ItemChargeAbyssPointPaymentPlan NoPaymentRequired()
	{
		return new ItemChargeAbyssPointPaymentPlan(
			ItemChargeAbyssPointPaymentStatus.NoPaymentRequired,
			CurrentAbyssPoints: 0,
			RequiredAbyssPoints: 0,
			AbyssPointsPlan: null);
	}

	public static ItemChargeAbyssPointPaymentPlan NoPlayer(long requiredAbyssPoints)
	{
		return new ItemChargeAbyssPointPaymentPlan(
			ItemChargeAbyssPointPaymentStatus.NoPlayer,
			CurrentAbyssPoints: 0,
			requiredAbyssPoints,
			AbyssPointsPlan: null);
	}

	public static ItemChargeAbyssPointPaymentPlan InsufficientAbyssPoints(int currentAbyssPoints, long requiredAbyssPoints)
	{
		return new ItemChargeAbyssPointPaymentPlan(
			ItemChargeAbyssPointPaymentStatus.InsufficientAbyssPoints,
			currentAbyssPoints,
			requiredAbyssPoints,
			AbyssPointsPlan: null);
	}

	public static ItemChargeAbyssPointPaymentPlan PaymentTooLarge(int currentAbyssPoints, long requiredAbyssPoints)
	{
		return new ItemChargeAbyssPointPaymentPlan(
			ItemChargeAbyssPointPaymentStatus.PaymentTooLarge,
			currentAbyssPoints,
			requiredAbyssPoints,
			AbyssPointsPlan: null);
	}
}

public enum ItemChargeAbyssPointPaymentStatus
{
	Ready,
	NoPaymentRequired,
	NoPlayer,
	InsufficientAbyssPoints,
	PaymentTooLarge,
}

public sealed record ItemChargeUpdateResult(
	InventoryItem ItemUpdate,
	bool ChargeBarChanged,
	int PointsDelta);

public sealed record ItemChargeBurnPlan(
	bool Changed,
	IReadOnlyList<InventoryItem> InventoryItems,
	IReadOnlyList<ItemChargeUpdateResult> Burns)
{
	public static ItemChargeBurnPlan NoChange()
	{
		return new ItemChargeBurnPlan(false, Array.Empty<InventoryItem>(), Array.Empty<ItemChargeUpdateResult>());
	}
}

public enum ItemChargeObserverEvent
{
	Attack,
	Attacked,
	DotAttacked,
}
