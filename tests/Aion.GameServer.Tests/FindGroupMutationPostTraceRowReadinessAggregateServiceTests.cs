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
		Assert.True(aggregate.HasRegistryObservationContract);
		Assert.True(aggregate.HasArtifactComparisonPreflight);
		Assert.True(aggregate.NeedsJavaFixture);
		Assert.True(aggregate.NeedsJavaInstrumentation);
		Assert.True(aggregate.NeedsGeneratedJavaArtifacts);
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

		Assert.Equal(4, aggregate.Rows.Count);
		Assert.Equal(Enumerable.Range(1, aggregate.Rows.Count), aggregate.Rows.Select(row => row.Order));
		Assert.Equal(
			[
				FindGroupMutationPostTraceRowReadinessBlocker.JavaCaptureRunbook,
				FindGroupMutationPostTraceRowReadinessBlocker.CSharpLiveTraceRowFixturePlan,
				FindGroupMutationPostTraceRowReadinessBlocker.RegistryObservationContract,
				FindGroupMutationPostTraceRowReadinessBlocker.ArtifactComparisonPreflight,
			],
			aggregate.Rows.Select(row => row.Blocker));
	}

	[Fact]
	public void Create_JavaRunbookRowNamesFixtureArtifactsAndFocusedMavenCommand()
	{
		var aggregate = FindGroupMutationPostTraceRowReadinessAggregateService.Create();

		Assert.Contains(aggregate.Rows, row =>
			row.Blocker == FindGroupMutationPostTraceRowReadinessBlocker.JavaCaptureRunbook
			&& row.Status == FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingJavaFixture
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
}
