using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostJavaArtifactCaptureImplementationReadinessServiceTests
{
	[Fact]
	public void Create_DefaultReadinessIsBlockedAndNonLive()
	{
		var readiness = FindGroupMutationPostJavaArtifactCaptureImplementationReadinessService.Create();

		Assert.Equal(FindGroupMutationPostJavaArtifactCaptureImplementationReadinessStatus.BlockedMissingJavaInstrumentation, readiness.Status);
		Assert.False(readiness.IsLive);
		Assert.True(readiness.RequiresJavaFixture);
		Assert.True(readiness.RequiresJavaInstrumentation);
		Assert.True(readiness.RequiresTraceSerializer);
		Assert.True(readiness.RequiresGeneratedArtifacts);
		Assert.False(readiness.ReadyForRuntimeComparison);
		Assert.Equal("FindGroupMutationPostTraceCaptureTest", readiness.FixtureClassName);
		Assert.Equal("aion.findGroupMutationPost.capture", readiness.CaptureFlag);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", readiness.TraceName);
	}

	[Fact]
	public void Create_ListsStableImplementationTaskRows()
	{
		var readiness = FindGroupMutationPostJavaArtifactCaptureImplementationReadinessService.Create();

		Assert.Equal(8, readiness.Tasks.Count);
		Assert.Equal(Enumerable.Range(1, readiness.Tasks.Count), readiness.Tasks.Select(task => task.Order));
		Assert.Equal(
			[
				FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FixtureClass,
				FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FixtureScenarios,
				FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.InstrumentationHooks,
				FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.TraceSerializer,
				FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ArtifactFiles,
				FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ArtifactValidation,
				FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FocusedMavenCommand,
				FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ComparisonHandOff,
			],
			readiness.Tasks.Select(task => task.Kind));
		Assert.True(readiness.HasFixtureTask);
		Assert.True(readiness.HasTraceSerializerTask);
		Assert.True(readiness.HasArtifactValidationTask);
		Assert.True(readiness.HasFocusedMavenCommand);
	}

	[Fact]
	public void Create_FixtureRowsNameFutureJavaTestAndActionScenarios()
	{
		var readiness = FindGroupMutationPostJavaArtifactCaptureImplementationReadinessService.Create();

		var fixture = Single(readiness, FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FixtureClass);
		var scenarios = Single(readiness, FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FixtureScenarios);

		Assert.Contains("FindGroupMutationPostTraceCaptureTest.java", fixture.Target, StringComparison.Ordinal);
		Assert.Equal(FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.DesignOnly, fixture.Status);
		Assert.Contains("capture disabled unless", fixture.RequiredWork, StringComparison.Ordinal);
		Assert.Contains("Fixture scaffold exists", fixture.Notes, StringComparison.Ordinal);
		Assert.True(readiness.HasActionTwoScenario);
		Assert.True(readiness.HasActionSixScenario);
		Assert.Contains("action 2 recruitment", scenarios.RequiredWork, StringComparison.Ordinal);
		Assert.Contains("action 6 application", scenarios.RequiredWork, StringComparison.Ordinal);
		Assert.Contains("targeted Maven test", scenarios.Notes, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_InstrumentationRowNamesJavaHookPointsAndOrderingGuard()
	{
		var readiness = FindGroupMutationPostJavaArtifactCaptureImplementationReadinessService.Create();

		var hooks = Single(readiness, FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.InstrumentationHooks);

		Assert.True(readiness.HasInstrumentationHooks);
		Assert.Equal(FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.BlockedMissingJavaInstrumentation, hooks.Status);
		Assert.Contains("CM_FIND_GROUP.readImpl/runImpl", hooks.JavaSource, StringComparison.Ordinal);
		Assert.Contains("FindGroupService.addRecruitment/addApplication/showRecruitments/showApplications", hooks.JavaSource, StringComparison.Ordinal);
		Assert.Contains("client_packet_payload_parsed", hooks.RequiredWork, StringComparison.Ordinal);
		Assert.Contains("application_refreshed_list_send_observed", hooks.RequiredWork, StringComparison.Ordinal);
		Assert.Contains("mutation before posted system message before refreshed list", hooks.AcceptanceEvidence, StringComparison.Ordinal);
		Assert.Contains("must not add synchronization", hooks.Notes, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_SerializerAndArtifactRowsUseSchemaAndStablePaths()
	{
		var readiness = FindGroupMutationPostJavaArtifactCaptureImplementationReadinessService.Create();

		var serializer = Single(readiness, FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.TraceSerializer);
		var files = Single(readiness, FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ArtifactFiles);

		Assert.Equal("parity-artifacts/find-group/mutation-post/java", readiness.ArtifactRoot);
		Assert.Equal(
			[
				"parity-artifacts/find-group/mutation-post/java/cm-find-group-direct-mutation-post-boundary-action-2-java.json",
				"parity-artifacts/find-group/mutation-post/java/cm-find-group-direct-mutation-post-boundary-action-6-java.json",
			],
			readiness.ExpectedArtifactPaths);
		Assert.Equal(FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.BlockedMissingTraceSerializer, serializer.Status);
		Assert.Contains("traceSource=Java", serializer.RequiredWork, StringComparison.Ordinal);
		Assert.Contains("all 22 schema fields", serializer.RequiredWork, StringComparison.Ordinal);
		Assert.Equal(FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.BlockedMissingGeneratedArtifacts, files.Status);
		Assert.Contains("Do not treat files as parity evidence", files.Notes, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ValidationAndMavenRowsStayDesignOnlyUntilFixtureExists()
	{
		var readiness = FindGroupMutationPostJavaArtifactCaptureImplementationReadinessService.Create();

		var validation = Single(readiness, FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ArtifactValidation);
		var maven = Single(readiness, FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FocusedMavenCommand);

		Assert.Equal(FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.DesignOnly, validation.Status);
		Assert.Contains("FindGroupMutationPostJavaTraceArtifactValidatorService", validation.Target, StringComparison.Ordinal);
		Assert.Contains("zero broadcast/invite counts", validation.RequiredWork, StringComparison.Ordinal);
		Assert.Contains("live C# rows", validation.Notes, StringComparison.Ordinal);
		Assert.Equal(
			"mvn -pl game-server -am test \"-Dtest=FindGroupMutationPostTraceCaptureTest\" \"-Daion.findGroupMutationPost.capture=true\" \"-Dmaven.test.skip=false\" \"-Dsurefire.failIfNoSpecifiedTests=false\"",
			readiness.FocusedMavenCommand);
		Assert.Equal(readiness.FocusedMavenCommand, maven.Target);
		Assert.Equal(FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.DesignOnly, maven.Status);
		Assert.Contains("not runnable evidence yet", maven.Notes, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ComparisonHandOffRefusesVerifiedParityClaim()
	{
		var readiness = FindGroupMutationPostJavaArtifactCaptureImplementationReadinessService.Create();

		var handoff = Single(readiness, FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ComparisonHandOff);

		Assert.Contains("FindGroupMutationPostArtifactComparisonPreflightService", handoff.Target, StringComparison.Ordinal);
		Assert.Contains("live C# rows", handoff.RequiredWork, StringComparison.Ordinal);
		Assert.Contains("comparison mismatches", handoff.AcceptanceEvidence, StringComparison.Ordinal);
		Assert.Contains("Do not claim verified parity", handoff.Notes, StringComparison.Ordinal);
		Assert.Contains("CM_FIND_GROUP.readImpl/runImpl actions 2 and 6", readiness.JavaSource, StringComparison.Ordinal);
	}

	private static FindGroupMutationPostJavaArtifactCaptureImplementationTask Single(
		FindGroupMutationPostJavaArtifactCaptureImplementationReadiness readiness,
		FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind kind) =>
		readiness.Tasks.Single(task => task.Kind == kind);
}
