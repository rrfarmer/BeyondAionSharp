using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public enum CmBuyItemHandlerCompositionPlanStatus
{
	SelectedSellToShopPlanner,
	SelectedRepurchasePlanner,
	SelectedBuyFromShopPlanner,
	SelectedPrivateStorePlanner,
	SkippedReadAudit,
	SkippedMissingPlayer,
	SkippedUnknownTarget,
	SkippedPlayerTargetNonPrivateStoreAction,
	RunAudit,
	SkippedNpcUnsupportedAction,
	SelectedPetSellToShopPlanner,
	SkippedPetWithoutMerchantFunction,
	SkippedPetNonSellAction,
	SkippedOtherTarget,
}

public enum CmBuyItemHandlerCompositionStep
{
	ReadParsedClientPacketValues,
	ResolveRunTarget,
	SelectJavaRunBranch,
	InvokeSellToShopPlanner,
	InvokeRepurchasePlanner,
	InvokeBuyFromShopPlanner,
	InvokePrivateStorePlanner,
	InvokePetSellToShopPlanner,
	ClassifyUnsupportedBranch,
}

public sealed record CmBuyItemHandlerCompositionInput(
	CmBuyItem Packet,
	bool PlayerPresent,
	CmBuyItemRunTargetKind TargetKind,
	bool InteractionAllowed = true,
	bool NpcCanBuy = true,
	bool NpcCanPurchase = false,
	bool NpcCanSell = true,
	TradeListTemplateSummary? PurchaseTemplate = null,
	TradeSellToShopPlan? SellToShopPlan = null,
	IReadOnlySet<int>? RepurchasableItemObjectIds = null,
	RepurchasePlan? RepurchasePlan = null,
	TradeListTemplateSummary? SellTemplate = null,
	TradeBuyTransactionPlan? BuyTransactionPlan = null,
	IReadOnlyList<PrivateStoreListedItemSummary>? PrivateStoreItems = null,
	PrivateStorePurchasePlan? PrivateStorePurchasePlan = null,
	int? PetSellModifier = null,
	TradeSellToShopPlan? PetSellToShopPlan = null,
	bool PetHasMerchantFunction = false);

public sealed record CmBuyItemHandlerCompositionPlan(
	CmBuyItemHandlerCompositionPlanStatus Status,
	CmBuyItem Packet,
	IReadOnlyList<CmBuyItemHandlerCompositionStep> Steps,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	CmBuyItemSellToShopCompositionPlan? SellToShopPlan = null,
	CmBuyItemRepurchaseCompositionPlan? RepurchasePlan = null,
	CmBuyItemBuyFromShopCompositionPlan? BuyFromShopPlan = null,
	PrivateStoreBoughtItemsPlan? PrivateStoreBoughtItemsPlan = null,
	PrivateStorePurchasePlan? PrivateStorePurchasePlan = null,
	int? PetSellModifier = null,
	TradeSellToShopPlan? PetSellToShopPlan = null,
	string? AuditReason = null)
{
	public bool IsLive => false;
}

public static class CmBuyItemHandlerCompositionPlanService
{
	private const int PrivateStoreTradeActionId = 0;
	private const int RepurchaseTradeActionId = 2;
	private const int PetSellToShopTradeActionId = 17;
	private static readonly HashSet<int> BuyFromShopActions = [13, 14, 15, 16];

	public static CmBuyItemHandlerCompositionPlan CreatePlan(CmBuyItemHandlerCompositionInput input)
	{
		// Java parity: CM_BUY_ITEM.runImpl first applies audit/player/target gates,
		// then selects Player, Npc, or Pet branches. This aggregation remains non-live.
		var steps = new List<CmBuyItemHandlerCompositionStep>
		{
			CmBuyItemHandlerCompositionStep.ReadParsedClientPacketValues,
			CmBuyItemHandlerCompositionStep.ResolveRunTarget,
			CmBuyItemHandlerCompositionStep.SelectJavaRunBranch,
		};

		if (input.Packet.IsAudit)
			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SkippedReadAudit,
				input,
				steps,
				"CM_BUY_ITEM.runImpl -> if (isAudit || player == null) return");

