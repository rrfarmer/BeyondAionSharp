using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum DropRollNotificationPlanStatus
{
	Passed,
	Rolled,
}

public sealed record DropRollNotificationPlan(
	DropRollNotificationPlanStatus Status,
	int RequestedRoll,
	int LuckResult,
	int MaxRoll,
	SmSystemMessage SelfMessage,
	SmSystemMessage OtherMessage,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class DropRollNotificationPlanService
{
	public static DropRollNotificationPlan CreatePlan(int requestedRoll, int maxRoll, string rollerName, Func<int, int, int>? randomInclusive = null)
	{
		// Java parity: services/drop/DropDistributionService.handleRoll.
		// roll == 0 → give up; else luck = Rnd.get(1, maxRoll).
		// Live broadcastPacket to in-range players is deferred.
		if (requestedRoll == 0)
		{
			return new DropRollNotificationPlan(
				DropRollNotificationPlanStatus.Passed,
				requestedRoll,
				LuckResult: 0,
				maxRoll,
				SmSystemMessage.DiceGiveupMe(),
				SmSystemMessage.DiceGiveupOther(rollerName),
				$"DropDistributionService.handleRoll -> roll==0 -> STR_MSG_DICE_GIVEUP_ME / STR_MSG_DICE_GIVEUP_OTHER({rollerName})"
			);
		}

		var getRandom = randomInclusive ?? ((min, max) => System.Random.Shared.Next(min, max + 1));
		var luck = getRandom(1, maxRoll);

		return new DropRollNotificationPlan(
			DropRollNotificationPlanStatus.Rolled,
			requestedRoll,
			luck,
			maxRoll,
			SmSystemMessage.DiceResultMe(luck, maxRoll),
			SmSystemMessage.DiceResultOther(rollerName, luck, maxRoll),
			$"DropDistributionService.handleRoll -> luck=Rnd.get(1,{maxRoll})={luck} -> STR_MSG_DICE_RESULT_ME / STR_MSG_DICE_RESULT_OTHER({rollerName})"
		);
	}
}
