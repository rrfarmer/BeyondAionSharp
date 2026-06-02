using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldServiceTests
{
	[Fact]
	public void Create_KeepsShowListScaffoldBlockedAndNonLive()
	{
		var scaffold = FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldService.Create();

		Assert.Equal(FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStatus.BlockedPendingLiveBoundaryTrace, scaffold.Status);
		Assert.False(scaffold.IsReadyForLiveShowListBoundary);
		Assert.False(scaffold.ShouldInvokeLiveSideEffects);
		Assert.False(scaffold.IsCmFindGroupBoundaryWired);
		Assert.Contains("Non-live scaffold only", scaffold.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("showRecruitments/showApplications", scaffold.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ScopesOnlyLowRiskJavaShowListActions()
	{
		var scaffold = FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldService.Create();

		Assert.Equal([0, 4], scaffold.ShowListActions);
		Assert.Equal([2, 6, 8, 9, 10, 11, 13, 15, 17], scaffold.ExcludedDirectPacketActions);
		Assert.DoesNotContain(scaffold.ShowListActions, action => action is 1 or 2 or 5 or 6 or 8 or 9 or 10 or 11 or 12 or 13 or 15 or 17 or 20 or 25);
		Assert.DoesNotContain(scaffold.ExcludedDirectPacketActions, action => action is 0 or 4);
	}

	[Fact]
	public void Create_RequiresOrderedTraceMilestonesBeforeLiveReadiness()
	{
		var scaffold = FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldService.Create();

		Assert.Equal(
			[
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.TriggeringClientPacketAccepted,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.ShowListPlanComposed,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.DirectPacketIntentMaterialized,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.DirectPacketExecutorInvokedFromBoundary,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.RegistrySendObserved,
				FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.BoundaryTraceCaptured,
			],
			scaffold.RequiredOrderedSteps.Select(step => step.Kind));
		Assert.Equal([1, 2, 3, 4, 5, 6], scaffold.RequiredOrderedSteps.Select(step => step.Sequence));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.ShowListPlanComposed
				&& step.Status == FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("ShowRecruitments or ShowApplications", StringComparison.Ordinal)
				&& step.Requirement.Contains("active player's race", StringComparison.Ordinal));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.DirectPacketIntentMaterialized
				&& step.Status == FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("exactly one direct SmFindGroup intent", StringComparison.Ordinal));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.RegistrySendObserved
				&& step.Status == FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepStatus.BlockedPendingLiveBoundary
				&& step.Requirement.Contains("no world broadcast", StringComparison.Ordinal)
				&& step.Requirement.Contains("no invite dispatch", StringComparison.Ordinal));
		Assert.Contains(
			scaffold.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketShowListLiveBoundaryTraceScaffoldStepKind.BoundaryTraceCaptured
				&& step.Requirement.Contains("one ordered trace for action 0", StringComparison.Ordinal)
				&& step.Requirement.Contains("one ordered trace for action 4", StringComparison.Ordinal));
	}
}
