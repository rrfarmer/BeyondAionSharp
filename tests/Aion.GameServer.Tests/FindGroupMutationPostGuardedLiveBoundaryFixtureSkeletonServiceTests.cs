using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonServiceTests
{
	[Fact]
	public void Create_KeepsSkeletonBlockedNonLiveAndProductionDispatchDisabled()
	{
		var skeleton = FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.Create();

		Assert.Equal(FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStatus.BlockedPendingGuardedBoundaryFixture, skeleton.Status);
		Assert.False(skeleton.IsLive);
		Assert.False(skeleton.IsProductionCmFindGroupDispatchEnabled);
		Assert.False(skeleton.ShouldSendPackets);
		Assert.True(skeleton.RequiresExplicitTraceGuard);
		Assert.False(skeleton.ReadyForRuntimeComparison);
		Assert.Equal("GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture", skeleton.FixtureClassName);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", skeleton.TraceName);
	}

	[Fact]
	public void Create_CoversActionTwoAndSixOnlyWithStableStepOrder()
	{
		var skeleton = FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.Create();

		Assert.Equal([2, 6], skeleton.Actions);
		Assert.Equal(7, skeleton.Steps.Count);
		Assert.Equal(Enumerable.Range(1, skeleton.Steps.Count), skeleton.Steps.Select(step => step.Order));
		Assert.Contains("addRecruitment/addApplication", skeleton.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_RequiresExplicitGuardAndPreservesDeferredProductionCase()
	{
		var skeleton = FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.Create();

		Assert.Contains(skeleton.Steps, step =>
			step.Kind == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ExplicitTraceGuard
			&& step.Target == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.TraceGuardName
			&& step.Status == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.DesignOnly
			&& !step.BlocksRuntimeComparison
			&& step.Notes.Contains("guard is absent", StringComparison.Ordinal));
		Assert.Contains(skeleton.Steps, step =>
			step.Kind == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ProductionDispatchGuard
			&& step.Target.Contains("ProcessPacketAsync", StringComparison.Ordinal)
			&& step.RequiredEvidence.Contains("production case deferred", StringComparison.Ordinal)
			&& step.Notes.Contains("fixturePlanBoundaryWired=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_NamesJavaActionTwoAndSixBoundaryScenarios()
	{
		var skeleton = FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.Create();

		Assert.Contains(skeleton.Steps, step =>
			step.Kind == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ActionTwoBoundaryScenario
			&& step.Status == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingFixture
			&& step.RequiredEvidence.Contains("posted system message 1400392", StringComparison.Ordinal)
			&& step.RequiredEvidence.Contains("SmFindGroup action 0", StringComparison.Ordinal)
			&& step.Notes.Contains("addRecruitment", StringComparison.Ordinal));
		Assert.Contains(skeleton.Steps, step =>
			step.Kind == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ActionSixBoundaryScenario
			&& step.Status == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingFixture
			&& step.RequiredEvidence.Contains("posted system message 1400393", StringComparison.Ordinal)
			&& step.RequiredEvidence.Contains("SmFindGroup action 4", StringComparison.Ordinal)
			&& step.Notes.Contains("addApplication", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RecordsExecutorAndRegistryObservationsAsMissing()
	{
		var skeleton = FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.Create();

		Assert.True(skeleton.RecordsMissingExecutorObservation);
		Assert.True(skeleton.RecordsMissingRegistryObservation);
		Assert.Contains(skeleton.Steps, step =>
			step.Kind == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ExecutorObservation
			&& step.Status == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingExecutorObservation
			&& step.RequiredEvidence.Contains("executorInvokedFromBoundary=true", StringComparison.Ordinal)
			&& step.Notes.Contains("opt-in executor calls outside the boundary remain insufficient", StringComparison.Ordinal));
		Assert.Contains(skeleton.Steps, step =>
			step.Kind == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.RegistryObservation
			&& step.Status == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingRegistryObservation
			&& step.RequiredEvidence.Contains("posted system message before refreshed list", StringComparison.Ordinal)
			&& step.RequiredEvidence.Contains("zero broadcast/invite counts", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CanCarryPreflightShapeInputsWithoutMarkingLiveRows()
	{
		var preflight = FindGroupMutationPostArtifactComparisonPreflightService.Create(
			javaArtifacts: RepositoryJavaArtifacts(),
			csharpFixtureReport: FindGroupMutationPostCSharpTraceRowFixtureReportService.Create(
				[
					FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(2),
					FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(6),
				],
				RepositoryJavaArtifacts()));

		var skeleton = FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.Create(preflight: preflight);

		Assert.True(skeleton.HasShapeValidJavaArtifacts);
		Assert.True(skeleton.HasCSharpShapeInputs);
		Assert.False(skeleton.HasLiveCSharpRows);
		var handoff = Assert.Single(skeleton.Steps, step => step.Kind == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ArtifactPreflightHandoff);
		Assert.Contains("javaArtifacts=True", handoff.Notes, StringComparison.Ordinal);
		Assert.Contains("csharpShapeInputs=True", handoff.Notes, StringComparison.Ordinal);
		Assert.Contains("liveRows=False", handoff.Notes, StringComparison.Ordinal);
		Assert.True(handoff.BlocksRuntimeComparison);
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryReport RepositoryJavaArtifacts()
	{
		var root = FindRepositoryRoot();
		return FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(
			Path.Combine(root, "parity-artifacts", "find-group", "mutation-post", "java"));
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "docs", "csharp-port.md")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new InvalidOperationException("Repository root could not be located.");
	}
}
