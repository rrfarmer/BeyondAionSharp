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

public enum TradeBuyTransactionPersistenceAdapterStatus
{
	MissingTransactionPlan,
	TransactionPlanNotReady,
	DisabledNoWrites,
}

public enum TradeBuyTransactionPersistenceOperationKind
{
	SaveAbyssPoints,
	SaveKinah,
	DeleteRequiredItem,
	SaveAddedItem,
	UpdateLimitedItemCounter,
}

public enum TradeBuyTransactionSendAdapterStatus
{
	MissingTransactionPlan,
	DisabledNoPackets,
}

public enum TradeBuyTransactionSendIntentKind
{
	SendInvalidBuyItemMessage,
	SendNotEnoughKinah,
	SendNotEnoughAbyssPoints,
	SendFullInventory,
	SendLimitedBuyDenied,
	SendAbyssPointsUpdate,
	SendKinahUpdate,
	SendRequiredItemDelete,
	SendBoughtItemAdd,
	WriteAuditLog,
}

public enum TradeBuyTransactionOutcomePlanStatus
{
	MissingTransactionPlan,
	DisabledNoTransaction,
}

public enum TradeBuyTransactionOutcomeStepKind
{
	PersistRepositoryWrites,
	DispatchPacketAndAuditIntents,
	CommitTransactionBoundary,
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
	int VendorBuyModifier = 100,
	PriceSnapshot? PriceSnapshot = null);

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

public sealed record TradeBuyTransactionPersistenceOperationPlan(
	TradeBuyTransactionPersistenceOperationKind Kind,
	int? ItemId,
	bool WouldWrite,
	bool DidWrite,
	string JavaSource);

public sealed record TradeBuyTransactionPersistenceAdapterPlan(
	TradeBuyTransactionPersistenceAdapterStatus Status,
	TradeBuyTransactionPlan? TransactionPlan,
	IReadOnlyList<TradeBuyTransactionPersistenceOperationPlan> Operations,
	bool WouldWriteRepository,
	bool DidWriteRepository,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public sealed record TradeBuyTransactionSendIntentPlan(
	TradeBuyTransactionSendIntentKind Kind,
	int? ItemId,
	bool WouldSend,
	bool DidSend,
	string JavaSource);

public sealed record TradeBuyTransactionSendAdapterPlan(
	TradeBuyTransactionSendAdapterStatus Status,
	TradeBuyTransactionPlan? TransactionPlan,
	IReadOnlyList<TradeBuyTransactionSendIntentPlan> Intents,
	bool WouldSendPackets,
	bool DidSendPackets,
	bool WouldWriteAuditLog,
	bool DidWriteAuditLog,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public sealed record TradeBuyTransactionOutcomeStepPlan(
	TradeBuyTransactionOutcomeStepKind Kind,
	bool WouldRun,
	bool DidRun,
	string JavaSource);

public sealed record TradeBuyTransactionOutcomePlan(
	TradeBuyTransactionOutcomePlanStatus Status,
	TradeBuyTransactionPlan? TransactionPlan,
	TradeBuyTransactionPersistenceAdapterPlan? PersistenceAdapterPlan,
	TradeBuyTransactionSendAdapterPlan? SendAdapterPlan,
	IReadOnlyList<TradeBuyTransactionOutcomeStepPlan> Steps,
	bool WouldWritePersistence,
	bool DidWritePersistence,
	bool WouldSendPackets,
	bool DidSendPackets,
	bool WouldWriteAuditLog,
	bool DidWriteAuditLog,
	bool WouldCommitTransactionBoundary,
	bool DidCommitTransactionBoundary,
	bool ShouldCommitTransactionBoundary,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

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
	public PriceSnapshot? PriceSnapshot { get; init; }

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
				"TradeService.performBuyTransaction -> !PlayerRestrictions.canTrade(player) -> false",
				priceSnapshot: input.PriceSnapshot);

		steps.Add(TradeBuyTransactionStep.ValidateBuyItems);
		var invalidItem = input.TradeItems.FirstOrDefault(item => item.Count < 1 || !item.IsAllowedByNpcGoodsList);
		if (invalidItem != null)
			return CreatePlan(
				TradeBuyTransactionPlanStatus.BlockedInvalidBuyItem,
				steps,
				"TradeService.validateBuyItems -> count < 1 or item id not in trade goods list -> false",
				priceSnapshot: input.PriceSnapshot,
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
					requiredKinah: requiredKinah,
					priceSnapshot: input.PriceSnapshot);
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
				requiredItems: requiredItems,
				priceSnapshot: input.PriceSnapshot);
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
				priceSnapshot: input.PriceSnapshot,
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
				priceSnapshot: input.PriceSnapshot,
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
				requiredItems: requiredItems,
				priceSnapshot: input.PriceSnapshot);
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
				priceSnapshot: input.PriceSnapshot,
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
			priceSnapshot: input.PriceSnapshot,
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
		string? auditReason = null,
		PriceSnapshot? priceSnapshot = null)
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
			auditReason)
		{
			PriceSnapshot = priceSnapshot,
		};
	}
}

