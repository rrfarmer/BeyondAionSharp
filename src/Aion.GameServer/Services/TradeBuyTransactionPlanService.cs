using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public enum TradeBuyTransactionPlanStatus
{
	WouldApplyBuyTransaction,
	BlockedCannotTrade,
	BlockedInvalidBuyItem,
	BlockedNotEnoughKinah,
	BlockedNotEnoughAbyssPoints,
	BlockedNotEnoughRequiredItems,
	AuditNegativeRequiredAp,
	BlockedInventoryFull,
	BlockedLimitedItem,
}

public enum TradeBuyTransactionStep
{
	CheckPlayerCanTrade,
	ValidateBuyItems,
	SnapshotInventoryFreeSlots,
	ClassifyTradeNpcRates,
	CalculateKinahPrice,
	CalculateAbyssRewardRequirements,
	CheckRequiredApExploit,
	CheckInventoryFreeSlots,
	CheckLimitedItems,
	PlanCostSubtraction,
	PlanItemAddsAndLimitUpdates,
}

public sealed record TradeBuyTransactionInput(
	IReadOnlyList<TradeBuyTransactionItemRequest> TradeItems,
	TradeListTemplateSummary TradeTemplate,
	bool UseKinah,
	bool PlayerCanTrade,
	long AvailableKinah,
	int CurrentAbyssPoints,
	int FreeSlots,
	IReadOnlyDictionary<int, long>? AvailableRequiredItems = null,
	int VendorBuyModifier = 100);

public sealed record TradeBuyTransactionItemRequest(
	int ItemId,
	long Count,
	long UnitBuyPrice,
	int RequiredApPerItem = 0,
	string AcquisitionType = "",
	int RequiredItemId = 0,
	long RequiredItemCountPerItem = 0,
	bool IsAllowedByNpcGoodsList = true,
	bool LimitedItemCanBuy = true);

public sealed record TradeBuyTransactionRequiredItem(int ItemId, long Count);

public sealed record TradeBuyTransactionMutationDescriptor(
	long RequiredKinah,
	int RequiredAbyssPoints,
	IReadOnlyList<TradeBuyTransactionRequiredItem> RequiredItems,
	IReadOnlyList<TradeBuyTransactionItemRequest> AddedItems,
	IReadOnlyList<int> LimitedItemUpdateItemIds,
	string JavaSource,
	bool IsLive = false);

public sealed record TradeBuyTransactionPlan(
	TradeBuyTransactionPlanStatus Status,
	IReadOnlyList<TradeBuyTransactionStep> Steps,
	long RequiredKinah,
	int RequiredAbyssPoints,
	IReadOnlyList<TradeBuyTransactionRequiredItem> RequiredItems,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	TradeBuyTransactionMutationDescriptor? Mutation = null,
	TradeBuyTransactionItemRequest? RejectedItem = null,
	TradeBuyTransactionRequiredItem? MissingRequiredItem = null,
	string? AuditReason = null)
{
	public bool IsLive => false;
}

