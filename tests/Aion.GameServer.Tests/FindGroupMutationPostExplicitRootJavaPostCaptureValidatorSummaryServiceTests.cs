using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryServiceTests
{
	[Fact]
	public void Create_MissingExplicitRootBlocksBeforeFilesystemValidation()
	{
		var summary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(string.Empty);

		Assert.Equal(FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedMissingExplicitRoot, summary.Status);
		Assert.False(summary.UsesExplicitRoot);
		Assert.False(summary.HasGeneratedJavaArtifacts);
		Assert.False(summary.CanRunRuntimeComparison);
		Assert.False(summary.CanClaimVerifiedParity);
		Assert.Contains("no explicit artifact root", summary.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_RepositoryRootIsRejectedAsExplicitCaptureRoot()
	{
		var summary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(
			FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot);

		Assert.Equal(FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedRepositoryArtifactRoot, summary.Status);
		Assert.True(summary.UsesRepositoryArtifactRoot);
		Assert.False(summary.CanRunRuntimeComparison);
		Assert.Contains("repository artifact root", summary.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_MissingDirectoryNamesExpectedRowsAndCaptureCommand()
	{
		var artifactRoot = Path.Combine(Path.GetTempPath(), $"find-group-post-capture-missing-{Guid.NewGuid():N}");

		var summary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(artifactRoot);

		Assert.Equal(FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedMissingDirectory, summary.Status);
		Assert.True(summary.UsesExplicitRoot);
		Assert.False(summary.HasGeneratedJavaArtifacts);
		Assert.False(summary.HasAllExpectedFiles);
		Assert.False(summary.HasOnlyShapeValidArtifacts);
		Assert.Contains("commandSuppliedArtifactRootPropertyWritesGuardedArtifacts", summary.JavaCaptureCommand, StringComparison.Ordinal);
		Assert.Contains($"-Daion.findGroupMutationPost.artifactRoot={artifactRoot}", summary.JavaCaptureCommand, StringComparison.Ordinal);
		Assert.Contains("allProvidersConsistent=True", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("selectedKind=ExecutorConsistencyAudit", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("commandDecisionRowsEvidence=", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("captureExecutionBlockerSummaryRows=", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("captureAcceptanceMatrixRows=", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("liveCapturePreflightRows=", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("runtimeComparisonHandoffRows=", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("consistencyAuditRowEvidence=", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("JavaArtifactRootValidationCommandReport=consistent:True", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Equal([2, 6], summary.Rows.Select(row => row.Action));
		Assert.All(summary.Rows, row =>
		{
			Assert.False(row.HasFile);
			Assert.False(row.IsShapeValid);
			Assert.Equal(0, row.TraceRowCount);
			Assert.Equal(0, row.ValidationIssueCount);
		});
	}

	[Fact]
	public void Create_PartialArtifactsRemainBlockedOnMissingExpectedFile()
	{
		var artifactRoot = CreateTempDirectory();
		try
		{
			File.WriteAllText(Path.Combine(artifactRoot, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2)), ActionTwoArtifactJson);

			var summary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(artifactRoot);

			Assert.Equal(FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedMissingExpectedFiles, summary.Status);
			Assert.True(summary.HasGeneratedJavaArtifacts);
			Assert.False(summary.HasAllExpectedFiles);
			Assert.False(summary.HasOnlyShapeValidArtifacts);
			Assert.Contains(summary.Rows, row =>
				row.Action == 2
				&& row.HasFile
				&& row.IsShapeValid
				&& row.TraceRowCount == 1
				&& row.ValidationIssueCount == 0);
			Assert.Contains(summary.Rows, row =>
				row.Action == 6
				&& !row.HasFile
				&& row.FileStatus == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile);
			Assert.Contains("expected action 2/6 Java artifacts are missing", summary.ExecutionDecision, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(artifactRoot, recursive: true);
		}
	}

	[Fact]
	public void Create_InvalidArtifactReportsValidationIssuesAndBlocksComparison()
	{
		var artifactRoot = CreateTempDirectory();
		try
		{
			File.WriteAllText(Path.Combine(artifactRoot, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2)), ActionTwoArtifactJson);
			File.WriteAllText(
				Path.Combine(artifactRoot, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(6)),
				ActionSixArtifactJson.Replace("\"postedSystemMessageId\": 1400393", "\"postedSystemMessageId\": 1400392", StringComparison.Ordinal));

			var summary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(artifactRoot);

			Assert.Equal(FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedInvalidArtifacts, summary.Status);
			Assert.True(summary.HasGeneratedJavaArtifacts);
			Assert.True(summary.HasAllExpectedFiles);
			Assert.False(summary.HasOnlyShapeValidArtifacts);
			Assert.Contains(summary.Rows, row =>
				row.Action == 6
				&& row.HasFile
				&& !row.IsShapeValid
				&& row.ValidationIssueCount > 0
				&& row.FileStatus == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.InvalidArtifact);
			Assert.False(summary.CanRunRuntimeComparison);
			Assert.Contains("failed schema/action validation", summary.ExecutionDecision, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(artifactRoot, recursive: true);
		}
	}

	[Fact]
	public void Create_ShapeValidArtifactsRemainRuntimeComparisonBlocked()
	{
		var artifactRoot = CreateTempDirectory();
		try
		{
			File.WriteAllText(Path.Combine(artifactRoot, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2)), ActionTwoArtifactJson);
			File.WriteAllText(Path.Combine(artifactRoot, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(6)), ActionSixArtifactJson);

			var summary = FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(artifactRoot);

			Assert.Equal(FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.ShapeValidRuntimeComparisonBlocked, summary.Status);
			Assert.True(summary.HasGeneratedJavaArtifacts);
			Assert.True(summary.HasAllExpectedFiles);
			Assert.True(summary.HasOnlyShapeValidArtifacts);
			Assert.False(summary.HasAcceptedLiveCSharpBoundaryRows);
			Assert.False(summary.CanRunRuntimeComparison);
			Assert.False(summary.CanClaimVerifiedParity);
			Assert.Contains("ResultEmissionBlocker", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
			Assert.Contains("JavaArtifactCaptureRunbook=consistent:True", summary.DryRunCommandConsistencyEvidence, StringComparison.Ordinal);
			Assert.All(summary.Rows, row =>
			{
				Assert.True(row.HasFile);
				Assert.True(row.IsShapeValid);
				Assert.Equal(1, row.TraceRowCount);
				Assert.Equal(0, row.ValidationIssueCount);
			});
			Assert.Contains("accepted live C# boundary rows", summary.ExecutionDecision, StringComparison.Ordinal);
			Assert.Contains("runtime comparison evidence", summary.ExecutionDecision, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(artifactRoot, recursive: true);
		}
	}

	private static string CreateTempDirectory()
	{
		var directory = Path.Combine(Path.GetTempPath(), $"find-group-post-capture-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);
		return directory;
	}

	private const string ActionTwoArtifactJson =
		"""
		{
		  "schemaVersion": 1,
		  "traceName": "cm-find-group-direct-mutation-post-boundary",
		  "traces": [
		    {
		      "schemaVersion": 1,
		      "traceName": "cm-find-group-direct-mutation-post-boundary",
		      "traceSource": "Java",
		      "action": 2,
		      "boundaryAccepted": true,
		      "activePlayerObjectId": 2002,
		      "activePlayerRace": "ELYOS",
		      "serverEpochSeconds": 1700000000,
		      "mutationKind": "Recruitment",
		      "mutatedEntryObjectId": 2002,
		      "stateMutationRecordedBeforeDirectPackets": true,
		      "postedSystemMessageRecipientObjectId": 2002,
		      "postedSystemMessageType": "SmSystemMessage",
		      "postedSystemMessageId": 1400392,
		      "refreshedListRecipientObjectId": 2002,
		      "refreshedListPacketType": "SmFindGroup",
		      "refreshedListAction": 0,
		      "visibleEntryObjectIdsAfterMutation": [2002, 3003],
		      "executorInvokedFromBoundary": false,
		      "registrySendsObservedInOrder": false,
		      "worldBroadcastCount": 0,
		      "inviteDispatchCount": 0
		    }
		  ]
		}
		""";

	private const string ActionSixArtifactJson =
		"""
		{
		  "schemaVersion": 1,
		  "traceName": "cm-find-group-direct-mutation-post-boundary",
		  "traces": [
		    {
		      "schemaVersion": 1,
		      "traceName": "cm-find-group-direct-mutation-post-boundary",
		      "traceSource": "Java",
		      "action": 6,
		      "boundaryAccepted": true,
		      "activePlayerObjectId": 4004,
		      "activePlayerRace": "ASMODIANS",
		      "serverEpochSeconds": 1700000000,
		      "mutationKind": "Application",
		      "mutatedEntryObjectId": 4004,
		      "stateMutationRecordedBeforeDirectPackets": true,
		      "postedSystemMessageRecipientObjectId": 4004,
		      "postedSystemMessageType": "SmSystemMessage",
		      "postedSystemMessageId": 1400393,
		      "refreshedListRecipientObjectId": 4004,
		      "refreshedListPacketType": "SmFindGroup",
		      "refreshedListAction": 4,
		      "visibleEntryObjectIdsAfterMutation": [4004],
		      "executorInvokedFromBoundary": false,
		      "registrySendsObservedInOrder": false,
		      "worldBroadcastCount": 0,
		      "inviteDispatchCount": 0
		    }
		  ]
		}
		""";
}
