namespace Aion.GameServer.Services;

public static class FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldService
{
	private static readonly int[] ShowListActions = [0, 4];

	public static FindGroupDirectPacketShowListLiveBoundaryTraceScaffold Create()
	{
		var steps = new[]
		{
			Step(
				1,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.TriggeringClientPacketAccepted,
				"Record the parsed CM_FIND_GROUP action 0 or 4 after the client packet is accepted by GameServerConnection.ProcessPacketAsync.",
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary),
			Step(
				2,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.ShowListPlanComposed,
				"Record FindGroupRecruitmentPlanService.ShowRecruitments or ShowApplications using the active player's race and current singleton snapshot.",
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable),
			Step(
				3,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.DirectPacketIntentMaterialized,
				"Record exactly one direct SmFindGroup intent to the triggering player for action 0 recruitments or action 4 applications.",
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable),
			Step(
				4,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.DirectPacketExecutorInvokedFromBoundary,
				"Record FindGroupSideEffectDispatchExecutorService invoked by the CmFindGroup boundary, not by a test-only opt-in path.",
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary),
			Step(
				5,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.RegistrySendObserved,
				"Record IGameClientConnectionRegistry direct send to the triggering player's object id with no world broadcast and no invite dispatch.",
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary),
			Step(
				6,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.BoundaryTraceCaptured,
				"Record one ordered trace for action 0 and one ordered trace for action 4 before expanding this scaffold to mutating direct-packet actions.",
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary),
		};

		return new FindGroupDirectPacketShowListLiveBoundaryTraceScaffold(
			FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStatus.BlockedPendingLiveBoundaryTrace,
			ShowListActions,
			ExcludedDirectPacketActions: [2, 6, 8, 9, 10, 11, 13, 15, 17],
			steps,
			ShouldInvokeLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"Non-live scaffold only; actions 0 and 4 are show-list direct-packet candidates, not live dispatch approval.",
			"Java sources reviewed: CM_FIND_GROUP.runImpl actions 0 and 4; FindGroupService.showRecruitments/showApplications.");
	}

	private static FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStep Step(
		int sequence,
		FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind kind,
		string requirement,
		FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus status)
	{
		return new FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStep(sequence, kind, requirement, status);
	}
}

public enum FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStatus
{
	BlockedPendingLiveBoundaryTrace,
	Ready,
}

public enum FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind
{
	TriggeringClientPacketAccepted,
	ShowListPlanComposed,
	DirectPacketIntentMaterialized,
	DirectPacketExecutorInvokedFromBoundary,
	RegistrySendObserved,
	BoundaryTraceCaptured,
}

public enum FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus
{
	NonLiveEvidenceAvailable,
	BlockedPendingLiveBoundary,
	Ready,
}

public sealed record FindGroupDirectPacketShowListLiveBoundaryTraceScaffold(
	FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStatus Status,
	IReadOnlyList<int> ShowListActions,
	IReadOnlyList<int> ExcludedDirectPacketActions,
	IReadOnlyList<FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStep> RequiredOrderedSteps,
	bool ShouldInvokeLiveSideEffects,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote,
	string JavaSource)
{
	public bool IsReadyForLiveShowListBoundary =>
		Status == FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStatus.Ready
		&& IsCmFindGroupBoundaryWired
		&& ShouldInvokeLiveSideEffects
		&& RequiredOrderedSteps.All(step => step.Status == FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.Ready);
}

public sealed record FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStep(
	int Sequence,
	FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind Kind,
	string Requirement,
	FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus Status);
