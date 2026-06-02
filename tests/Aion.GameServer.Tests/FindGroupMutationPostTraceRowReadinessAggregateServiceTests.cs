using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostTraceRowReadinessAggregateServiceTests
{
	[Fact]
	public void Create_DefaultAggregateBlocksOnJavaCaptureAndIsNonLive()
	{
		var aggregate = FindGroupMutationPostTraceRowReadinessAggregateService.Create();

		Assert.Equal(FindGroupMutationPostTraceRowReadinessStatus.BlockedMissingJavaCapture, aggregate.Status);
		Assert.False(aggregate.IsLive);
		Assert.True(aggregate.HasJavaCaptureRunbook);
		Assert.True(aggregate.HasCSharpLiveTraceRowFixturePlan);
		Assert.True(aggregate.HasGuardedLiveBoundaryFixtureSkeleton);
		Assert.True(aggregate.HasRegistryObservationContract);
		Assert.True(aggregate.HasArtifactComparisonPreflight);
		Assert.True(aggregate.NeedsJavaFixture);
		Assert.True(aggregate.NeedsJavaInstrumentation);
		Assert.True(aggregate.NeedsGeneratedJavaArtifacts);
		Assert.False(aggregate.HasCSharpTraceRowShapeInputs);
		Assert.True(aggregate.NeedsGuardedBoundaryFixture);
		Assert.True(aggregate.NeedsCSharpLiveRows);
		Assert.True(aggregate.NeedsRegistryObservation);
		Assert.True(aggregate.NeedsComparisonExecution);
		Assert.False(aggregate.ReadyForRuntimeComparison);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", aggregate.TraceName);
		Assert.Contains("addRecruitment/addApplication", aggregate.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ListsStableRowsForEachAggregateInput()
	{
		var aggregate = FindGroupMutationPostTraceRowReadinessAggregateService.Create();

		Assert.Equal(5, aggregate.Rows.Count);
		Assert.Equal(Enumerable.Range(1, aggregate.Rows.Count), aggregate.Rows.Select(row => row.Order));
		Assert.Equal(
			[
				FindGroupMutationPostTraceRowReadinessBlocker.JavaCaptureRunbook,
				FindGroupMutationPostTraceRowReadinessBlocker.CSharpLiveTraceRowFixturePlan,
				FindGroupMutationPostTraceRowReadinessBlocker.GuardedLiveBoundaryFixtureSkeleton,
				FindGroupMutationPostTraceRowReadinessBlocker.RegistryObservationContract,
				FindGroupMutationPostTraceRowReadinessBlocker.ArtifactComparisonPreflight,
			],
			aggregate.Rows.Select(row => row.Blocker));
	}

	[Fact]
	public void Create_GuardedFixtureSkeletonRowKeepsProductionDispatchDisabled()
	{
		var aggregate = FindGroupMutationPostTraceRowReadinessAggregateService.Create();

		Assert.Contains(aggregate.Rows, row =>
			row.Blocker == FindGroupMutationPostTraceRowReadinessBlocker.GuardedLiveBoundaryFixtureSkeleton
			&& row.Status == FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingCSharpLiveFixture
			&& row.Evidence.Contains("GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture", StringComparison.Ordinal)
			&& row.Evidence.Contains("traceGuard=AION_FIND_GROUP_MUTATION_POST_TRACE_GUARD", StringComparison.Ordinal)
			&& row.Evidence.Contains("productionDispatch=False", StringComparison.Ordinal)
			&& row.Evidence.Contains("sendsPackets=False", StringComparison.Ordinal)
			&& row.Evidence.Contains("missingExecutor=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("missingRegistry=True", StringComparison.Ordinal)
			&& row.Notes.Contains("guarded boundary execution", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithShapeInputsStillRequiresGuardedBoundaryLiveRows()
	{
		var javaArtifacts = RepositoryJavaArtifacts();
		var csharpFixture = FindGroupMutationPostCSharpTraceRowFixtureReportService.Create(
			[
				FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(2),
				FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(6),
			],
			javaArtifacts);
		var preflight = FindGroupMutationPostArtifactComparisonPreflightService.Create(
			javaArtifacts: javaArtifacts,
			csharpFixtureReport: csharpFixture);
		var skeleton = FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.Create(preflight: preflight);

		var aggregate = FindGroupMutationPostTraceRowReadinessAggregateService.Create(
			comparisonPreflight: preflight,
			guardedFixtureSkeleton: skeleton);

		Assert.True(aggregate.HasCSharpTraceRowShapeInputs);
		Assert.True(aggregate.NeedsGuardedBoundaryFixture);
		Assert.True(aggregate.NeedsCSharpLiveRows);
		Assert.True(aggregate.NeedsRegistryObservation);
		Assert.Equal(FindGroupMutationPostTraceRowReadinessStatus.BlockedMissingJavaCapture, aggregate.Status);
		Assert.Contains(aggregate.Rows, row =>
			row.Blocker == FindGroupMutationPostTraceRowReadinessBlocker.GuardedLiveBoundaryFixtureSkeleton
			&& row.Evidence.Contains("csharpShapeInputs=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("liveRows=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_JavaRunbookRowNamesFixtureArtifactsAndFocusedMavenCommand()
	{
		var aggregate = FindGroupMutationPostTraceRowReadinessAggregateService.Create();

		Assert.Contains(aggregate.Rows, row =>
			row.Blocker == FindGroupMutationPostTraceRowReadinessBlocker.JavaCaptureRunbook
			&& row.Status == FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingJavaInstrumentation
			&& row.BlocksRuntimeComparison
			&& row.Evidence.Contains("FindGroupMutationPostTraceCaptureTest", StringComparison.Ordinal)
			&& row.Evidence.Contains("artifacts=2", StringComparison.Ordinal)
			&& row.Evidence.Contains("mvn -pl game-server", StringComparison.Ordinal)
			&& row.Notes.Contains("Java fixture", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CSharpFixtureRowKeepsBoundaryDisabled()
	{
		var aggregate = FindGroupMutationPostTraceRowReadinessAggregateService.Create();

		Assert.Contains(aggregate.Rows, row =>
			row.Blocker == FindGroupMutationPostTraceRowReadinessBlocker.CSharpLiveTraceRowFixturePlan
			&& row.Status == FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingCSharpLiveFixture
			&& row.Evidence.Contains("GameServerConnectionFindGroupMutationPostLiveTraceRowFixture", StringComparison.Ordinal)
			&& row.Evidence.Contains("boundaryWired=False", StringComparison.Ordinal)
			&& row.Evidence.Contains("invokeLiveSideEffects=False", StringComparison.Ordinal)
			&& row.Notes.Contains("real connection boundary fixture", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RegistryRowRequiresOrderedDirectSendsAndNoUnexpectedSideEffects()
	{
		var aggregate = FindGroupMutationPostTraceRowReadinessAggregateService.Create();

		Assert.Contains(aggregate.Rows, row =>
			row.Blocker == FindGroupMutationPostTraceRowReadinessBlocker.RegistryObservationContract
			&& row.Status == FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingRegistryObservation
			&& row.Evidence.Contains("orderedSends=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("twoDirectSends=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("zeroBroadcasts=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("zeroInvites=True", StringComparison.Ordinal)
			&& row.Notes.Contains("posted system message before refreshed list", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_PreflightRowReflectsDefaultMissingJavaArtifacts()
	{
		var aggregate = FindGroupMutationPostTraceRowReadinessAggregateService.Create();

		Assert.Contains(aggregate.Rows, row =>
			row.Blocker == FindGroupMutationPostTraceRowReadinessBlocker.ArtifactComparisonPreflight
			&& row.Status == FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingJavaArtifacts
			&& row.Evidence.Contains("status=BlockedMissingJavaArtifacts", StringComparison.Ordinal)
			&& row.Evidence.Contains("comparisonExecuted=False", StringComparison.Ordinal)
			&& row.Notes.Contains("verified parity", StringComparison.OrdinalIgnoreCase));
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