public static class TradeBuyTransactionPlanService
{
	public static TradeBuyTransactionPlan CreatePlan(TradeBuyTransactionInput input)
	{
		// Java parity: services/TradeService.performBuyTransaction plus model/trade/TradeList.
		// This planner models decision ordering and derived costs only; it never mutates AP,
		// Kinah, inventory, limited-item state, packets, or repositories.
		var steps = new List<TradeBuyTransactionStep>
		{
			TradeBuyTransactionStep.CheckPlayerCanTrade,
		};

		if (!input.PlayerCanTrade)
			return CreatePlan(
				TradeBuyTransactionPlanStatus.BlockedCannotTrade,
				steps,
				"TradeService.performBuyTransaction -> !PlayerRestrictions.canTrade(player) -> false");

		steps.Add(TradeBuyTransactionStep.ValidateBuyItems);
		var invalidItem = input.TradeItems.FirstOrDefault(item => item.Count < 1 || !item.IsAllowedByNpcGoodsList);
		if (invalidItem != null)
			return CreatePlan(
				TradeBuyTransactionPlanStatus.BlockedInvalidBuyItem,
				steps,
				"TradeService.validateBuyItems -> count < 1 or item id not in trade goods list -> false",
				rejectedItem: invalidItem);

		steps.Add(TradeBuyTransactionStep.SnapshotInventoryFreeSlots);
		steps.Add(TradeBuyTransactionStep.ClassifyTradeNpcRates);

		var sellModifier = string.Equals(input.TradeTemplate.NpcType, "ABYSS_KINAH", StringComparison.Ordinal)
			? input.TradeTemplate.SellPriceRate2
			: input.TradeTemplate.SellPriceRate;
		var apSellModifier = string.Equals(input.TradeTemplate.NpcType, "ABYSS_KINAH", StringComparison.Ordinal)
			? input.TradeTemplate.ApSellPriceRate2
			: input.TradeTemplate.SellPriceRate;

		long requiredKinah = 0;
		if (input.UseKinah)
		{
			steps.Add(TradeBuyTransactionStep.CalculateKinahPrice);
			requiredKinah = CalculateRequiredKinah(input.TradeItems, sellModifier);
			if (input.AvailableKinah < requiredKinah)
			{
				return CreatePlan(
					TradeBuyTransactionPlanStatus.BlockedNotEnoughKinah,
					steps,
					"TradeService.performBuyTransaction -> useKinah && !tradeList.calculateBuyListPrice -> STR_MSG_NOT_ENOUGH_MONEY",
					requiredKinah: requiredKinah);
			}
		}

		steps.Add(TradeBuyTransactionStep.CalculateAbyssRewardRequirements);
		var requiredAp = CalculateRequiredAp(input.TradeItems, apSellModifier, input.VendorBuyModifier);
		var requiredItems = AggregateRequiredItems(input.TradeItems);
		if (input.CurrentAbyssPoints < requiredAp)
		{
			return CreatePlan(
				TradeBuyTransactionPlanStatus.BlockedNotEnoughAbyssPoints,
				steps,
				"TradeService.performBuyTransaction -> !tradeList.calculateAbyssRewardBuyList due AP -> STR_MSG_NOT_ENOUGH_ABYSSPOINT",
				requiredKinah: requiredKinah,
				requiredAp: requiredAp,
				requiredItems: requiredItems);
		}

		var availableRequiredItems = input.AvailableRequiredItems ?? new Dictionary<int, long>();
		var missingRequiredItem = requiredItems.FirstOrDefault(item =>
			item.Count < 1 || !availableRequiredItems.TryGetValue(item.ItemId, out var availableCount) || availableCount < item.Count);
		if (missingRequiredItem != null)
		{
			return CreatePlan(
				TradeBuyTransactionPlanStatus.BlockedNotEnoughRequiredItems,
				steps,
				"TradeService.performBuyTransaction -> !tradeList.calculateAbyssRewardBuyList due required item count -> STR_MSG_NOT_ENOUGH_ABYSSPOINT",
				requiredKinah: requiredKinah,
				requiredAp: requiredAp,
				requiredItems: requiredItems,
				missingRequiredItem: missingRequiredItem);
		}

		steps.Add(TradeBuyTransactionStep.CheckRequiredApExploit);
		if (requiredAp < 0)
		{
			return CreatePlan(
				TradeBuyTransactionPlanStatus.AuditNegativeRequiredAp,
				steps,
				"TradeService.performBuyTransaction -> tradeList.getRequiredAp() < 0 -> audit and STR_MSG_NOT_ENOUGH_ABYSSPOINT",
				requiredKinah: requiredKinah,
				requiredAp: requiredAp,
				requiredItems: requiredItems,
				auditReason: "possibly used packet hack: tradeList.getRequiredAp() < 0");
		}

		steps.Add(TradeBuyTransactionStep.CheckInventoryFreeSlots);
		if (input.FreeSlots < input.TradeItems.Count)
		{
			return CreatePlan(
				TradeBuyTransactionPlanStatus.BlockedInventoryFull,
				steps,
				"TradeService.performBuyTransaction -> freeSlots < tradeList.size() -> STR_MSG_FULL_INVENTORY",
				requiredKinah: requiredKinah,
				requiredAp: requiredAp,
				requiredItems: requiredItems);
		}

		steps.Add(TradeBuyTransactionStep.CheckLimitedItems);
		var limitedBlockedItem = input.TradeItems.FirstOrDefault(item => !item.LimitedItemCanBuy);
		if (limitedBlockedItem != null)
		{
			return CreatePlan(
				TradeBuyTransactionPlanStatus.BlockedLimitedItem,
				steps,
				"TradeService.performBuyTransaction -> !canBuyLimitItem -> STR_MSG_LIMITED_BUYING_CANT_SELECT_NO_ITEMS",
				requiredKinah: requiredKinah,
				requiredAp: requiredAp,
				requiredItems: requiredItems,
				rejectedItem: limitedBlockedItem);
		}

		steps.Add(TradeBuyTransactionStep.PlanCostSubtraction);
		steps.Add(TradeBuyTransactionStep.PlanItemAddsAndLimitUpdates);
		var limitedItemUpdateIds = input.TradeItems
			.Where(item => item.LimitedItemCanBuy)
			.Select(item => item.ItemId)
			.ToArray();

		return CreatePlan(
			TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction,
			steps,
			"TradeService.performBuyTransaction -> subtract AP/Kinah/required items, add bought items, update limited-item counters, return true",
			requiredKinah: requiredKinah,
			requiredAp: requiredAp,
			requiredItems: requiredItems,
			mutation: new TradeBuyTransactionMutationDescriptor(
				requiredKinah,
				requiredAp,
				requiredItems,
				input.TradeItems,
				limitedItemUpdateIds,
				"TradeService.performBuyTransaction steps 6-7",
				IsLive: false));
	}

