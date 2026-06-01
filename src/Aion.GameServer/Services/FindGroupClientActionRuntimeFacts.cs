using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed record FindGroupClientActionRuntimeFacts(
	Player ActivePlayer,
	int NowEpochSeconds,
	Func<int, Player?>? ResolvePlayer = null,
	FindGroupRecruitmentSubject? CurrentTeam = null,
	IReadOnlyList<FindGroupInstanceGroupMemberState>? CurrentMembers = null,
	bool FormInstanceGroupAnywhere = false,
	IReadOnlyList<int>? TargetNpcInstanceMaskIds = null,
	IReadOnlyList<int>? AllRecruitableInstanceMaskIds = null)
{
	public FindGroupClientActionPlan ComposeDisabledPlan(
		FindGroupClientActionPlanService planner,
		CmFindGroup packet)
	{
		return ComposeDisabledPlan(planner, FindGroupClientAction.FromPacket(packet));
	}

	public FindGroupClientActionPlan ComposeDisabledPlan(
		FindGroupClientActionPlanService planner,
		FindGroupClientAction action)
	{
		// Java parity: CM_FIND_GROUP.runImpl obtains active player from the connection and
		// runtime facts from FindGroupService/World/config/data managers. This context only
		// packages those facts for disabled planning; it does not dispatch live side effects.
		return planner.Plan(
			ActivePlayer,
			action,
			NowEpochSeconds,
			ResolvePlayer,
			CurrentTeam,
			CurrentMembers,
			FormInstanceGroupAnywhere,
			TargetNpcInstanceMaskIds,
			AllRecruitableInstanceMaskIds);
	}
}
