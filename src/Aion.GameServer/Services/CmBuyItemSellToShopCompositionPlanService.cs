using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public enum CmBuyItemSellToShopCompositionPlanStatus
{
	WouldDispatchSellToShop,
	WouldDispatchSellForApToShop,
	ReadAudit,
	SkippedMissingPlayer,
	SkippedNonSellAction,
	SkippedUnknownTarget,
	SkippedNonNpcTarget,
	RunAudit,
	SkippedNpcCannotBuyOrPurchase,
}

public enum CmBuyItemSellToShopCompositionStep
{
	ReadParsedClientPacketValues,
	CreateTradeListItems,
	ApplyRunGates,
	AttachSellToShopPlan,
}

public sealed record CmBuyItemSellToShopCompositionInput(
	CmBuyItem Packet,
	bool PlayerPresent,
	CmBuyItemRunTargetKind TargetKind,
	bool InteractionAllowed = true,
	bool NpcCanBuy = true,
	bool NpcCanPurchase = false,
	TradeListTemplateSummary? PurchaseTemplate = null,
	TradeSellToShopPlan? SellToShopPlan = null);

public sealed record CmBuyItemSellToShopDispatchDescriptor(
	int SellerObjectId,
	IReadOnlyList<TradeSellToShopItemRequest> TradeItems,
	TradeListTemplateSummary? PurchaseTemplate,
	bool DispatchesAbyssApSell,
	TradeSellToShopPlan? SellToShopPlan,
	string JavaSource,
	bool IsLive = false);

public sealed record CmBuyItemSellToShopCompositionPlan(
	CmBuyItemSellToShopCompositionPlanStatus Status,
	CmBuyItem Packet,
	IReadOnlyList<TradeSellToShopItemRequest> TradeItems,
	IReadOnlyList<CmBuyItemSellToShopCompositionStep> Steps,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	CmBuyItemSellToShopDispatchDescriptor? Dispatch = null,
	string? AuditReason = null)
{
	public bool IsLive => false;
}

public static class CmBuyItemSellToShopCompositionPlanService
{
	public const int SellToShopTradeActionId = 1;

	public static CmBuyItemSellToShopCompositionPlan CreatePlan(CmBuyItemSellToShopCompositionInput input)
	{
		// Java parity: CM_BUY_ITEM action 1 creates a TradeList during readImpl,
		// then runImpl gates on target NPC and canBuy/canPurchase before choosing
		// performSellForAPToShop for ABYSS purchase templates or performSellToShop.
		var tradeItems = input.Packet.Items
			.Select(item => new TradeSellToShopItemRequest(item.ItemObjectId, item.Count))
			.ToArray();
		var steps = new List<CmBuyItemSellToShopCompositionStep>
		{
			CmBuyItemSellToShopCompositionStep.ReadParsedClientPacketValues,
			CmBuyItemSellToShopCompositionStep.CreateTradeListItems,
			CmBuyItemSellToShopCompositionStep.ApplyRunGates,
		};

		if (input.SellToShopPlan != null)
			steps.Add(CmBuyItemSellToShopCompositionStep.AttachSellToShopPlan);

		if (input.Packet.IsAudit)
			return CreatePlan(
				CmBuyItemSellToShopCompositionPlanStatus.ReadAudit,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.readImpl -> isAudit=true; runImpl returns before target lookup");

		if (!input.PlayerPresent)
			return CreatePlan(
				CmBuyItemSellToShopCompositionPlanStatus.SkippedMissingPlayer,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> if (isAudit || player == null) return");

		if (input.Packet.TradeActionId != SellToShopTradeActionId)
			return CreatePlan(
				CmBuyItemSellToShopCompositionPlanStatus.SkippedNonSellAction,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> action handled by non-sell-to-shop branch");

		if (input.TargetKind == CmBuyItemRunTargetKind.Unknown)
			return CreatePlan(
				CmBuyItemSellToShopCompositionPlanStatus.SkippedUnknownTarget,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> player.getKnownList().getObject(sellerObjId) == null -> return");

		if (input.TargetKind != CmBuyItemRunTargetKind.Npc)
			return CreatePlan(
				CmBuyItemSellToShopCompositionPlanStatus.SkippedNonNpcTarget,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> action 1 dispatch exists only inside target instanceof Npc branch");

		if (!input.InteractionAllowed)
			return CreatePlan(
				CmBuyItemSellToShopCompositionPlanStatus.RunAudit,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> !DialogService.isInteractionAllowed(player, npc) -> audit and return",
				auditReason: "might be abusing CM_BUY_ITEM: no right trading with npc");

		if (!input.NpcCanBuy && !input.NpcCanPurchase)
			return CreatePlan(
				CmBuyItemSellToShopCompositionPlanStatus.SkippedNpcCannotBuyOrPurchase,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl action 1 -> if (npc.canBuy() || npc.canPurchase())");

		var dispatchesAbyssApSell = string.Equals(input.PurchaseTemplate?.NpcType, "ABYSS", StringComparison.Ordinal);
		var status = dispatchesAbyssApSell
			? CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellForApToShop
			: CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellToShop;
		var javaSource = dispatchesAbyssApSell
			? "CM_BUY_ITEM.runImpl action 1 -> purchaseTemplate TradeNpcType.ABYSS -> TradeService.performSellForAPToShop"
			: "CM_BUY_ITEM.runImpl action 1 -> TradeService.performSellToShop";

		return CreatePlan(status, input, tradeItems, steps, javaSource, new CmBuyItemSellToShopDispatchDescriptor(
			input.Packet.SellerObjectId,
			tradeItems,
			input.PurchaseTemplate,
			dispatchesAbyssApSell,
			dispatchesAbyssApSell ? null : input.SellToShopPlan,
			dispatchesAbyssApSell
				? "TradeService.performSellForAPToShop(player, tradeList, tradeTemplate)"
				: "TradeService.performSellToShop(player, tradeList, tradeTemplate)",
			IsLive: false));
	}

	private static CmBuyItemSellToShopCompositionPlan CreatePlan(
		CmBuyItemSellToShopCompositionPlanStatus status,
		CmBuyItemSellToShopCompositionInput input,
		IReadOnlyList<TradeSellToShopItemRequest> tradeItems,
		IReadOnlyList<CmBuyItemSellToShopCompositionStep> steps,
		string javaSource,
		CmBuyItemSellToShopDispatchDescriptor? dispatch = null,
		string? auditReason = null)
	{
		return new CmBuyItemSellToShopCompositionPlan(
			status,
			input.Packet,
			tradeItems,
			steps,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			dispatch,
			auditReason);
	}
}
