using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum NpcDialogControllerDispatchStatus
{
	CreatureControllerNoOp,
	OutOfTalkRange,
	AiHandled,
	DialogServiceFallback,
}

public sealed record NpcDialogControllerDispatchInput(
	QuestDialogNpcControllerDispatchDescriptor Dispatch,
	bool TargetIsNpc,
	bool IsInTalkRange = true,
	bool NpcAiHandledDialogSelect = false,
	NpcDialogServiceSelectFacts? DialogServiceFacts = null,
	SmTradeListPacketPlan? TradeListPacketPlan = null,
	SmTradeInListPacketPlan? TradeInListPacketPlan = null,
	SmRepurchase? RepurchasePacket = null,
	RepurchasePacketSnapshotPlan? RepurchasePacketSnapshotPlan = null);

public sealed record NpcDialogServiceSelectFacts(
	bool NpcSupportsAction = true,
	bool HasTradeList = false,
	bool HasSellableTradeGoods = false,
	int VendorBuyModifier = 100,
	int TradeSellPriceRate = 100,
	bool HasTradeInList = false);

public sealed record NpcDialogControllerDispatchPlan(
	NpcDialogControllerDispatchStatus Status,
	string JavaSource,
	bool IsLive,
	bool CallsNpcAi,
	bool CallsDialogService,
	QuestDialogNpcControllerDispatchDescriptor Dispatch,
	NpcDialogServiceFallbackDescriptor? DialogServiceFallback = null,
	NpcDialogServiceSelectPlan? DialogServicePlan = null,
	string? AuditReason = null);

public sealed record NpcDialogServiceFallbackDescriptor(
	int TargetObjectId,
	int DialogActionId,
	int QuestId,
	int ExtendedRewardIndex,
	string JavaSource,
	bool IsLive = false);

public static class NpcDialogControllerDispatchPlanService
{
	public static NpcDialogControllerDispatchPlan CreatePlan(NpcDialogControllerDispatchInput input)
	{
		// Java parity breadcrumbs:
		// - controllers/CreatureController.onDialogSelect is the empty base implementation.
		// - controllers/NpcController.onDialogSelect checks PositionUtil.isInTalkRange,
		//   then delegates to AI, then falls back to DialogService only when AI returns false.
		if (!input.TargetIsNpc)
		{
			return new NpcDialogControllerDispatchPlan(
				NpcDialogControllerDispatchStatus.CreatureControllerNoOp,
				"CreatureController.onDialogSelect empty implementation",
				IsLive: false,
				CallsNpcAi: false,
				CallsDialogService: false,
				input.Dispatch,
				AuditReason: "known non-NPC Creature target has no base controller dialog behavior");
		}

		if (!input.IsInTalkRange)
		{
			return new NpcDialogControllerDispatchPlan(
				NpcDialogControllerDispatchStatus.OutOfTalkRange,
				"NpcController.onDialogSelect -> !PositionUtil.isInTalkRange(player, getOwner()) return",
				IsLive: false,
				CallsNpcAi: false,
				CallsDialogService: false,
				input.Dispatch,
				AuditReason: "NPC dialog select is ignored outside Java talk range");
		}

		if (input.NpcAiHandledDialogSelect)
		{
			return new NpcDialogControllerDispatchPlan(
				NpcDialogControllerDispatchStatus.AiHandled,
				"NpcController.onDialogSelect -> getOwner().getAi().onDialogSelect(...) returned true",
				IsLive: false,
				CallsNpcAi: true,
				CallsDialogService: false,
				input.Dispatch);
		}

		return new NpcDialogControllerDispatchPlan(
			NpcDialogControllerDispatchStatus.DialogServiceFallback,
			"NpcController.onDialogSelect -> !getOwner().getAi().onDialogSelect(...) -> DialogService.onDialogSelect(...)",
			IsLive: false,
			CallsNpcAi: true,
			CallsDialogService: true,
			input.Dispatch,
			CreateFallbackDescriptor(input.Dispatch),
			CreateDialogServicePlan(input));
	}

	private static NpcDialogServiceFallbackDescriptor CreateFallbackDescriptor(QuestDialogNpcControllerDispatchDescriptor dispatch)
	{
		return new NpcDialogServiceFallbackDescriptor(
			dispatch.TargetObjectId,
			dispatch.DialogActionId,
			dispatch.QuestId,
			dispatch.ExtendedRewardIndex,
			"DialogService.onDialogSelect(dialogActionId, player, getOwner(), questId, extendedRewardIndex)",
			IsLive: false);
	}

	private static NpcDialogServiceSelectPlan? CreateDialogServicePlan(NpcDialogControllerDispatchInput input)
	{
		if (input.DialogServiceFacts == null)
		{
			return null;
		}

		var facts = input.DialogServiceFacts;
		return NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(
				CreateFallbackDescriptor(input.Dispatch),
				facts.NpcSupportsAction,
				facts.HasTradeList,
				facts.HasSellableTradeGoods,
				facts.VendorBuyModifier,
				facts.TradeSellPriceRate,
				facts.HasTradeInList,
				input.TradeListPacketPlan,
				input.TradeInListPacketPlan,
				input.RepurchasePacket,
				input.RepurchasePacketSnapshotPlan));
	}
}