		if (!input.PlayerPresent)
			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SkippedMissingPlayer,
				input,
				steps,
				"CM_BUY_ITEM.runImpl -> if (isAudit || player == null) return");

		if (input.TargetKind == CmBuyItemRunTargetKind.Unknown)
			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget,
				input,
				steps,
				"CM_BUY_ITEM.runImpl -> player.getKnownList().getObject(sellerObjId) == null -> return");

		return input.TargetKind switch
		{
			CmBuyItemRunTargetKind.Player => CreatePlayerTargetPlan(input, steps),
			CmBuyItemRunTargetKind.Npc => CreateNpcTargetPlan(input, steps),
			CmBuyItemRunTargetKind.Pet => CreatePetTargetPlan(input, steps),
			_ => CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SkippedOtherTarget,
				input,
				steps,
				"CM_BUY_ITEM.runImpl -> target is not Player, Npc, or Pet"),
		};
	}

	private static CmBuyItemHandlerCompositionPlan CreatePlayerTargetPlan(
		CmBuyItemHandlerCompositionInput input,
		List<CmBuyItemHandlerCompositionStep> steps)
	{
		if (input.Packet.TradeActionId == PrivateStoreTradeActionId)
		{
			steps.Add(CmBuyItemHandlerCompositionStep.InvokePrivateStorePlanner);
			var boughtItemsPlan = PrivateStoreBoughtItemsPlanService.CreatePlan(
				input.Packet.Items,
				input.PrivateStoreItems ?? Array.Empty<PrivateStoreListedItemSummary>());

			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SelectedPrivateStorePlanner,
				input,
				steps,
				"CM_BUY_ITEM.runImpl -> target instanceof Player && action 0 -> PrivateStoreService.sellStoreItem",
				privateStoreBoughtItemsPlan: boughtItemsPlan,
				privateStorePurchasePlan: input.PrivateStorePurchasePlan);
		}

		steps.Add(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch);
		return CreatePlan(
			CmBuyItemHandlerCompositionPlanStatus.SkippedPlayerTargetNonPrivateStoreAction,
			input,
			steps,
			"CM_BUY_ITEM.runImpl -> Player target dispatch exists only for action 0");
	}

	private static CmBuyItemHandlerCompositionPlan CreateNpcTargetPlan(
		CmBuyItemHandlerCompositionInput input,
		List<CmBuyItemHandlerCompositionStep> steps)
	{
		if (!input.InteractionAllowed)
		{
			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.RunAudit,
				input,
				steps,
				"CM_BUY_ITEM.runImpl -> !DialogService.isInteractionAllowed(player, npc) -> audit and return",
				auditReason: "might be abusing CM_BUY_ITEM: no right trading with npc");
		}

		if (input.Packet.TradeActionId == CmBuyItemSellToShopCompositionPlanService.SellToShopTradeActionId)
		{
			steps.Add(CmBuyItemHandlerCompositionStep.InvokeSellToShopPlanner);
			var sellPlan = CmBuyItemSellToShopCompositionPlanService.CreatePlan(
				new CmBuyItemSellToShopCompositionInput(
					input.Packet,
					input.PlayerPresent,
					CmBuyItemRunTargetKind.Npc,
					input.InteractionAllowed,
					input.NpcCanBuy,
					input.NpcCanPurchase,
					input.PurchaseTemplate,
					input.SellToShopPlan));

			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SelectedSellToShopPlanner,
				input,
				steps,
				"CM_BUY_ITEM.runImpl action 1 -> sell-to-shop planner branch",
				sellToShopPlan: sellPlan);
		}

		if (input.Packet.TradeActionId == RepurchaseTradeActionId)
		{
			steps.Add(CmBuyItemHandlerCompositionStep.InvokeRepurchasePlanner);
			var repurchasePlan = CmBuyItemRepurchaseCompositionPlanService.CreatePlan(
				new CmBuyItemRepurchaseCompositionInput(
					input.Packet,
					input.PlayerPresent,
					CmBuyItemRunTargetKind.Npc,
					input.RepurchasableItemObjectIds ?? new HashSet<int>(),
					input.InteractionAllowed,
					input.NpcCanBuy,
					input.RepurchasePlan));

			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SelectedRepurchasePlanner,
				input,
				steps,
				"CM_BUY_ITEM.runImpl action 2 -> repurchase planner branch",
				repurchasePlan: repurchasePlan);
		}

		if (BuyFromShopActions.Contains(input.Packet.TradeActionId))
		{
			steps.Add(CmBuyItemHandlerCompositionStep.InvokeBuyFromShopPlanner);
			var buyPlan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
				new CmBuyItemBuyFromShopCompositionInput(
					input.Packet,
					input.PlayerPresent,
					CmBuyItemRunTargetKind.Npc,
					input.SellTemplate,
					input.InteractionAllowed,
					input.NpcCanSell,
					input.BuyTransactionPlan));

			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SelectedBuyFromShopPlanner,
				input,
				steps,
				"CM_BUY_ITEM.runImpl action 13-16 -> buy-from-shop planner branch",
				buyFromShopPlan: buyPlan);
		}

		steps.Add(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch);
		return CreatePlan(
			CmBuyItemHandlerCompositionPlanStatus.SkippedNpcUnsupportedAction,
			input,
			steps,
			"CM_BUY_ITEM.runImpl Npc branch -> default log.warn(\"Unknown shop action\") and break");
	}

	private static CmBuyItemHandlerCompositionPlan CreatePetTargetPlan(
		CmBuyItemHandlerCompositionInput input,
		List<CmBuyItemHandlerCompositionStep> steps)
	{
		if (input.Packet.TradeActionId != PetSellToShopTradeActionId)
		{
			steps.Add(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch);
			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SkippedPetNonSellAction,
				input,
				steps,
				"CM_BUY_ITEM.runImpl -> Pet target dispatch exists only for action 17");
		}

		if (!input.PetHasMerchantFunction)
		{
			steps.Add(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch);
			return CreatePlan(
				CmBuyItemHandlerCompositionPlanStatus.SkippedPetWithoutMerchantFunction,
				input,
				steps,
				"CM_BUY_ITEM.runImpl -> pet merchant function missing -> return");
		}

		steps.Add(CmBuyItemHandlerCompositionStep.InvokePetSellToShopPlanner);
		return CreatePlan(
			CmBuyItemHandlerCompositionPlanStatus.SelectedPetSellToShopPlanner,
			input,
			steps,
			"CM_BUY_ITEM.runImpl -> Pet MERCHANT action 17 -> TradeService.performSellToShop(player, tradeList, null, pf.getRatePrice())",
			petSellModifier: input.PetSellModifier,
			petSellToShopPlan: input.PetSellToShopPlan);
	}

	private static CmBuyItemHandlerCompositionPlan CreatePlan(
		CmBuyItemHandlerCompositionPlanStatus status,
		CmBuyItemHandlerCompositionInput input,
		IReadOnlyList<CmBuyItemHandlerCompositionStep> steps,
		string javaSource,
		CmBuyItemSellToShopCompositionPlan? sellToShopPlan = null,
		CmBuyItemRepurchaseCompositionPlan? repurchasePlan = null,
		CmBuyItemBuyFromShopCompositionPlan? buyFromShopPlan = null,
		PrivateStoreBoughtItemsPlan? privateStoreBoughtItemsPlan = null,
		PrivateStorePurchasePlan? privateStorePurchasePlan = null,
		int? petSellModifier = null,
		TradeSellToShopPlan? petSellToShopPlan = null,
		string? auditReason = null)
	{
		return new CmBuyItemHandlerCompositionPlan(
			status,
			input.Packet,
			steps.ToArray(),
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			sellToShopPlan,
			repurchasePlan,
			buyFromShopPlan,
			privateStoreBoughtItemsPlan,
			privateStorePurchasePlan,
			petSellModifier,
			petSellToShopPlan,
			auditReason);
	}
}
