namespace Aion.GameServer.Services;

public static class FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldService
{
	private static readonly int[] MutationPostActions = [2, 6];

	public static FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffold Create()
	{
		var steps = new[]
		{
			Step(
				1,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.TriggeringClientPacketAccepted,
				"Record the parsed CM_FIND_GROUP action 2 or 6 after the client packet is accepted by GameServerConnection.ProcessPacketAsync.",
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary),
			Step(
				2,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.SharedSingletonMutationPlanComposed,
				"Record FindGroupRecruitmentPlanService.AddRecruitment or AddApplication against the shared singleton planner before any direct-packet executor is considered.",
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable),
			Step(
				3,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.StateMutationRecorded,
				"Record the recruitment or application state mutation before the posted system message and refreshed show-list intents are emitted.",
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable),
			Step(
				4,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.PostedSystemMessageIntentMaterialized,
				"Record action 2 as STR_PARTY_MATCH_OFFER_PARTY_POSTED message id 1400392 and action 6 as STR_PARTY_MATCH_SEEK_PARTY_POSTED message id 1400393 to the triggering player.",
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable),
			Step(
				5,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.RefreshedShowListIntentMaterialized,
				"Record action 2 followed by the refreshed SM_FIND_GROUP action 0 recruitment show-list, and action 6 followed by the refreshed SM_FIND_GROUP action 4 application show-list.",
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable),
			Step(
				6,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.DirectPacketExecutorInvokedFromBoundary,
				"Record FindGroupSideEffectDispatchExecutorService invoked by the CmFindGroup boundary, not by a test-only opt-in path.",
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary),
			Step(
				7,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.RegistrySendOrderingObserved,
				"Record IGameClientConnectionRegistry direct sends in Java order: posted system message before refreshed show-list, with no world broadcast and no invite dispatch.",
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary),
			Step(
				8,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.BoundaryTraceCaptured,
				"Record one ordered trace for action 2 and one ordered trace for action 6 before expanding this scaffold to other mutating direct-packet actions.",
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary),
		};

		return new FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffold(
			FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStatus.BlockedPendingLiveBoundaryTrace,
			MutationPostActions,
			ExcludedDirectPacketActions: [0, 4, 8, 9, 10, 11, 13, 15, 17],
			steps,
			ActionTwoPostedMessageId: 1400392,
			ActionSixPostedMessageId: 1400393,
			ShouldInvokeLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"Non-live scaffold only; actions 2 and 6 mutate singleton state and require posted-message-before-refresh boundary ordering before live dispatch.",
			"Java sources reviewed: CM_FIND_GROUP.runImpl actions 2 and 6; FindGroupService.addRecruitment/addApplication.");
	}

	private static FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStep Step(
		int sequence,
		FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind kind,
		string requirement,
		FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus status)
	{
		return new FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStep(sequence, kind, requirement, status);
	}
}

public enum FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStatus
{
	BlockedPendingLiveBoundaryTrace,
	Ready,
}

public enum FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind
{
	TriggeringClientPacketAccepted,
	SharedSingletonMutationPlanComposed,
	StateMutationRecorded,
	PostedSystemMessageIntentMaterialized,
	RefreshedShowListIntentMaterialized,
	DirectPacketExecutorInvokedFromBoundary,
	RegistrySendOrderingObserved,
	BoundaryTraceCaptured,
}

public enum FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus
{
	NonLiveEvidenceAvailable,
	BlockedPendingLiveBoundary,
	Ready,
}

public sealed record FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffold(
	FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStatus Status,
	IReadOnlyList<int> MutationPostActions,
	IReadOnlyList<int> ExcludedDirectPacketActions,
	IReadOnlyList<FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStep> RequiredOrderedSteps,
	int ActionTwoPostedMessageId,
	int ActionSixPostedMessageId,
	bool ShouldInvokeLiveSideEffects,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote,
	string JavaSource)
{
	public bool IsReadyForLiveMutationPostBoundary =>
		Status == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStatus.Ready
		&& IsCmFindGroupBoundaryWired
		&& ShouldInvokeLiveSideEffects
		&& RequiredOrderedSteps.All(step => step.Status == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.Ready);
}

public sealed record FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStep(
	int Sequence,
	FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind Kind,
	string Requirement,
	FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus Status);
