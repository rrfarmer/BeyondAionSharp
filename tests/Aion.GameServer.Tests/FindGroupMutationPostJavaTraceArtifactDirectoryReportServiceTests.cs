using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostJavaTraceArtifactDirectoryReportServiceTests
{
	[Fact]
	public void Create_MissingDirectoryReportsExpectedFilesAsBlocked()
	{
		var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

		var report = FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(missing);

		Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryStatus.MissingDirectory, report.Status);
		Assert.False(report.HasGeneratedJavaArtifacts);
		Assert.False(report.HasAllExpectedFiles);
		Assert.False(report.HasOnlyShapeValidArtifacts);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal([2, 6], report.Files.Select(file => file.Action));
		Assert.All(report.Files, file =>
		{
			Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile, file.Status);
			Assert.Null(file.ValidationReport);
			Assert.Contains("missing", file.Notes, StringComparison.OrdinalIgnoreCase);
		});
	}

	[Fact]
	public void Create_EmptyDirectoryReportsMissingExpectedFiles()
	{
		var directory = CreateTempDirectory();
		try
		{
			var report = FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(directory);

			Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryStatus.MissingExpectedFiles, report.Status);
			Assert.False(report.HasGeneratedJavaArtifacts);
			Assert.False(report.HasAllExpectedFiles);
			Assert.False(report.ReadyForRuntimeComparison);
			Assert.Equal(2, report.Files.Count);
			Assert.All(report.Files, file => Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile, file.Status));
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public void Create_ValidExpectedArtifactsReportShapeValidButNotRuntimeReady()
	{
		var directory = CreateTempDirectory();
		try
		{
			File.WriteAllText(Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2)), ActionTwoArtifactJson);
			File.WriteAllText(Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(6)), ActionSixArtifactJson);

			var report = FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(directory);

			Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid, report.Status);
			Assert.True(report.HasGeneratedJavaArtifacts);
			Assert.True(report.HasAllExpectedFiles);
			Assert.True(report.HasOnlyShapeValidArtifacts);
			Assert.False(report.ReadyForRuntimeComparison);
			Assert.Contains("shape-valid only", report.Notes, StringComparison.Ordinal);
			Assert.All(report.Files, file =>
			{
				Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid, file.Status);
				Assert.NotNull(file.ValidationReport);
				Assert.True(file.ValidationReport.IsValid);
				Assert.Contains(file.ValidationReport.Metadata!.TraceRows, row => row.Action == file.Action);
			});
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public void Create_InvalidArtifactAggregatesValidatorIssues()
	{
		var directory = CreateTempDirectory();
		try
		{
			File.WriteAllText(Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2)), ActionTwoArtifactJson);
			File.WriteAllText(
				Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(6)),
				ActionSixArtifactJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal));

			var report = FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(directory);

			Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryStatus.InvalidArtifacts, report.Status);
			Assert.True(report.HasGeneratedJavaArtifacts);
			Assert.True(report.HasAllExpectedFiles);
			Assert.False(report.HasOnlyShapeValidArtifacts);
			Assert.False(report.ReadyForRuntimeComparison);
			Assert.Contains(report.Files, file =>
				file.Action == 6
				&& file.Status == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.InvalidArtifact
				&& file.ValidationReport!.Issues.Any(issue => issue.Code == FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnsupportedSchemaVersion));
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public void Create_ShapeValidArtifactStillRequiresExpectedActionInFile()
	{
		var directory = CreateTempDirectory();
		try
		{
			File.WriteAllText(Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2)), ActionSixArtifactJson);
			File.WriteAllText(Path.Combine(directory, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(6)), ActionSixArtifactJson);

			var report = FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(directory);

			Assert.Equal(FindGroupMutationPostJavaTraceArtifactDirectoryStatus.InvalidArtifacts, report.Status);
			Assert.Contains(report.Files, file =>
				file.Action == 2
				&& file.Status == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingExpectedAction
				&& file.ValidationReport!.IsValid);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	private static string CreateTempDirectory()
	{
		var directory = Path.Combine(Path.GetTempPath(), $"find-group-mutation-post-{Guid.NewGuid():N}");
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
		      "serverEpochSeconds": 200,
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
		      "serverEpochSeconds": 201,
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
