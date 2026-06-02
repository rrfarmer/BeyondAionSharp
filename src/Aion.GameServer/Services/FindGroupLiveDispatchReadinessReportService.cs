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
				"Action 11 instance application and declined action 12 direct packet dispatch have connection-adjacent disabled executor evidence but are not invoked from the CM_FIND_GROUP boundary.",
				"Action 12 group/alliance invite request dispatch has connection-adjacent disabled executor evidence but is not invoked from the CM_FIND_GROUP boundary.",
				"FindGroupService lifecycle hooks now have observer-only evidence for logout and invite joined-team paths, but they are not wired as a live singleton across all team/logout callers.",
				"No encrypted socket or real-client runtime comparison has verified live packet order, visibility filtering, or service concurrency.",
			],
			[
				"PlayerEnterWorldService.LeaveWorldAsync can record disabled FindGroupService.onLogout cleanup before pending question denial.",
				"PlayerGroupInviteRequestService and PlayerAllianceInviteRequestService can expose disabled FindGroupService.onJoinedTeam plans after accepted invite membership mutation.",
				"FindGroupRecruitmentPlanService.OnJoinedTeam removes stored instance-group registration when current-team size reaches minMembers.",
				"FindGroupInstanceApplicationDirectDispatchPlanService can compose action 11 direct SM_FIND_GROUP applicant packet and declined action 12 SM_MESSAGE whisper dispatch evidence without sending packets.",
				"FindGroupInstanceApplicationInviteDispatchPlanService can compose action 12 group/alliance invite request-service results without sending packets.",
				"FindGroupSideEffectDispatchAuditService can audit direct packet and world-broadcast intents without calling the live connection registry.",
				"FindGroupSideEffectDispatchExecutorService can execute direct packet and race-filtered world-broadcast intents through IGameClientConnectionRegistry when explicitly invoked.",
				"FindGroupConnectionBoundarySideEffectCompositionEvidenceService can compose parsed CmFindGroup planner output, including action 0/4 show-list direct packets, action 8/9/17 instance-group mutation packets, action 10/13 instance-group show direct packets, and action 1/5 world-broadcast intents, with opt-in executor results without wiring GameServerConnection.",
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
	IReadOnlyList<string> ObserverEvidence,
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