public static class TradeBuyTransactionPersistenceAdapterPlanService
{
	public static TradeBuyTransactionPersistenceAdapterPlan CreateDisabledPlan(TradeBuyTransactionPlan? transactionPlan)
	{
		if (transactionPlan == null)
			return CreateTerminalPlan(
				TradeBuyTransactionPersistenceAdapterStatus.MissingTransactionPlan,
				transactionPlan,
				"TradeService.performBuyTransaction persistence adapter requires a transaction plan");

		if (transactionPlan.Status != TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction || transactionPlan.Mutation == null)
			return CreateTerminalPlan(
				TradeBuyTransactionPersistenceAdapterStatus.TransactionPlanNotReady,
				transactionPlan,
				"TradeService.performBuyTransaction persistence adapter stops before repository writes because transaction plan is blocked");

		var mutation = transactionPlan.Mutation;
		var operations = new List<TradeBuyTransactionPersistenceOperationPlan>();
		if (mutation.RequiredAbyssPoints > 0)
			operations.Add(Disabled(
				TradeBuyTransactionPersistenceOperationKind.SaveAbyssPoints,
				itemId: null,
				"TradeService.performBuyTransaction -> AbyssPointsService.addAp(player, -requiredAp) persists AP deduction"));
		if (mutation.RequiredKinah > 0)
			operations.Add(Disabled(
				TradeBuyTransactionPersistenceOperationKind.SaveKinah,
				itemId: null,
				"TradeService.performBuyTransaction -> inventory.tryDecreaseKinah(tradeListPrice) persists Kinah deduction"));
		operations.AddRange(mutation.RequiredItems.Select(item => Disabled(
			TradeBuyTransactionPersistenceOperationKind.DeleteRequiredItem,
			item.ItemId,
			"TradeService.performBuyTransaction -> inventory.decreaseByItemId(requiredItemId, count) persists required item consumption")));
		operations.AddRange(mutation.AddedItems.Select(item => Disabled(
			TradeBuyTransactionPersistenceOperationKind.SaveAddedItem,
			item.ItemId,
			"TradeService.performBuyTransaction -> ItemService.addItem(player, itemId, count, true, BUY/INC_ITEM_BUY) persists bought item")));
		operations.AddRange(mutation.LimitedItemUpdateItemIds.Select(itemId => Disabled(
			TradeBuyTransactionPersistenceOperationKind.UpdateLimitedItemCounter,
			itemId,
			"TradeService.performBuyTransaction -> LimitedItemTradeService updates per-player buy count/default sell limit")));

		return new TradeBuyTransactionPersistenceAdapterPlan(
			TradeBuyTransactionPersistenceAdapterStatus.DisabledNoWrites,
			transactionPlan,
			operations,
			WouldWriteRepository: operations.Count > 0,
			DidWriteRepository: false,
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performBuyTransaction persistence writes are recorded but disabled",
			IsLive: false);
	}

	private static TradeBuyTransactionPersistenceAdapterPlan CreateTerminalPlan(
		TradeBuyTransactionPersistenceAdapterStatus status,
		TradeBuyTransactionPlan? transactionPlan,
		string javaSource) =>
		new(
			status,
			transactionPlan,
			Operations: Array.Empty<TradeBuyTransactionPersistenceOperationPlan>(),
			WouldWriteRepository: false,
			DidWriteRepository: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);

	private static TradeBuyTransactionPersistenceOperationPlan Disabled(
		TradeBuyTransactionPersistenceOperationKind kind,
		int? itemId,
		string javaSource) =>
		new(kind, itemId, WouldWrite: true, DidWrite: false, javaSource);
}

