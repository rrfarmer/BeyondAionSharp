namespace Aion.GameServer.Services;

public static class FindGroupActionTwelveInviteLiveBoundaryTraceContractService
{
	public static FindGroupActionTwelveInviteLiveBoundaryTraceContract Create()
	{
		var steps = new[]
		{
			Step(
				1,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.TriggeringClientPacketAccepted,
				"Record the parsed CM_FIND_GROUP action 12 after the client packet is accepted by GameServerConnection.ProcessPacketAsync.",
				FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				2,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.ApplicantResolved,
				"Record World.getPlayer(applicantId) equivalent resolution and the missing-applicant no-side-effect branch.",
				FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable),
			Step(
				3,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.ReplyBranchEvaluated,
				"Record instanceApplicationReply == 1 as accept and every other reply value as declined whisper.",
				FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable),
			Step(
				4,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.ResponderInstanceGroupEvaluated,
				"For accept replies, record the responder instanceGroups lookup and the missing-instance-group no-side-effect branch.",
				FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable),
			Step(
				5,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.InviteKindSelected,
				"For accepted replies with an instance group, record minMembers <= 6 selecting PlayerGroupService.inviteToGroup and minMembers > 6 selecting PlayerAllianceService.inviteToAlliance.",
				FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable),
			Step(
				6,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.InviteExecutorInvokedFromBoundary,
				"Record FindGroupInstanceApplicationInviteDispatchPlanService invoked by the CmFindGroup boundary, not by a test-only opt-in path.",
				FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				7,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.InviteRequestMutationObserved,
				"Record the live group/alliance invite request mutation and question-window packet ordering for accepted group and alliance branches.",
				FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				8,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.DeclinedWhisperObserved,
				"Record the declined SM_MESSAGE whisper sent to the applicant with ChatUtil.l10n(1400217) and ChatType.WHISPER.",
				FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
			Step(
				9,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.BoundaryTraceCaptured,
				"Record one ordered trace containing boundary acceptance, applicant resolution, accept/decline branch, missing-branch no-side-effect outcomes, and live invite or whisper result.",
				FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary),
		};

		return new FindGroupActionTwelveInviteLiveBoundaryTraceContract(
			FindGroupActionTwelveInviteLiveBoundaryTraceContractStatus.BlockedPendingLiveBoundaryTrace,
			Action: 12,
			AcceptReplyValue: 1,
			DeclineReplyRule: "instanceApplicationReply != 1",
			steps,
			ShouldInvokeLiveSideEffects: false,
			ShouldMutateInviteRequests: false,
			IsCmFindGroupBoundaryWired: false,
			"Non-live contract only; do not enable CmFindGroup action 12 invite dispatch until each ordered trace step is live-ready.",
			"Java sources reviewed: CM_FIND_GROUP.runImpl action 12 and FindGroupService.sendInstanceApplicationResult.");
	}

	private static FindGroupActionTwelveInviteLiveBoundaryTraceStep Step(
		int sequence,
		FindGroupActionTwelveInviteLiveBoundaryTraceStepKind kind,
		string requirement,
		FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus status)
	{
		return new FindGroupActionTwelveInviteLiveBoundaryTraceStep(sequence, kind, requirement, status);
	}
}

public enum FindGroupActionTwelveInviteLiveBoundaryTraceContractStatus
{
	BlockedPendingLiveBoundaryTrace,
	Ready,
}

public enum FindGroupActionTwelveInviteLiveBoundaryTraceStepKind
{
	TriggeringClientPacketAccepted,
	ApplicantResolved,
	ReplyBranchEvaluated,
	ResponderInstanceGroupEvaluated,
	InviteKindSelected,
	InviteExecutorInvokedFromBoundary,
	InviteRequestMutationObserved,
	DeclinedWhisperObserved,
	BoundaryTraceCaptured,
}

public enum FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus
{
	NonLiveEvidenceAvailable,
	BlockedPendingLiveBoundary,
	Ready,
}

public sealed record FindGroupActionTwelveInviteLiveBoundaryTraceContract(
	FindGroupActionTwelveInviteLiveBoundaryTraceContractStatus Status,
	int Action,
	byte AcceptReplyValue,
	string DeclineReplyRule,
	IReadOnlyList<FindGroupActionTwelveInviteLiveBoundaryTraceStep> RequiredOrderedSteps,
	bool ShouldInvokeLiveSideEffects,
	bool ShouldMutateInviteRequests,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote,
	string JavaSource)
{
	public bool IsReadyForLiveActionTwelveInviteBoundary =>
		Status == FindGroupActionTwelveInviteLiveBoundaryTraceContractStatus.Ready
		&& IsCmFindGroupBoundaryWired
		&& ShouldInvokeLiveSideEffects
		&& ShouldMutateInviteRequests
		&& RequiredOrderedSteps.All(step => step.Status == FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.Ready);
}

public sealed record FindGroupActionTwelveInviteLiveBoundaryTraceStep(
	int Sequence,
	FindGroupActionTwelveInviteLiveBoundaryTraceStepKind Kind,
	string Requirement,
	FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus Status);
