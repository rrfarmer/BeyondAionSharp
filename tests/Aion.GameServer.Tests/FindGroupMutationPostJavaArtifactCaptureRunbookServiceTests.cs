using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostJavaArtifactCaptureRunbookServiceTests
{
	[Fact]
	public void Create_KeepsRunbookBlockedAndNonLive()
	{
		var runbook = FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();

		Assert.Equal(FindGroupMutationPostJavaArtifactCaptureRunbookStatus.BlockedMissingJavaInstrumentation, runbook.Status);
		Assert.False(runbook.IsLive);
		Assert.True(runbook.RequiresJavaFixture);
		Assert.True(runbook.RequiresJavaInstrumentation);
		Assert.True(runbook.RequiresTraceSerializer);
		Assert.True(runbook.RequiresGeneratedArtifacts);
		Assert.False(runbook.ReadyForRuntimeComparison);
		Assert.Equal("FindGroupMutationPostTraceCaptureTest", runbook.FixtureClassName);
		Assert.Equal("aion.findGroupMutationPost.capture", runbook.CaptureFlag);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", runbook.TraceName);
	}

	[Fact]
	public void Create_ListsStableStepOrderAndJavaSources()
	{
		var runbook = FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();

		Assert.Equal(10, runbook.Steps.Count);
		Assert.Equal(Enumerable.Range(1, runbook.Steps.Count), runbook.Steps.Select(step => step.Order));
		Assert.Contains(runbook.Steps, step =>
			step.Kind == FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.JavaFixtureClass
			&& step.Status == FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.DesignOnly
			&& step.Target.Contains("FindGroupMutationPostTraceCaptureTest.java", StringComparison.Ordinal)
			&& step.Notes.Contains("Maven-runnable", StringComparison.Ordinal));
		Assert.Contains(runbook.Steps, step =>
			step.Kind == FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ClientPacketRunImplHook
			&& step.JavaSource == "CM_FIND_GROUP.runImpl"
			&& step.Requirement.Contains("activePlayerRace", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RecordsActionTwoAndSixMutationHooks()
	{
		var runbook = FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();

		Assert.Contains(runbook.Steps, step =>
			step.Kind == FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.RecruitmentMutationHooks
			&& step.Target.Contains("action 2", StringComparison.Ordinal)
			&& step.Requirement.Contains("SmSystemMessage id 1400392", StringComparison.Ordinal)
			&& step.Requirement.Contains("SmFindGroup action 0", StringComparison.Ordinal)
			&& step.Notes.Contains("mutation-before-posted-message-before-refreshed-list", StringComparison.Ordinal));
		Assert.Contains(runbook.Steps, step =>
			step.Kind == FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ApplicationMutationHooks
			&& step.Target.Contains("action 6", StringComparison.Ordinal)
			&& step.Requirement.Contains("SmSystemMessage id 1400393", StringComparison.Ordinal)
			&& step.Requirement.Contains("SmFindGroup action 4", StringComparison.Ordinal)
			&& step.Notes.Contains("mutation-before-posted-message-before-refreshed-list", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DefinesArtifactPathsSerializerAndValidatorFlow()
	{
		var runbook = FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();

		Assert.True(runbook.HasActionTwoArtifactPath);
		Assert.True(runbook.HasActionSixArtifactPath);
		Assert.True(runbook.ReusesMutationPostSchema);
		Assert.True(runbook.ReusesArtifactValidator);
		Assert.True(runbook.FeedsComparisonPreflight);
		Assert.Equal(
			[
				"parity-artifacts/find-group/mutation-post/java/cm-find-group-direct-mutation-post-boundary-action-2-java.json",
				"parity-artifacts/find-group/mutation-post/java/cm-find-group-direct-mutation-post-boundary-action-6-java.json",
			],
			runbook.ExpectedArtifactPaths);
		Assert.Contains(runbook.Steps, step =>
			step.Kind == FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.TraceSerializer
			&& step.Requirement.Contains("traceSource=Java", StringComparison.Ordinal)
			&& step.Requirement.Contains("all 22 schema fields", StringComparison.Ordinal));
		Assert.Contains(runbook.Steps, step =>
			step.Kind == FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ArtifactValidation
			&& step.Target.Contains("FindGroupMutationPostJavaTraceArtifactValidatorService", StringComparison.Ordinal)
			&& step.Requirement.Contains("zero broadcast/invite counts", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_NamesFocusedMavenCommandButMarksItDesignOnly()
	{
		var runbook = FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();

		Assert.Equal(
			"mvn -pl game-server -am test \"-Dtest=FindGroupMutationPostTraceCaptureTest\" \"-Daion.findGroupMutationPost.capture=true\" \"-Dmaven.test.skip=false\" \"-Dsurefire.failIfNoSpecifiedTests=false\"",
			runbook.FocusedMavenCommand);
		Assert.Contains(runbook.Steps, step =>
			step.Kind == FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.FocusedMavenCommand
			&& step.Status == FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.DesignOnly
			&& step.Requirement.Contains("Run only after the Java fixture", StringComparison.Ordinal)
			&& step.Notes.Contains("should not become a broad Maven run", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_EndsWithComparisonPreflightWithoutClaimingParity()
	{
		var runbook = FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();

		var preflight = Assert.Single(runbook.Steps, step => step.Kind == FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ComparisonPreflight);

		Assert.Contains("FindGroupMutationPostArtifactComparisonPreflightService", preflight.Target, StringComparison.Ordinal);
		Assert.Contains("live C# rows", preflight.Requirement, StringComparison.Ordinal);
		Assert.Contains("projected Java/C# rows", preflight.Notes, StringComparison.Ordinal);
		Assert.False(runbook.ReadyForRuntimeComparison);
	}
}