public static class TradeBuyTransactionSendAdapterPlanService
{
	public static TradeBuyTransactionSendAdapterPlan CreateDisabledPlan(TradeBuyTransactionPlan? transactionPlan)
	{
		if (transactionPlan == null)
			return CreateTerminalPlan(
				TradeBuyTransactionSendAdapterStatus.MissingTransactionPlan,
				transactionPlan,
				"TradeService.performBuyTransaction send adapter requires a transaction plan");

		var intents = new List<TradeBuyTransactionSendIntentPlan>();
		switch (transactionPlan.Status)
		{
			case TradeBuyTransactionPlanStatus.BlockedInvalidBuyItem:
				intents.Add(Disabled(
					TradeBuyTransactionSendIntentKind.SendInvalidBuyItemMessage,
					transactionPlan.RejectedItem?.ItemId,
					"TradeService.performBuyTransaction -> PacketSendUtility.sendMessage(\"Some items are not allowed to be sold from this NPC.\")"));
				break;
			case TradeBuyTransactionPlanStatus.BlockedNotEnoughKinah:
				intents.Add(Disabled(
					TradeBuyTransactionSendIntentKind.SendNotEnoughKinah,
					itemId: null,
					"TradeService.performBuyTransaction -> STR_MSG_NOT_ENOUGH_MONEY"));
				break;
			case TradeBuyTransactionPlanStatus.BlockedNotEnoughAbyssPoints:
			case TradeBuyTransactionPlanStatus.BlockedNotEnoughRequiredItems:
				intents.Add(Disabled(
					TradeBuyTransactionSendIntentKind.SendNotEnoughAbyssPoints,
					transactionPlan.MissingRequiredItem?.ItemId,
					"TradeService.performBuyTransaction -> STR_MSG_NOT_ENOUGH_ABYSSPOINT"));
				break;
			case TradeBuyTransactionPlanStatus.AuditNegativeRequiredAp:
				intents.Add(Disabled(
					TradeBuyTransactionSendIntentKind.WriteAuditLog,
					itemId: null,
					"TradeService.performBuyTransaction -> AuditLogger.log(player, \"possibly used packet hack: tradeList.getRequiredAp() < 0\")"));
				intents.Add(Disabled(
					TradeBuyTransactionSendIntentKind.SendNotEnoughAbyssPoints,
					itemId: null,
					"TradeService.performBuyTransaction negative required AP -> STR_MSG_NOT_ENOUGH_ABYSSPOINT"));
				break;
			case TradeBuyTransactionPlanStatus.BlockedInventoryFull:
				intents.Add(Disabled(
					TradeBuyTransactionSendIntentKind.SendFullInventory,
					itemId: null,
					"TradeService.performBuyTransaction -> STR_MSG_FULL_INVENTORY"));
				break;
			case TradeBuyTransactionPlanStatus.BlockedLimitedItem:
				intents.Add(Disabled(
					TradeBuyTransactionSendIntentKind.SendLimitedBuyDenied,
					transactionPlan.RejectedItem?.ItemId,
					"TradeService.performBuyTransaction -> STR_MSG_LIMITED_BUYING_CANT_SELECT_NO_ITEMS"));
				break;
			case TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction:
				AddSuccessIntents(transactionPlan, intents);
				break;
		}

		return new TradeBuyTransactionSendAdapterPlan(
			TradeBuyTransactionSendAdapterStatus.DisabledNoPackets,
			transactionPlan,
			intents,
			WouldSendPackets: intents.Any(intent => intent.Kind != TradeBuyTransactionSendIntentKind.WriteAuditLog),
			DidSendPackets: false,
			WouldWriteAuditLog: intents.Any(intent => intent.Kind == TradeBuyTransactionSendIntentKind.WriteAuditLog),
			DidWriteAuditLog: false,
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performBuyTransaction packet/audit intents are recorded but disabled",
			IsLive: false);
	}

	private static void AddSuccessIntents(
		TradeBuyTransactionPlan transactionPlan,
		List<TradeBuyTransactionSendIntentPlan> intents)
	{
		if (transactionPlan.Mutation == null)
			return;

		if (transactionPlan.Mutation.RequiredAbyssPoints > 0)
			intents.Add(Disabled(
				TradeBuyTransactionSendIntentKind.SendAbyssPointsUpdate,
				itemId: null,
				"TradeService.performBuyTransaction -> AbyssPointsService.addAp sends AP update"));
		if (transactionPlan.Mutation.RequiredKinah > 0)
			intents.Add(Disabled(
				TradeBuyTransactionSendIntentKind.SendKinahUpdate,
				itemId: null,
				"TradeService.performBuyTransaction -> inventory.tryDecreaseKinah sends Kinah update"));
		intents.AddRange(transactionPlan.Mutation.RequiredItems.Select(item => Disabled(
			TradeBuyTransactionSendIntentKind.SendRequiredItemDelete,
			item.ItemId,
			"TradeService.performBuyTransaction -> inventory.decreaseByItemId sends required item delete/update")));
		intents.AddRange(transactionPlan.Mutation.AddedItems.Select(item => Disabled(
			TradeBuyTransactionSendIntentKind.SendBoughtItemAdd,
			item.ItemId,
			"TradeService.performBuyTransaction -> ItemService.addItem sends bought item add/update")));
	}

