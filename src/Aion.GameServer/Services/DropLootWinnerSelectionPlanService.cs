using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum DropLootWinnerSelectionPlanStatus
{
	NewHighestBid,
	NotHighest,
	AllPassed,
}

public sealed record DropLootWinnerSelectionPlan(
	DropLootWinnerSelectionPlanStatus Status,
	long LuckyValue,
	long CurrentHighestValue,
	int? NewWinnerObjectId,
	SmSystemMessage? AllPassedMessage,
	bool HasNewWinner,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class DropLootWinnerSelectionPlanService
{
	public static DropLootWinnerSelectionPlan CreatePlan(
		long luckyValue,
		long currentHighestValue,
		int playerObjectId,
		bool allPlayersFinished,
		int? currentWinnerObjectId)
	{
		// Java parity: services/drop/DropDistributionService.distributeLoot.
		// if (luckyPlayer > requestedItem.getHighestValue()) → update winner.
		// if (!dropNpc.getPlayerStatus().isEmpty()) → return (not all done).
		// if (winningPlayer == null) → all passed, STR_MSG_PAY_ALL_GIVEUP.
		var hasNewWinner = luckyValue > currentHighestValue;

		var effectiveWinnerObjectId = hasNewWinner ? playerObjectId : currentWinnerObjectId;

		if (!allPlayersFinished)
		{
			return new DropLootWinnerSelectionPlan(
				hasNewWinner ? DropLootWinnerSelectionPlanStatus.NewHighestBid : DropLootWinnerSelectionPlanStatus.NotHighest,
				luckyValue,
				currentHighestValue,
				effectiveWinnerObjectId,
				AllPassedMessage: null,
				hasNewWinner,
				$"DropDistributionService.distributeLoot -> luckyValue={luckyValue} vs highestValue={currentHighestValue} -> {(hasNewWinner ? $"new winner {playerObjectId}" : "not highest")}; players still pending"
			);
		}

		// All players finished
		if (effectiveWinnerObjectId == null)
		{
			return new DropLootWinnerSelectionPlan(
				DropLootWinnerSelectionPlanStatus.AllPassed,
				luckyValue,
				currentHighestValue,
				NewWinnerObjectId: null,
				SmSystemMessage.PayAllGiveup(),
				HasNewWinner: false,
				"DropDistributionService.distributeLoot -> all done, winningPlayer==null -> STR_MSG_PAY_ALL_GIVEUP -> isFreeForAll(true)"
			);
		}

		return new DropLootWinnerSelectionPlan(
			hasNewWinner ? DropLootWinnerSelectionPlanStatus.NewHighestBid : DropLootWinnerSelectionPlanStatus.NotHighest,
			luckyValue,
			currentHighestValue,
			effectiveWinnerObjectId,
			AllPassedMessage: null,
			hasNewWinner,
			$"DropDistributionService.distributeLoot -> all done, winner={effectiveWinnerObjectId} -> isDistributeItem(true)"
		);
	}
}
