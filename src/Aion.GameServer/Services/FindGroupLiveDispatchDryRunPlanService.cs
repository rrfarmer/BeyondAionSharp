namespace Aion.GameServer.Services;

public static class FindGroupLiveDispatchDryRunPlanService
{
	public static FindGroupLiveDispatchDryRunPlan CreatePlan()
	{
		var checklist = FindGroupLiveDispatchGoNoGoChecklistService.CreateChecklist();
		var gatePlans = new[]
		{
			new FindGroupLiveDispatchDryRunGatePlan(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ConnectionBoundaryWiring,
				"GameServerConnection.ProcessPacketAsync case CmFindGroup",
				"FindGroupConnectionBoundaryDispatchAdapterPlan plus FindGroupConnectionBoundarySideEffectIntentPlan",
				"GameServerConnection.CreateDisabledFindGroupBoundaryPlan can compose the disabled boundary shape, but ProcessPacketAsync still breaks without invoking it.",
				BlocksLiveWiring: true),
			new FindGroupLiveDispatchDryRunGatePlan(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.SharedSingletonLifecycle,
				"FindGroupRecruitmentPlanService singleton graph",
				"FindGroupLifecycleSingletonWiringReadinessReport and FindGroupConcurrentMutationOrderingReadinessReport",
				"Lifecycle callers have singleton graph and deterministic interleaving evidence; live CM_FIND_GROUP does not execute the shared singleton.",
				BlocksLiveWiring: true),
			new FindGroupLiveDispatchDryRunGatePlan(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch,
				"FindGroupSideEffectDispatchExecutorService.ExecuteAsync direct packet phase",
				"FindGroupSideEffectDispatchExecutionPlan.DirectPackets and ExecutionOrder",
				"Opt-in direct packet executor evidence exists, but no live boundary trace proves order relative to the triggering CM_FIND_GROUP packet.",
				BlocksLiveWiring: true),
			new FindGroupLiveDispatchDryRunGatePlan(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.WorldBroadcastDispatch,
				"FindGroupSideEffectDispatchExecutorService.ExecuteAsync world-broadcast phase",
				"FindGroupSideEffectDispatchExecutionPlan.WorldBroadcasts and ExecutionOrder",
				"Opt-in race-filter fanout evidence exists, but no live boundary trace proves same-race fanout and opposite-race exclusion for actions 1 and 5.",
				BlocksLiveWiring: true),
			new FindGroupLiveDispatchDryRunGatePlan(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ActionTwelveInviteDispatch,
				"FindGroupInstanceApplicationInviteDispatchPlanService.CreateDisabledPlan",
				"FindGroupActionTwelveInviteLiveBoundaryTraceContract plus FindGroupInstanceApplicationInviteDispatchPlan group/alliance request result and missing-player status",
				"Disabled action 12 invite plans, failure results, and an ordered live-boundary trace contract exist, but live invite request mutation, declined-whisper dispatch, and packet/question ordering remain unverified.",
				BlocksLiveWiring: true),
			new FindGroupLiveDispatchDryRunGatePlan(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.RuntimeComparison,
				"Future encrypted socket or real-client runtime comparison harness",
				"Java/C# runtime trace or socket comparison artifact",
				"No encrypted socket or real-client comparison has verified live CM_FIND_GROUP packet order, fanout, singleton concurrency, or client-observable behavior.",
				BlocksLiveWiring: true),
		};

		return new FindGroupLiveDispatchDryRunPlan(
			FindGroupLiveDispatchDryRunStatus.BlockedNonLivePlan,
			checklist,
			gatePlans,
			ShouldInvokeLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"Dry run only: enumerate required executors and result surfaces before wiring GameServerConnection.ProcessPacketAsync case CmFindGroup.",
			"Java sources reviewed: CM_FIND_GROUP.runImpl and FindGroupService sendPacket/broadcast/invite/singleton call sites.");
	}
}

public enum FindGroupLiveDispatchDryRunStatus
{
	BlockedNonLivePlan,
	ReadyForLiveWiring,
}

public sealed record FindGroupLiveDispatchDryRunPlan(
	FindGroupLiveDispatchDryRunStatus Status,
	FindGroupLiveDispatchGoNoGoChecklist Checklist,
	IReadOnlyList<FindGroupLiveDispatchDryRunGatePlan> GatePlans,
	bool ShouldInvokeLiveSideEffects,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote,
	string JavaSource)
{
	public bool HasEveryRequiredGatePlan =>
		Checklist.RequiredLiveDispatchGateKinds.All(required => GatePlans.Any(plan => plan.GateKind == required));

	public IReadOnlyList<FindGroupLiveDispatchGoNoGoChecklistItemKind> MissingRequiredGatePlans =>
		Checklist.RequiredLiveDispatchGateKinds
			.Where(required => GatePlans.All(plan => plan.GateKind != required))
			.ToArray();

	public bool IsReadyForLiveDispatch =>
		Status == FindGroupLiveDispatchDryRunStatus.ReadyForLiveWiring
		&& Checklist.IsReadyForLiveDispatch
		&& HasEveryRequiredGatePlan
		&& GatePlans.All(plan => !plan.BlocksLiveWiring);
}

public sealed record FindGroupLiveDispatchDryRunGatePlan(
	FindGroupLiveDispatchGoNoGoChecklistItemKind GateKind,
	string RequiredExecutor,
	string RequiredResultSurface,
	string ExistingNonLiveEvidence,
	bool BlocksLiveWiring);
