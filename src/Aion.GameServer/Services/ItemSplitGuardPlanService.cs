using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ItemSplitGuardPlanStatus
{
	CanSplit,
	BlockedZeroOrNegativeSplitAmount,
	BlockedIsTrading,
}

public sealed record ItemSplitGuardPlan(
	ItemSplitGuardPlanStatus Status,
	long SplitAmount,
	SmSystemMessage? DenialMessage,
	bool CanSplit,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ItemSplitGuardPlanService
{
	public static ItemSplitGuardPlan CreatePlan(long splitAmount, bool isTrading)
	{
		// Java parity: services/item/ItemSplitService.splitItem early guards.
		if (splitAmount <= 0)
		{
			return new ItemSplitGuardPlan(
				ItemSplitGuardPlanStatus.BlockedZeroOrNegativeSplitAmount,
				splitAmount,
				DenialMessage: null,
				CanSplit: false,
				"ItemSplitService.splitItem -> splitAmount <= 0 -> return (silent)"
			);
		}

		if (isTrading)
		{
			return new ItemSplitGuardPlan(
				ItemSplitGuardPlanStatus.BlockedIsTrading,
				splitAmount,
				SmSystemMessage.InventorySplitDuringTrade(),
				CanSplit: false,
				"ItemSplitService.splitItem -> player.isTrading() -> STR_MSG_INVENTORY_SPLIT_DURING_TRADE"
			);
		}

		return new ItemSplitGuardPlan(
			ItemSplitGuardPlanStatus.CanSplit,
			splitAmount,
			DenialMessage: null,
			CanSplit: true,
			"ItemSplitService.splitItem -> guards passed -> continue to storage lookup"
		);
	}
}
