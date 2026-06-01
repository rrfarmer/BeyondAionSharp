namespace Aion.GameServer.Services;

public static class FindGroupLiveDispatchReadinessReportService
{
	private static readonly int[] JavaRunImplActions =
	[
		0,
		1,
		2,
		3,
		4,
		5,
		6,
		7,
		8,
		9,
		10,
		11,
		12,
		13,
		15,
		17,
	];

	private static readonly int[] ParsedButNoRunImplActions = [20, 25];

	public static FindGroupLiveDispatchReadinessReport CreateReport()
	{
		// Java parity: network/aion/clientpackets/CM_FIND_GROUP.runImpl dispatches only
		// JavaRunImplActions. Actions 20 and 25 are parsed in readImpl but have no runImpl branch.
		var actionReports = JavaRunImplActions
			.Select(action => CreateActionReport(action, hasJavaRunImplBranch: true))
			.Concat(ParsedButNoRunImplActions.Select(action => CreateActionReport(action, hasJavaRunImplBranch: false)))
			.ToArray();

		return new FindGroupLiveDispatchReadinessReport(
			FindGroupLiveDispatchReadinessStatus.BlockedPendingLiveDispatchReview,
			JavaRunImplActions,
			ParsedButNoRunImplActions,
			actionReports,
			[
				"GameServerConnection still defers CmFindGroup instead of invoking FindGroupService-equivalent live side effects.",
				"Direct PacketSendUtility.sendPacket intents are planned but not executed from the CM_FIND_GROUP boundary.",
				"PacketSendUtility.broadcastToWorld race-filter fanout is planned but not executed from the CM_FIND_GROUP boundary.",
				"PlayerGroupService.inviteToGroup and PlayerAllianceService.inviteToAlliance side effects for action 12 are planned but not executed.",
				"FindGroupService.onJoinedTeam and onLogout lifecycle hooks are represented by disabled planners but not wired into all live team/logout callers.",
				"No encrypted socket or real-client runtime comparison has verified live packet order, visibility filtering, or service concurrency.",
			],
			"Java sources reviewed: CM_FIND_GROUP.runImpl and services/findgroup/FindGroupService.");
	}

	private static FindGroupLiveDispatchActionReadiness CreateActionReport(int action, bool hasJavaRunImplBranch)
	{
		var prerequisites = FindGroupClientActionDispatchPrerequisites.Inspect(new FindGroupClientAction(action));
		return new FindGroupLiveDispatchActionReadiness(
			action,
			hasJavaRunImplBranch,
			prerequisites.Readiness,
			prerequisites.Requirements,
			prerequisites.JavaSource);
	}
}

public enum FindGroupLiveDispatchReadinessStatus
{
	BlockedPendingLiveDispatchReview,
	Ready,
}

public sealed record FindGroupLiveDispatchReadinessReport(
	FindGroupLiveDispatchReadinessStatus Status,
	IReadOnlyList<int> JavaRunImplActions,
	IReadOnlyList<int> ParsedButNoRunImplActions,
	IReadOnlyList<FindGroupLiveDispatchActionReadiness> Actions,
	IReadOnlyList<string> GlobalBlockers,
	string JavaSource)
{
	public bool IsReadyForLiveDispatch => Status == FindGroupLiveDispatchReadinessStatus.Ready;
}

public sealed record FindGroupLiveDispatchActionReadiness(
	int Action,
	bool HasJavaRunImplBranch,
	FindGroupClientActionDispatchReadiness Readiness,
	IReadOnlyList<FindGroupClientActionRuntimeRequirement> Requirements,
	string JavaSource);
