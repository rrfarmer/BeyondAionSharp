using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupDirectPacketLiveBoundaryTraceContractServiceTests
{
	[Fact]
	public void Create_KeepsContractBlockedAndNonLive()
	{
		var contract = FindGroupDirectPacketLiveBoundaryTraceContractService.Create();

		Assert.Equal(FindGroupDirectPacketLiveBoundaryTraceContractStatus.BlockedPendingLiveBoundaryTrace, contract.Status);
		Assert.False(contract.IsReadyForLiveDirectPacketBoundary);
		Assert.False(contract.ShouldInvokeLiveSideEffects);
		Assert.False(contract.IsCmFindGroupBoundaryWired);
		Assert.Contains("Non-live contract only", contract.BoundaryNote, StringComparison.Ordinal);
		Assert.Contains("CM_FIND_GROUP.runImpl", contract.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_CoversJavaDirectPacketActionsAndExcludesParsedOnlyActions()
	{
		var contract = FindGroupDirectPacketLiveBoundaryTraceContractService.Create();

		Assert.Equal([0, 2, 4, 6, 8, 9, 10, 11, 13, 15, 17], contract.DirectPacketActions);
		Assert.Equal([20, 25], contract.ParsedOnlyActions);
		Assert.DoesNotContain(contract.DirectPacketActions, action => action is 20 or 25);
		Assert.DoesNotContain(contract.DirectPacketActions, action => action is 1 or 5 or 12 or 14 or 16);
	}

	[Fact]
	public void Create_RequiresOrderedTraceMilestonesBeforeLiveReadiness()
	{
		var contract = FindGroupDirectPacketLiveBoundaryTraceContractService.Create();

		Assert.Equal(
			[
				FindGroupDirectPacketLiveBoundaryTraceStepKind.TriggeringClientPacketAccepted,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.SharedSingletonPlanComposed,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.DirectPacketIntentsMaterialized,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.DirectPacketExecutorInvokedFromBoundary,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.RegistrySendObserved,
				FindGroupDirectPacketLiveBoundaryTraceStepKind.BoundaryTraceCaptured,
			],
			contract.RequiredOrderedSteps.Select(step => step.Kind));
		Assert.Equal([1, 2, 3, 4, 5, 6], contract.RequiredOrderedSteps.Select(step => step.Sequence));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketLiveBoundaryTraceStepKind.SharedSingletonPlanComposed
				&& step.Status == FindGroupDirectPacketLiveBoundaryTraceStepStatus.NonLiveEvidenceAvailable
				&& step.Requirement.Contains("FindGroupConnectionBoundaryDispatchAdapterPlan", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketLiveBoundaryTraceStepKind.DirectPacketExecutorInvokedFromBoundary
				&& step.Status == FindGroupDirectPacketLiveBoundaryTraceStepStatus.BlockedPendingLiveBoundary
				&& step.Requirement.Contains("not by a test-only opt-in path", StringComparison.Ordinal));
		Assert.Contains(
			contract.RequiredOrderedSteps,
			step => step.Kind == FindGroupDirectPacketLiveBoundaryTraceStepKind.BoundaryTraceCaptured
				&& step.Requirement.Contains("one ordered trace", StringComparison.Ordinal)
				&& step.Requirement.Contains("no parsed-only actions", StringComparison.Ordinal));
	}
}
