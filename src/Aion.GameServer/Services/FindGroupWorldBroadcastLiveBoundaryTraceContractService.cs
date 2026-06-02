namespace Aion.GameServer.Services;

public static class FindGroupWorldBroadcastLiveBoundaryTraceContractService
{
	private static readonly int[] WorldBroadcastActions = [1, 5];

	public static FindGroupWorldBroadcastLiveBoundaryTraceContract Create()
	{
		var steps = new[]
		{
			Step(
				1,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.TriggeringClientPacketAccepted,
				"Record the parsed CM_FIND_GROUP action after the client packet is accepted by GameServerConnection.ProcessPacketAsync.",
				FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				2,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.SharedSingletonRemovalEvaluated,
				"Record the shared FindGroupRecruitmentPlanService removal result before any broadcast is considered.",
				FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable),
			Step(
				3,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.WorldBroadcastIntentMaterialized,
				"Record the world-broadcast intent only for removed recruitment/application branches, and record no intent for missing branches.",
				FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable),
			Step(
				4,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.RaceFilterApplied,
				"Record the Java race predicate: recruitment.getRace() for action 1 and application.getPlayer().getRace() for action 5.",
				FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable),
			Step(
				5,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.WorldBroadcastExecutorInvokedFromBoundary,
				"Record FindGroupSideEffectDispatchExecutorService invoked by the CmFindGroup boundary, not by a test-only opt-in path.",
				FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				6,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.RegistryBroadcastObserved,
				"Record IGameClientConnectionRegistry broadcasts with same-race recipients included and opposite-race recipients excluded.",
				FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				7,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.BoundaryTraceCaptured,
				"Record one ordered trace containing boundary acceptance, removal outcome, race-filtered broadcast, and missing-branch no-send outcomes.",
				FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
		};

		return new FindGroupWorldBroadcastLiveBoundaryTraceContract(
			FindGroupWorldBroadcastLiveBoundaryTraceContractStatus.BlockedPendingLiveBoundaryTrace,
			WorldBroadcastActions,
			NonBroadcastActions: [0, 2, 4, 6, 8, 9, 10, 11, 12, 13, 15, 17, 20, 25],
			steps,
			ShouldInvokeLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"Non-live contract only; do not enable CmFindGroup world-broadcast dispatch until each ordered trace step is live-ready.",
			"Java sources reviewed: CM_FIND_GROUP.runImpl and FindGroupService removeRecruitment/removeApplication broadcastToWorld call sites.");
	}

	private static FindGroupWorldBroadcastLiveBoundaryTraceStep Step(
		int sequence,
		FindGroupWorldBroadcastLiveBoundaryTraceStepKind kind,
		string requirement,
		FindGroupWorldBroadcastLiveBoundaryTraceStepStatus status)
	{
		return new FindGroupWorldBroadcastLiveBoundaryTraceStep(sequence, kind, requirement, status);
	}
}

public enum FindGroupWorldBroadcastLiveBoundaryTraceContractStatus
{
	BlockedPendingLiveBoundaryTrace,
	Ready,
}

public enum FindGroupWorldBroadcastLiveBoundaryTraceStepKind
{
	TriggeringClientPacketAccepted,
	SharedSingletonRemovalEvaluated,
	WorldBroadcastIntentMaterialized,
	RaceFilterApplied,
	WorldBroadcastExecutorInvokedFromBoundary,
	RegistryBroadcastObserved,
	BoundaryTraceCaptured,
}

public enum FindGroupWorldBroadcastLiveBoundaryTraceStepStatus
{
	NonLiveEvidenceAvailable,
	BlockedPendingLiveBoundary,
	Ready,
}

public sealed record FindGroupWorldBroadcastLiveBoundaryTraceContract(
	FindGroupWorldBroadcastLiveBoundaryTraceContractStatus Status,
	IReadOnlyList<int> WorldBroadcastActions,
	IReadOnlyList<int> NonBroadcastActions,
	IReadOnlyList<FindGroupWorldBroadcastLiveBoundaryTraceStep> RequiredOrderedSteps,
	bool ShouldInvokeLiveSideEffects,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote,
	string JavaSource)
{
	public bool IsReadyForLiveWorldBroadcastBoundary =>
		Status == FindGroupWorldBroadcastLiveBoundaryTraceContractStatus.Ready
		&& IsCmFindGroupBoundaryWired
		&& ShouldInvokeLiveSideEffects
		&& RequiredOrderedSteps.All(step => step.Status == FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.Ready);
}

public sealed record FindGroupWorldBroadcastLiveBoundaryTraceStep(
	int Sequence,
	FindGroupWorldBroadcastLiveBoundaryTraceStepKind Kind,
	string Requirement,
	FindGroupWorldBroadcastLiveBoundaryTraceStepStatus Status);
