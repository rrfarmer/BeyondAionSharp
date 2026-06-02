using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostJavaArtifactRootValidationCommandReportServiceTests
{
	[Fact]
	public void Create_MissingDirectoryNamesCaptureAndValidatorCommands()
	{
		var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

		var report = FindGroupMutationPostJavaArtifactRootValidationCommandReportService.Create(missing);

		Assert.Equal(FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.BlockedMissingDirectory, report.Status);
		Assert.False(report.IsLive);
		Assert.False(report.HasGeneratedJavaArtifacts);
		Assert.False(report.HasAllExpectedFiles);
		Assert.False(report.HasOnlyShapeValidArtifacts);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal("aion.findGroupMutationPost.serverEpochSeconds", report.DeterministicTimestampProperty);
		Assert.Equal(1700000000, report.DeterministicServerEpochSeconds);
		Assert.Contains("-Daion.findGroupMutationPost.capture=true", report.JavaCaptureCommand, StringComparison.Ordinal);
		Assert.Contains("-Daion.findGroupMutationPost.serverEpochSeconds=1700000000", report.JavaCaptureCommand, StringComparison.Ordinal);
		Assert.Contains($"-Daion.findGroupMutationPost.artifactRoot={missing}", report.JavaCaptureCommand, StringComparison.Ordinal);
		Assert.Contains("FindGroupMutationPostJavaTraceArtifactDirectoryReportServiceTests", report.CSharpValidatorCommand, StringComparison.Ordinal);
		Assert.Contains("FindGroupMutationPostJavaTraceArtifactValidatorServiceTests", report.CSharpValidatorCommand, StringComparison.Ordinal);
		Assert.Equal([2, 6], report.Rows.Select(row => row.Action));
		Assert.All(report.Rows, row =>
		{
			Assert.False(row.HasFile);
			Assert.False(row.IsShapeValid);
			Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile, row.FileStatus);
		});
		Assert.Contains("directory is missing", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ShapeValidArtifactsRemainRuntimeComparisonBlocked()
	{
		var directory = CreateTempDirectory();
		try
		{
			File.WriteAllText(Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2)), ActionTwoArtifactJson);
			File.WriteAllText(Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(6)), ActionSixArtifactJson);

			var report = FindGroupMutationPostJavaArtifactRootValidationCommandReportService.Create(directory);

			Assert.Equal(FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.ShapeValidRuntimeComparisonBlocked, report.Status);
			Assert.True(report.HasGeneratedJavaArtifacts);
			Assert.True(report.HasAllExpectedFiles);
			Assert.True(report.HasOnlyShapeValidArtifacts);
			Assert.False(report.ReadyForRuntimeComparison);
			Assert.All(report.Rows, row =>
			{
				Assert.True(row.HasFile);
				Assert.True(row.IsShapeValid);
				Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid, row.FileStatus);
				Assert.Contains("shape-valid only", row.Notes, StringComparison.Ordinal);
			});
			Assert.Contains("shape-valid only", report.ExecutionDecision, StringComparison.Ordinal);
			Assert.Contains("runtime comparison evidence", report.ExecutionDecision, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public void Create_InvalidArtifactKeepsValidatorTargetAndBlocksComparison()
	{
		var directory = CreateTempDirectory();
		try
		{
			File.WriteAllText(Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2)), ActionTwoArtifactJson);
			File.WriteAllText(
				Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(6)),
				ActionSixArtifactJson.Replace("\"traceSource\": \"Java\"", "\"traceSource\": \"CSharp\"", StringComparison.Ordinal));

			var report = FindGroupMutationPostJavaArtifactRootValidationCommandReportService.Create(directory);

			Assert.Equal(FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.BlockedInvalidArtifacts, report.Status);
			Assert.True(report.HasGeneratedJavaArtifacts);
			Assert.True(report.HasAllExpectedFiles);
			Assert.False(report.HasOnlyShapeValidArtifacts);
			Assert.Contains(report.Rows, row =>
				row.Action == 6
				&& row.HasFile
				&& !row.IsShapeValid
				&& row.FileStatus == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.InvalidArtifact
				&& row.ValidatorTarget.Contains("FindGroupMutationPostJavaTraceArtifactValidatorService", StringComparison.Ordinal));
			Assert.Contains("failed schema/action validation", report.ExecutionDecision, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	private static string CreateTempDirectory()
	{
		var directory = Path.Combine(Path.GetTempPath(), $"find-group-mutation-post-root-{Guid.NewGuid():N}");
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
		      "activePlayerObjectId": 16909060,
		      "activePlayerRace": "ELYOS",
		      "serverEpochSeconds": 1700000000,
		      "mutationKind": "Recruitment",
		      "mutatedEntryObjectId": 16909060,
		      "stateMutationRecordedBeforeDirectPackets": true,
		      "postedSystemMessageRecipientObjectId": 16909060,
		      "postedSystemMessageType": "SmSystemMessage",
		      "postedSystemMessageId": 1400392,
		      "refreshedListRecipientObjectId": 16909060,
		      "refreshedListPacketType": "SmFindGroup",
		      "refreshedListAction": 0,
		      "visibleEntryObjectIdsAfterMutation": [16909060],
		      "executorInvokedFromBoundary": true,
		      "registrySendsObservedInOrder": true,
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
		      "activePlayerObjectId": 16909061,
		      "activePlayerRace": "ASMODIANS",
		      "serverEpochSeconds": 1700000000,
		      "mutationKind": "Application",
		      "mutatedEntryObjectId": 16909061,
		      "stateMutationRecordedBeforeDirectPackets": true,
		      "postedSystemMessageRecipientObjectId": 16909061,
		      "postedSystemMessageType": "SmSystemMessage",
		      "postedSystemMessageId": 1400393,
		      "refreshedListRecipientObjectId": 16909061,
		      "refreshedListPacketType": "SmFindGroup",
		      "refreshedListAction": 4,
		      "visibleEntryObjectIdsAfterMutation": [16909061],
		      "executorInvokedFromBoundary": true,
		      "registrySendsObservedInOrder": true,
		      "worldBroadcastCount": 0,
		      "inviteDispatchCount": 0
		    }
		  ]
		}
		""";
}
