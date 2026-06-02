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
				"GameServerConnection now wires CM_FIND_GROUP mutation-post actions 2 and 6, but the remaining runImpl actions still need live boundary review.",
				"Direct PacketSendUtility.sendPacket intents are executed from the CM_FIND_GROUP boundary only for action 2 and action 6.",
				"PacketSendUtility.broadcastToWorld race-filter fanout is planned but not executed from the CM_FIND_GROUP boundary.",
				"World-broadcast fanout remains blocked: opt-in race-filter evidence exists, but no live boundary trace proves same-race recipients and opposite-race exclusion from CM_FIND_GROUP actions 1 and 5.",
				"Action 11 instance application and declined action 12 direct packet dispatch have connection-adjacent disabled executor evidence but are not invoked from the CM_FIND_GROUP boundary.",
				"Action 12 group/alliance invite request dispatch has connection-adjacent disabled executor evidence but is not invoked from the CM_FIND_GROUP boundary.",
				"FindGroupService lifecycle hooks now have production singleton graph evidence for logout, joined-team, disband cleanup, and CM_FIND_GROUP mutation-post actions 2/6; broader CM_FIND_GROUP actions remain unwired.",
				"Concurrent mutation ordering remains blocked: C# has ConcurrentDictionary state stores and sequential onJoinedTeam evidence, but no live interleaving proof for CM_FIND_GROUP, logout, joined-team, and disband callers sharing one singleton.",
				"Direct packet trigger ordering remains blocked beyond mutation-post actions 2/6: focused live boundary tests prove posted-message-before-show-list ordering for actions 2 and 6 only.",
				"No encrypted socket or real-client runtime comparison has verified live packet order, visibility filtering, or service concurrency.",
			],
			[
				"PlayerEnterWorldService.LeaveWorldAsync uses the injected shared FindGroupService-equivalent cleanup before pending question denial without requiring an observer.",
				"PlayerGroupInviteRequestService and PlayerAllianceInviteRequestService can expose disabled FindGroupService.onJoinedTeam plans after accepted invite membership mutation.",
				"FindGroupRecruitmentPlanService.OnJoinedTeam removes stored instance-group registration when current-team size reaches minMembers.",
				"FindGroupRecruitmentPlanService.OnJoinedTeam preserves Java mutation priority for leader solo recruitment re-add before full-team recruitment removal.",
				"FindGroupRecruitmentPlanService uses ConcurrentDictionary-backed recruitment, application, and instance-group state stores to mirror Java FindGroupService ConcurrentHashMap declarations.",
				"FindGroupConcurrentMutationOrderingReadinessService separates Java ConcurrentHashMap/method-order review and C# focused sequential/concurrent-store plus deterministic shared-singleton interleaving trace projection evidence from the still-missing live singleton interleaving proof.",
				"FindGroupInstanceApplicationDirectDispatchPlanService can compose action 11 direct SM_FIND_GROUP applicant packet, action 11 missing-recipient no-send evidence, and declined action 12 SM_MESSAGE whisper dispatch evidence without sending packets.",
				"FindGroupInstanceApplicationInviteDispatchPlanService can compose action 12 group/alliance invite request-service results without sending packets, and adapter missing-runtime evidence blocks before request mutation.",
				"FindGroupSideEffectDispatchAuditService can audit direct packet and world-broadcast intents without calling the live connection registry.",
				"FindGroupSideEffectDispatchExecutorService can execute direct packet and race-filtered world-broadcast intents through IGameClientConnectionRegistry when explicitly invoked, and records execution order for future live-boundary audits.",
				"FindGroupDirectPacketBoundaryTraceReadinessService records action 0/2/4/6/8/9/10/11/13/15/17 disabled-boundary acceptance; actions 2 and 6 also have focused ProcessPacketAsync send-order tests.",
				"FindGroupDirectPacketTriggerOrderingReadinessService separates Java synchronous runImpl/sendPacket review and C# opt-in executor ordering from the still-missing live ProcessPacketAsync trigger-order proof.",
				"FindGroupWorldBroadcastFanoutReadinessService separates Java PacketSendUtility.broadcastToWorld race-filter review and C# opt-in registry fanout plus disabled action 1/5 boundary fanout trace evidence from the still-missing live CM_FIND_GROUP actions 1/5 fanout proof.",
				"FindGroupConnectionBoundarySideEffectCompositionEvidenceService can compose parsed CmFindGroup planner output, including action 0/4 show-list direct packets, action 2/3/6/7 recruitment/application mutation evidence, action 8/9/17 instance-group mutation packets, action 10/13 instance-group show direct packets, action 11/12 instance-application direct/invite intents, and action 1/5 world-broadcast intents, with opt-in executor results without wiring GameServerConnection.",
				"FindGroupConnectionBoundaryDispatchAdapterService can compose a non-live boundary result with direct packet intents, world-broadcast intents, optional action 12 invite plans, parsed-only no-op status, and missing-runtime no-mutation status without wiring GameServerConnection.",
				"GameServerConnection.CreateDisabledFindGroupBoundaryPlan remains the reviewed composition surface; ProcessPacketAsync consumes it for action 2 and action 6 live direct sends only.",
				"FindGroupLiveDispatchDryRunPlanService enumerates required live-wiring executors and result surfaces for the boundary, singleton, direct-packet, world-broadcast, action 12 invite, and runtime/socket comparison gates without invoking live side effects.",
				"FindGroupLifecycleSingletonWiringReadinessService enumerates Java FindGroupService.getInstance call sites and keeps live singleton wiring blocked until CM_FIND_GROUP also uses the shared C# state store.",
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
