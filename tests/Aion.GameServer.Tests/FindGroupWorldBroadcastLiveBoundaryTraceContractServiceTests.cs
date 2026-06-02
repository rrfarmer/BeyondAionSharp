using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupWorldBroadcastLiveBoundaryTraceContractServiceTests
{
	[Fact]
	public void Create_KeepsContractBlockedAndNonLive()
	{
		var contract = FindGroupWorldBroadcastLiveBoundaryTraceContractService.Create();

		Assert.Equal(FindGroupWorldBroadcastLiveBoundaryTraceContractStatus.BlockedPendingLiveBoundaryTrace, contract.Status);
		Assert.False(contract.IsReadyForLiveWorldBroadcastBoundary);
		Assert.False(contract.ShouldInvokeLiveSideEffects);
		Assert.False(contract.IsCmFindGroupBoundaryWired);
		Assert.Contains("Non-live contract only", contract.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("removeRecruitment/removeApplication", contract.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_CoversOnlyJavaWorldBroadcastActions()
	{
		var contract = FindGroupWorldBroadcastLiveBoundaryTraceContractService.Create();

		Assert.Equal([1, 5], contract.WorldBroadcastActions);
		Assert.Contains(0, contract.NonBroadcastActions);
		Assert.Contains(12, contract.NonBroadcastActions);
		Assert.Contains(20, contract.NonBroadcastActions);
		Assert.Contains(25, contract.NonBroadcastActions);
		Assert.DoesNotContain(contract.WorldBroadcastActions, action => action is 0 or 2 or 4 or 6 or 8 or 9 or 10 or 11 or 12 or 13 or 15 or 17 or 20 or 25);
	}

	[Fact]
	public void Create_RequiresOrderedTraceMilestonesBeforeLiveReadiness()
	{
		var contract = FindGroupWorldBroadcastLiveBoundaryTraceContractService.Create();

		Assert.Equal(
			[
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.TriggeringClientPacketAccepted,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.SharedSingletonRemovalEvaluated,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.WorldBroadcastIntentMaterialized,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.RaceFilterApplied,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.WorldBroadcastExecutorInvokedFromBoundary,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.RegistryBroadcastObserved,
				FindGroupWorldBroadcastLiveBoundaryTraceStepKind.BoundaryTraceCaptured,
			],
			contract.RequiredOrderedSteps.Select(step => step.Kind));
		Assert.Equal([1, 2, 3, 4, 5, 6, 7], contract.RequiredOrderedSteps.Select(step => step.Sequence));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupWorldBroadcastLiveBoundaryTraceStepKind.WorldBroadcastIntentMaterialized
				&& step.Status == FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("removed recruitment/application branches", StringComparison.Ordinal)
				&& step.Requirement.Contains("missing branches", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupWorldBroadcastLiveBoundaryTraceStepKind.RaceFilterApplied
				&& step.Status == FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("recruitment.getRace", StringComparison.Ordinal)
				&& step.Requirement.Contains("application.getPlayer().getRace", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupWorldBroadcastLiveBoundaryTraceStepKind.WorldBroadcastExecutorInvokedFromBoundary
				&& step.Status == FindGroupWorldBroadcastLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary
				&& step.Requirement.Contains("not by a test-only opt-in path", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupWorldBroadcastLiveBoundaryTraceStepKind.RegistryBroadcastObserved
				&& step.Requirement.Contains("same-race recipients", StringComparison.Ordinal)
				&& step.Requirement.Contains("opposite-race recipients excluded", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupWorldBroadcastLiveBoundaryTraceStepKind.BoundaryTraceCaptured
				&& step.Requirement.Contains("one ordered trace", StringComparison.Ordinal)
				&& step.Requirement.Contains("missing-branch no-send outcomes", StringComparison.Ordinal));
	}
}
