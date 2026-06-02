using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostCSharpLiveTraceRowFixturePlanServiceTests
{
	[Fact]
	public void Create_KeepsPlanBlockedNonLiveAndDispatchDisabled()
	{
		var plan = FindGroupMutationPostCSharpLiveTraceRowFixturePlanService.Create();

		Assert.Equal(FindGroupMutationPostCSharpLiveTraceRowFixturePlanStatus.BlockedPendingLiveBoundaryFixture, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.False(plan.ShouldInvokeLiveSideEffects);
		Assert.True(plan.RequiresLiveBoundaryFixture);
		Assert.True(plan.RequiresLiveEmitter);
		Assert.True(plan.RequiresRegistryObservation);
		Assert.True(plan.RequiresGeneratedJavaArtifacts);
		Assert.True(plan.FeedsArtifactComparisonPreflight);
		Assert.False(plan.ReadyForRuntimeComparison);
		Assert.Equal("GameServerConnectionFindGroupMutationPostLiveTraceRowFixture", plan.FixtureClassName);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", plan.TraceName);
	}

	[Fact]
	public void Create_CoversActionTwoAndSixOnlyWithStableStepOrder()
	{
		var plan = FindGroupMutationPostCSharpLiveTraceRowFixturePlanService.Create();

		Assert.Equal([2, 6], plan.Actions);
		Assert.Equal(9, plan.Steps.Count);
		Assert.Equal(Enumerable.Range(1, plan.Steps.Count), plan.Steps.Select(step => step.Order));
		Assert.Contains("addRecruitment/addApplication", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_PreservesLiveDispatchGuard()
	{
		var plan = FindGroupMutationPostCSharpLiveTraceRowFixturePlanService.Create();

		Assert.Contains(plan.Steps, step =>
			step.Kind == FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.LiveDispatchGuard
			&& step.Status == FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.DesignOnly
			&& step.Target.Contains("ProcessPacketAsync", StringComparison.Ordinal)
			&& step.RequiredEvidence.Contains("Keep production live dispatch disabled", StringComparison.Ordinal)
			&& step.Notes.Contains("deferred live-dispatch gate", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RequiresBoundaryAcceptanceFromRealConnectionBoundary()
	{
		var plan = FindGroupMutationPostCSharpLiveTraceRowFixturePlanService.Create();

		Assert.Contains(plan.Steps, step =>
			step.Kind == FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.BoundaryAcceptanceTrace
			&& step.Status == FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.BlockedPendingLiveBoundaryFixture
			&& step.Target.Contains("live CmFindGroup branch", StringComparison.Ordinal)
			&& step.TraceFields.Contains("boundaryAccepted", StringComparison.Ordinal)
			&& step.TraceFields.Contains("activePlayerRace", StringComparison.Ordinal)
			&& step.Notes.Contains("not from CreateDisabledFindGroupBoundaryPlan", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RequiresJavaSpecificMutationPostDirectPacketRows()
	{
		var plan = FindGroupMutationPostCSharpLiveTraceRowFixturePlanService.Create();

		Assert.Contains(plan.Steps, step =>
			step.Kind == FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.SharedSingletonMutationTrace
			&& step.TraceFields.Contains("stateMutationRecordedBeforeDirectPackets", StringComparison.Ordinal)
			&& step.Notes.Contains("Action 2", StringComparison.Ordinal)
			&& step.Notes.Contains("Action 6", StringComparison.Ordinal));
		Assert.Contains(plan.Steps, step =>
			step.Kind == FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.DirectPacketIntentTrace
			&& step.TraceFields.Contains("postedSystemMessageId", StringComparison.Ordinal)
			&& step.RequiredEvidence.Contains("posted system message", StringComparison.Ordinal)
			&& step.Notes.Contains("1400392", StringComparison.Ordinal)
			&& step.Notes.Contains("1400393", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RequiresExecutorAndRegistryObservationBeforeRowSerialization()
	{
		var plan = FindGroupMutationPostCSharpLiveTraceRowFixturePlanService.Create();

		var executor = Assert.Single(plan.Steps, step => step.Kind == FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.BoundaryExecutorTrace);
		var registry = Assert.Single(plan.Steps, step => step.Kind == FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.RegistrySendObservationTrace);
		var serialization = Assert.Single(plan.Steps, step => step.Kind == FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.RuntimeRowSerialization);

		Assert.True(executor.Order < registry.Order);
		Assert.True(registry.Order < serialization.Order);
		Assert.Contains("executorInvokedFromBoundary=true", executor.TraceFields, StringComparison.Ordinal);
		Assert.Contains("registrySendsObservedInOrder=true", registry.TraceFields, StringComparison.Ordinal);
		Assert.Contains("worldBroadcastCount=0", registry.TraceFields, StringComparison.Ordinal);
		Assert.Contains("inviteDispatchCount=0", registry.TraceFields, StringComparison.Ordinal);
		Assert.Contains("schemaVersion", serialization.TraceFields, StringComparison.Ordinal);
		Assert.Contains("visibleEntryObjectIdsAfterMutation", serialization.TraceFields, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_EndsAtArtifactComparisonPreflightWithoutClaimingComparison()
	{
		var plan = FindGroupMutationPostCSharpLiveTraceRowFixturePlanService.Create();

		var step = Assert.Single(plan.Steps, step => step.Kind == FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.ComparisonPreflightInput);

		Assert.Contains("FindGroupMutationPostArtifactComparisonPreflightService", step.Target, StringComparison.Ordinal);
		Assert.Contains("hasLiveCSharpTraceRows=true", step.TraceFields, StringComparison.Ordinal);
		Assert.Contains("BlockedMissingJavaArtifacts", step.Notes, StringComparison.Ordinal);
		Assert.False(plan.ReadyForRuntimeComparison);
	}
}
