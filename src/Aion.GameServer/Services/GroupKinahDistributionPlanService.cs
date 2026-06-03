namespace Aion.GameServer.Services;

// Java parity: model/team/common/events/TeamKinahDistributionEvent (used by PlayerGroupService.distributeKinah and
// the alliance/league variants). Pure decision logic for "distribute kinah" among a team's online members.
//
// The amount < 2 and PlayerRestrictions.canTrade guards live in CM_GROUP_DISTRIBUTION.runImpl (the client handler),
// not in the event; this planner models the event's checkCondition + handleEvent decision only.
public enum GroupKinahDistributionOutcome
{
	// checkCondition() false (distributor is not a member of the team) -> nothing happens, no packet.
	Ignored,

	// handleEvent(): distributor inventory kinah < amount -> STR_NOT_ENOUGH_MONEY to the distributor only.
	NotEnoughMoney,

	// handleEvent(): onlineMembers.size() <= 1 OR amount < onlineMembers.size() -> no split, no packet.
	NoDistribution,

	// handleEvent(): split occurs; each online member receives RewardPerPlayer; messages are sent.
	Distribute,
}

public sealed record GroupKinahDistributionPlan(
	GroupKinahDistributionOutcome Outcome,
	long Amount,
	int OnlineMemberCount,
	long RewardPerPlayer);

public static class GroupKinahDistributionPlanService
{
	// Java parity: TeamKinahDistributionEvent.checkCondition + handleEvent.
	// distributorKinah is the distributor's current cube kinah; onlineMemberCount is team.getOnlineMembers().size()
	// (which includes the distributor); isTeamMember is team.hasMember(distributor.getObjectId()).
	public static GroupKinahDistributionPlan Plan(long amount, long distributorKinah, int onlineMemberCount, bool isTeamMember)
	{
		if (!isTeamMember)
			return new GroupKinahDistributionPlan(GroupKinahDistributionOutcome.Ignored, amount, onlineMemberCount, 0);

		// Java parity: handleEvent first check — getInventory().getKinah() < amount.
		if (distributorKinah < amount)
			return new GroupKinahDistributionPlan(GroupKinahDistributionOutcome.NotEnoughMoney, amount, onlineMemberCount, 0);

		// Java parity: onlineMembers.size() > 1 && amount >= onlineMembers.size().
		if (onlineMemberCount > 1 && amount >= onlineMemberCount)
		{
			// Java parity: long rewardPerPlayer = amount / onlineMembers.size() (integer/truncating division).
			var rewardPerPlayer = amount / onlineMemberCount;
			return new GroupKinahDistributionPlan(GroupKinahDistributionOutcome.Distribute, amount, onlineMemberCount, rewardPerPlayer);
		}

		return new GroupKinahDistributionPlan(GroupKinahDistributionOutcome.NoDistribution, amount, onlineMemberCount, 0);
	}
}
