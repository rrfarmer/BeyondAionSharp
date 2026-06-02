using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupActionTwelveInviteLiveBoundaryTraceContractServiceTests
{
	[Fact]
	public void Create_KeepsContractBlockedAndNonLive()
	{
		var contract = FindGroupActionTwelveInviteLiveBoundaryTraceContractService.Create();

		Assert.Equal(FindGroupActionTwelveInviteLiveBoundaryTraceContractStatus.BlockedPendingLiveBoundaryTrace, contract.Status);
		Assert.False(contract.IsReadyForLiveActionTwelveInviteBoundary);
		Assert.False(contract.ShouldInvokeLiveSideEffects);
		Assert.False(contract.ShouldMutateInviteRequests);
		Assert.False(contract.IsCmFindGroupBoundaryWired);
		Assert.Equal(12, contract.Action);
		Assert.Equal(1, contract.AcceptReplyValue);
		Assert.Equal("instanceApplicationReply != 1", contract.DeclineReplyRule);
		Assert.Contains("Non-live contract only", contract.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("sendInstanceApplicationResult", contract.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_RequiresOrderedTraceMilestonesBeforeLiveReadiness()
	{
		var contract = FindGroupActionTwelveInviteLiveBoundaryTraceContractService.Create();

		Assert.Equal(
			[
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.TriggeringClientPacketAccepted,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.ApplicantResolved,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.ReplyBranchEvaluated,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.ResponderInstanceGroupEvaluated,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.InviteKindSelected,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.InviteExecutorInvokedFromBoundary,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.InviteRequestMutationObserved,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.DeclinedWhisperObserved,
				FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.BoundaryTraceCaptured,
			],
			contract.RequiredOrderedSteps.Select(step => step.Kind));
		Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9], contract.RequiredOrderedSteps.Select(step => step.Sequence));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.ApplicantResolved
				&& step.Status == FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("World.getPlayer(applicantId)", StringComparison.Ordinal)
				&& step.Requirement.Contains("missing-applicant no-side-effect branch", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.ReplyBranchEvaluated
				&& step.Status == FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("instanceApplicationReply == 1", StringComparison.Ordinal)
				&& step.Requirement.Contains("every other reply value", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_SeparatesAcceptedInviteMutationFromDeclinedWhisperTrace()
	{
		var contract = FindGroupActionTwelveInviteLiveBoundaryTraceContractService.Create();

		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.InviteKindSelected
				&& step.Status == FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("minMembers <= 6", StringComparison.Ordinal)
				&& step.Requirement.Contains("PlayerGroupService.inviteToGroup", StringComparison.Ordinal)
				&& step.Requirement.Contains("PlayerAllianceService.inviteToAlliance", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.InviteExecutorInvokedFromBoundary
				&& step.Status == FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary
				&& step.Requirement.Contains("not by a test-only opt-in path", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.InviteRequestMutationObserved
				&& step.Status == FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary
				&& step.Requirement.Contains("live group/alliance invite request mutation", StringComparison.Ordinal)
				&& step.Requirement.Contains("question-window packet ordering", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupActionTwelveInviteLiveBoundaryTraceStepKind.DeclinedWhisperObserved
				&& step.Status == FindGroupActionTwelveInviteLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary
				&& step.Requirement.Contains("ChatUtil.l10n(1400217)", StringComparison.Ordinal)
				&& step.Requirement.Contains("ChatType.WHISPER", StringComparison.Ordinal));
	}
}
