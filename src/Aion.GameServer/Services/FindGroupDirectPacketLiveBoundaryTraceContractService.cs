namespace Aion.GameServer.Services;

public static class FindGroupDirectPacketLiveBoundaryTraceContractService
{
	private static readonly int[] DirectPacketActions = [0, 2, 4, 6, 8, 9, 10, 11, 13, 15, 17];

	public static FindGroupDirectPacketLiveBoundaryTraceContract Create()
	{
		var steps = new[]
		{
			Step(
				1,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.TriggeringClientPacketAccepted,
				"Record the parsed CM_FIND_GROUP action after the client packet is accepted by GameServerConnection.ProcessPacketAsync.",
				FindGroupDirectPacketLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				2,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.SharedSingletonPlanComposed,
				"Record the FindGroupConnectionBoundaryDispatchAdapterPlan composed from the shared FindGroupRecruitmentPlanService graph.",
				FindGroupDirectPacketLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable),
			Step(
				3,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.DirectPacketIntentsMaterialized,
				"Record direct packet intents in Java branch order before any registry send is attempted.",
				FindGroupDirectPacketLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable),
			Step(
				4,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.DirectPacketExecutorInvokedFromBoundary,
				"Record FindGroupSideEffectDispatchExecutorService invoked by the CmFindGroup boundary, not by a test-only opt-in path.",
				FindGroupDirectPacketLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				5,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.RegistrySendObserved,
				"Record IGameClientConnectionRegistry direct sends to the Java-selected recipient object ids.",
				FindGroupDirectPacketLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				6,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.BoundaryTraceCaptured,
				"Record one ordered trace containing boundary acceptance, direct packet execution order, and no parsed-only actions.",
				FindGroupDirectPacketLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
		};

		return new FindGroupDirectPacketLiveBoundaryTraceContract(
			FindGroupDirectPacketLiveBoundaryTraceContractStatus.BlockedPendingLiveBoundaryTrace,
			DirectPacketActions,
			ParsedOnlyActions: [20, 25],
			steps,
			ShouldInvokeLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"Non-live contract only; do not enable CmFindGroup direct packet dispatch until each ordered trace step is live-ready.",
			"Java sources reviewed: CM_FIND_GROUP.runImpl and FindGroupService direct PacketSendUtility.sendPacket call sites.");
	}

	private static FindGroupDirectPacketLiveBoundaryTraceStep Step(
		int sequence,
		FindGroupDirectPacketLiveBoundaryTraceStepKind kind,
		string requirement,
		FindGroupDirectPacketLiveBoundaryTraceStepStatus status)
	{
		return new FindGroupDirectPacketLiveBoundaryTraceStep(sequence, kind, requirement, status);
	}
}

public enum FindGroupDirectPacketLiveBoundaryTraceContractStatus
{
	BlockedPendingLiveBoundaryTrace,
	Ready,
}

public enum FindGroupDirectPacketLiveBoundaryTraceStepKind
{
	TriggeringClientPacketAccepted,
	SharedSingletonPlanComposed,
	DirectPacketIntentsMaterialized,
	DirectPacketExecutorInvokedFromBoundary,
	RegistrySendObserved,
	BoundaryTraceCaptured,
}

public enum FindGroupDirectPacketLiveBoundaryTraceStepStatus
{
	NonLiveEvidenceAvailable,
	BlockedPendingLiveBoundary,
	Ready,
}

public sealed record FindGroupDirectPacketLiveBoundaryTraceContract(
	FindGroupDirectPacketLiveBoundaryTraceContractStatus Status,
	IReadOnlyList<int> DirectPacketActions,
	IReadOnlyList<int> ParsedOnlyActions,
	IReadOnlyList<FindGroupDirectPacketLiveBoundaryTraceStep> RequiredOrderedSteps,
	bool ShouldInvokeLiveSideEffects,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote,
	string JavaSource)
{
	public bool IsReadyForLiveDirectPacketBoundary =>
		Status == FindGroupDirectPacketLiveBoundaryTraceContractStatus.Ready
		&& IsCmFindGroupBoundaryWired
		&& ShouldInvokeLiveSideEffects
		&& RequiredOrderedSteps.All(step => step.Status == FindGroupDirectPacketLiveBoundaryTraceStepStatus.Ready);
}

public sealed record FindGroupDirectPacketLiveBoundaryTraceStep(
	int Sequence,
	FindGroupDirectPacketLiveBoundaryTraceStepKind Kind,
	string Requirement,
	FindGroupDirectPacketLiveBoundaryTraceStepStatus Status);