	private static long CalculateRequiredKinah(IReadOnlyList<TradeBuyTransactionItemRequest> tradeItems, int sellModifier)
	{
		long requiredKinah = 0;
		foreach (var tradeItem in tradeItems)
			requiredKinah += tradeItem.UnitBuyPrice * tradeItem.Count * sellModifier / 100;
		return requiredKinah;
	}

	private static int CalculateRequiredAp(
		IReadOnlyList<TradeBuyTransactionItemRequest> tradeItems,
		int apSellModifier,
		int vendorBuyModifier)
	{
		return TradeApFormulaService.CalculateAbyssBuyRequiredAp(
			tradeItems
				.Where(item => IsAbyssRewardAcquisition(item.AcquisitionType))
				.Select(item => new TradeApCostComponent(item.RequiredApPerItem, item.Count)),
			apSellModifier,
			vendorBuyModifier);
	}

	private static IReadOnlyList<TradeBuyTransactionRequiredItem> AggregateRequiredItems(
		IReadOnlyList<TradeBuyTransactionItemRequest> tradeItems)
	{
		var order = new List<int>();
		var requiredItems = new Dictionary<int, long>();
		foreach (var tradeItem in tradeItems)
		{
			if (tradeItem.RequiredItemId == 0)
				continue;
			if (!requiredItems.ContainsKey(tradeItem.RequiredItemId))
				order.Add(tradeItem.RequiredItemId);
			requiredItems[tradeItem.RequiredItemId] =
				requiredItems.GetValueOrDefault(tradeItem.RequiredItemId) + tradeItem.RequiredItemCountPerItem * tradeItem.Count;
		}

		return order
			.Select(itemId => new TradeBuyTransactionRequiredItem(itemId, requiredItems[itemId]))
			.ToArray();
	}

	private static bool IsAbyssRewardAcquisition(string acquisitionType)
	{
		return string.Equals(acquisitionType, "AP", StringComparison.Ordinal)
			|| string.Equals(acquisitionType, "ABYSS", StringComparison.Ordinal);
	}

	private static TradeBuyTransactionPlan CreatePlan(
		TradeBuyTransactionPlanStatus status,
		IReadOnlyList<TradeBuyTransactionStep> steps,
		string javaSource,
		long requiredKinah = 0,
		int requiredAp = 0,
		IReadOnlyList<TradeBuyTransactionRequiredItem>? requiredItems = null,
		TradeBuyTransactionMutationDescriptor? mutation = null,
		TradeBuyTransactionItemRequest? rejectedItem = null,
		TradeBuyTransactionRequiredItem? missingRequiredItem = null,
		string? auditReason = null)
	{
		return new TradeBuyTransactionPlan(
			status,
			steps.ToArray(),
			requiredKinah,
			requiredAp,
			requiredItems ?? Array.Empty<TradeBuyTransactionRequiredItem>(),
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			mutation,
			rejectedItem,
			missingRequiredItem,
			auditReason);
	}
}
