using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public enum CmBuyItemBuyFromShopCompositionPlanStatus
{
	WouldDispatchBuyFromShop,
	ReadAudit,
	SkippedMissingPlayer,
	SkippedNonBuyFromShopAction,
	SkippedUnknownTarget,
	SkippedNonNpcTarget,
	RunAudit,
	SkippedNpcCannotSell,
	UnknownTradeNpcType,
}

public enum CmBuyItemBuyFromShopCompositionStep
{
	ReadParsedClientPacketValues,
	CreateTradeListItems,
	ApplyRunGates,
	ClassifyTradeNpcType,
	AttachBuyTransactionPlan,
}

public sealed record CmBuyItemBuyFromShopCompositionInput(
	CmBuyItem Packet,
	bool PlayerPresent,
	CmBuyItemRunTargetKind TargetKind,
	TradeListTemplateSummary? TradeTemplate,
	bool InteractionAllowed = true,
	bool NpcCanSell = true,
	TradeBuyTransactionPlan? BuyTransactionPlan = null);

public sealed record CmBuyItemBuyFromShopDispatchDescriptor(
	int SellerObjectId,
	int TradeActionId,
	IReadOnlyList<CmBuyItemBuyFromShopItemRequest> TradeItems,
	TradeListTemplateSummary? TradeTemplate,
	bool UseKinah,
	TradeBuyTransactionPlan? BuyTransactionPlan,
	string JavaSource,
	bool IsLive = false);

public sealed record CmBuyItemBuyFromShopItemRequest(int ItemId, long Count);

public sealed record CmBuyItemBuyFromShopCompositionPlan(
	CmBuyItemBuyFromShopCompositionPlanStatus Status,
	CmBuyItem Packet,
	IReadOnlyList<CmBuyItemBuyFromShopItemRequest> TradeItems,
	IReadOnlyList<CmBuyItemBuyFromShopCompositionStep> Steps,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	CmBuyItemBuyFromShopDispatchDescriptor? Dispatch = null,
	string? AuditReason = null)
{
	public bool IsLive => false;
}

public static class CmBuyItemBuyFromShopCompositionPlanService
{
	private static readonly HashSet<int> BuyFromShopActions = [13, 14, 15, 16];

	public static CmBuyItemBuyFromShopCompositionPlan CreatePlan(CmBuyItemBuyFromShopCompositionInput input)
	{
		// Java parity: CM_BUY_ITEM action 13-16 uses a TradeList from readImpl,
		// then runImpl gates on target NPC/canSell before TradeService.performBuyFromShop.
		var tradeItems = input.Packet.Items
			.Select(item => new CmBuyItemBuyFromShopItemRequest(item.ItemObjectId, item.Count))
			.ToArray();
		var steps = new List<CmBuyItemBuyFromShopCompositionStep>
		{
			CmBuyItemBuyFromShopCompositionStep.ReadParsedClientPacketValues,
			CmBuyItemBuyFromShopCompositionStep.CreateTradeListItems,
			CmBuyItemBuyFromShopCompositionStep.ApplyRunGates,
		};

		if (input.Packet.IsAudit)
			return CreatePlan(
				CmBuyItemBuyFromShopCompositionPlanStatus.ReadAudit,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.readImpl -> isAudit=true; runImpl returns before target lookup");

		if (!input.PlayerPresent)
			return CreatePlan(
				CmBuyItemBuyFromShopCompositionPlanStatus.SkippedMissingPlayer,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> if (isAudit || player == null) return");

		if (!BuyFromShopActions.Contains(input.Packet.TradeActionId))
			return CreatePlan(
				CmBuyItemBuyFromShopCompositionPlanStatus.SkippedNonBuyFromShopAction,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> action handled by non-buy-from-shop branch");

		if (input.TargetKind == CmBuyItemRunTargetKind.Unknown)
			return CreatePlan(
				CmBuyItemBuyFromShopCompositionPlanStatus.SkippedUnknownTarget,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> player.getKnownList().getObject(sellerObjId) == null -> return");

		if (input.TargetKind != CmBuyItemRunTargetKind.Npc)
			return CreatePlan(
				CmBuyItemBuyFromShopCompositionPlanStatus.SkippedNonNpcTarget,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> action 13-16 dispatch exists only inside target instanceof Npc branch");

		if (!input.InteractionAllowed)
			return CreatePlan(
				CmBuyItemBuyFromShopCompositionPlanStatus.RunAudit,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl -> !DialogService.isInteractionAllowed(player, npc) -> audit and return",
				auditReason: "might be abusing CM_BUY_ITEM: no right trading with npc");

		if (!input.NpcCanSell)
			return CreatePlan(
				CmBuyItemBuyFromShopCompositionPlanStatus.SkippedNpcCannotSell,
				input,
				tradeItems,
				steps,
				"CM_BUY_ITEM.runImpl action 13-16 -> if (npc.canSell()) TradeService.performBuyFromShop");

		steps.Add(CmBuyItemBuyFromShopCompositionStep.ClassifyTradeNpcType);
		if (!TryGetUseKinah(input.TradeTemplate?.NpcType, out var useKinah))
			return CreatePlan(
				CmBuyItemBuyFromShopCompositionPlanStatus.UnknownTradeNpcType,
				input,
				tradeItems,
				steps,
				"TradeService.performBuyFromShop -> default unhandled TradeNpcType warning and false");

		if (input.BuyTransactionPlan != null)
			steps.Add(CmBuyItemBuyFromShopCompositionStep.AttachBuyTransactionPlan);

		return CreatePlan(
			CmBuyItemBuyFromShopCompositionPlanStatus.WouldDispatchBuyFromShop,
			input,
			tradeItems,
			steps,
			"CM_BUY_ITEM.runImpl action 13-16 -> TradeService.performBuyFromShop(npc, player, tradeList)",
			new CmBuyItemBuyFromShopDispatchDescriptor(
				input.Packet.SellerObjectId,
				input.Packet.TradeActionId,
				tradeItems,
				input.TradeTemplate,
				useKinah,
				input.BuyTransactionPlan,
				useKinah
					? "TradeService.performBuyFromShop -> NORMAL/ABYSS_KINAH -> performBuyTransaction(..., true)"
					: "TradeService.performBuyFromShop -> ABYSS/REWARD -> performBuyTransaction(..., false)",
				IsLive: false));
	}

	private static bool TryGetUseKinah(string? npcType, out bool useKinah)
	{
		useKinah = npcType switch
		{
			"NORMAL" or "ABYSS_KINAH" => true,
			"ABYSS" or "REWARD" => false,
			_ => false,
		};
		return npcType is "NORMAL" or "ABYSS_KINAH" or "ABYSS" or "REWARD";
	}

	private static CmBuyItemBuyFromShopCompositionPlan CreatePlan(
		CmBuyItemBuyFromShopCompositionPlanStatus status,
		CmBuyItemBuyFromShopCompositionInput input,
		IReadOnlyList<CmBuyItemBuyFromShopItemRequest> tradeItems,
		IReadOnlyList<CmBuyItemBuyFromShopCompositionStep> steps,
		string javaSource,
		CmBuyItemBuyFromShopDispatchDescriptor? dispatch = null,
		string? auditReason = null)
	{
		return new CmBuyItemBuyFromShopCompositionPlan(
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
