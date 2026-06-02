using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldServiceTests
{
	[Fact]
	public void Create_KeepsMutationPostScaffoldBlockedAndNonLive()
	{
		var scaffold = FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldService.Create();

		Assert.Equal(FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStatus.BlockedPendingLiveBoundaryTrace, scaffold.Status);
		Assert.False(scaffold.IsReadyForLiveMutationPostBoundary);
		Assert.False(scaffold.ShouldInvokeLiveSideEffects);
		Assert.False(scaffold.IsCmFindGroupBoundaryWired);
		Assert.Contains("Non-live scaffold only", scaffold.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("mutate singleton state", scaffold.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("addRecruitment/addApplication", scaffold.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ScopesOnlyJavaMutationPostDirectActions()
	{
		var scaffold = FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldService.Create();

		Assert.Equal([2, 6], scaffold.MutationPostActions);
		Assert.Equal([0, 4, 8, 9, 10, 11, 13, 15, 17], scaffold.ExcludedDirectPacketActions);
		Assert.DoesNotContain(scaffold.MutationPostActions, action => action is 0 or 1 or 3 or 4 or 5 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 15 or 17 or 20 or 25);
		Assert.DoesNotContain(scaffold.ExcludedDirectPacketActions, action => action is 2 or 6);
	}

	[Fact]
	public void Create_RequiresOrderedMutationPostTraceMilestonesBeforeLiveReadiness()
	{
		var scaffold = FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldService.Create();

		Assert.Equal(
			[
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.TriggeringClientPacketAccepted,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.SharedSingletonMutationPlanComposed,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.StateMutationRecorded,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.PostedSystemMessageIntentMaterialized,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.RefreshedShowListIntentMaterialized,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.DirectPacketExecutorInvokedFromBoundary,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.RegistrySendOrderingObserved,
				FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.BoundaryTraceCaptured,
			],
			scaffold.RequiredOrderedSteps.Select(step => step.Kind));
		Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], scaffold.RequiredOrderedSteps.Select(step => step.Sequence));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.SharedSingletonMutationPlanComposed
				&& step.Status == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("AddRecruitment or AddApplication", StringComparison.Ordinal)
				&& step.Requirement.Contains("shared singleton planner", StringComparison.Ordinal));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.StateMutationRecorded
				&& step.Status == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("before the posted system message", StringComparison.Ordinal));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.PostedSystemMessageIntentMaterialized
				&& step.Status == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("STR_PARTY_MATCH_OFFER_PARTY_POSTED", StringComparison.Ordinal)
				&& step.Requirement.Contains("1400392", StringComparison.Ordinal)
				&& step.Requirement.Contains("STR_PARTY_MATCH_SEEK_PARTY_POSTED", StringComparison.Ordinal)
				&& step.Requirement.Contains("1400393", StringComparison.Ordinal));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.RefreshedShowListIntentMaterialized
				&& step.Status == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("SM_FIND_GROUP action 0", StringComparison.Ordinal)
				&& step.Requirement.Contains("SM_FIND_GROUP action 4", StringComparison.Ordinal));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.RegistrySendOrderingObserved
				&& step.Status == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary
				&& step.Requirement.Contains("posted system message before refreshed show-list", StringComparison.Ordinal)
				&& step.Requirement.Contains("no world broadcast", StringComparison.Ordinal)
				&& step.Requirement.Contains("no invite dispatch", StringComparison.Ordinal));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldStepKind.BoundaryTraceCaptured
				&& step.Requirement.Contains("one ordered trace for action 2", StringComparison.Ordinal)
				&& step.Requirement.Contains("one ordered trace for action 6", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RecordsJavaPostedSystemMessageIds()
	{
		var scaffold = FindGroupDirectPacketMutationPostLiveBoundaryTraceScaffoldService.Create();

		Assert.Equal(1400392, scaffold.ActionTwoPostedMessageId);
		Assert.Equal(1400393, scaffold.ActionSixPostedMessageId);
	}
}