	private static TradeBuyTransactionSendAdapterPlan CreateTerminalPlan(
		TradeBuyTransactionSendAdapterStatus status,
		TradeBuyTransactionPlan? transactionPlan,
		string javaSource) =>
		new(
			status,
			transactionPlan,
			Intents: Array.Empty<TradeBuyTransactionSendIntentPlan>(),
			WouldSendPackets: false,
			DidSendPackets: false,
			WouldWriteAuditLog: false,
			DidWriteAuditLog: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);

	private static TradeBuyTransactionSendIntentPlan Disabled(
		TradeBuyTransactionSendIntentKind kind,
		int? itemId,
		string javaSource) =>
		new(kind, itemId, WouldSend: true, DidSend: false, javaSource);
}

public static class TradeBuyTransactionOutcomePlanService
{
	public static TradeBuyTransactionOutcomePlan CreateDisabledPlan(TradeBuyTransactionPlan? transactionPlan)
	{
		if (transactionPlan == null)
			return new TradeBuyTransactionOutcomePlan(
				TradeBuyTransactionOutcomePlanStatus.MissingTransactionPlan,
				transactionPlan,
				PersistenceAdapterPlan: null,
				SendAdapterPlan: null,
				Steps: Array.Empty<TradeBuyTransactionOutcomeStepPlan>(),
				WouldWritePersistence: false,
				DidWritePersistence: false,
				WouldSendPackets: false,
				DidSendPackets: false,
				WouldWriteAuditLog: false,
				DidWriteAuditLog: false,
				WouldCommitTransactionBoundary: false,
				DidCommitTransactionBoundary: false,
				ShouldCommitTransactionBoundary: false,
				ShouldDispatchLiveSideEffects: false,
				"TradeService.performBuyTransaction final outcome requires a transaction plan",
				IsLive: false);

		var persistenceAdapterPlan = TradeBuyTransactionPersistenceAdapterPlanService.CreateDisabledPlan(transactionPlan);
		var sendAdapterPlan = TradeBuyTransactionSendAdapterPlanService.CreateDisabledPlan(transactionPlan);
		var wouldWritePersistence = persistenceAdapterPlan.WouldWriteRepository;
		var wouldSendPackets = sendAdapterPlan.WouldSendPackets;
		var wouldWriteAuditLog = sendAdapterPlan.WouldWriteAuditLog;
		var wouldCommitBoundary = wouldWritePersistence || wouldSendPackets || wouldWriteAuditLog;

		var steps = new List<TradeBuyTransactionOutcomeStepPlan>();
		if (wouldWritePersistence)
			steps.Add(Disabled(
				TradeBuyTransactionOutcomeStepKind.PersistRepositoryWrites,
				"TradeService.performBuyTransaction -> persist AP/Kinah/required-item/bought-item/limited-item updates"));
		if (wouldSendPackets || wouldWriteAuditLog)
			steps.Add(Disabled(
				TradeBuyTransactionOutcomeStepKind.DispatchPacketAndAuditIntents,
				"TradeService.performBuyTransaction -> dispatch system/inventory/AP packet intents and audit log entries"));
		if (wouldCommitBoundary)
			steps.Add(Disabled(
				TradeBuyTransactionOutcomeStepKind.CommitTransactionBoundary,
				"TradeService.performBuyTransaction final transaction boundary is recorded only; Java transaction semantics are not yet runtime-verified"));

		return new TradeBuyTransactionOutcomePlan(
			TradeBuyTransactionOutcomePlanStatus.DisabledNoTransaction,
			transactionPlan,
			persistenceAdapterPlan,
			sendAdapterPlan,
			steps,
			wouldWritePersistence,
			DidWritePersistence: false,
			wouldSendPackets,
			DidSendPackets: false,
			wouldWriteAuditLog,
			DidWriteAuditLog: false,
			wouldCommitBoundary,
			DidCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performBuyTransaction final outcome is disabled; write/send/audit/transaction boundaries are recorded without dispatch",
			IsLive: false);
	}

	private static TradeBuyTransactionOutcomeStepPlan Disabled(
		TradeBuyTransactionOutcomeStepKind kind,
		string javaSource) =>
		new(kind, WouldRun: true, DidRun: false, javaSource);
}
