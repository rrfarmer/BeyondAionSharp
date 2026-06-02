using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostJavaTraceArtifactFileReportServiceTests
{
	[Fact]
	public void Create_ListsBlockedActionTwoAndSixJavaArtifactTargets()
	{
		var report = FindGroupMutationPostJavaTraceArtifactFileReportService.Create();

		Assert.False(report.IsLive);
		Assert.True(report.HasActionTwoTarget);
		Assert.True(report.HasActionSixTarget);
		Assert.True(report.UsesStableTraceName);
		Assert.True(report.RequiresJavaInstrumentation);
		Assert.True(report.RequiresTraceSerializer);
		Assert.True(report.RequiresGeneratedArtifacts);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal("parity-artifacts/find-group/mutation-post/java", report.ArtifactRoot);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary-action-{action}-java.json", report.FileNamePattern);
		Assert.Equal([2, 6], report.Files.Select(file => file.Action));
		Assert.All(report.Files, file =>
			Assert.Equal(FindGroupMutationPostJavaTraceArtifactFileStatus.BlockedMissingGeneratedArtifact, file.Status));
	}

	[Fact]
	public void Create_UsesSchemaMappingsForExpectedArtifactRows()
	{
		var report = FindGroupMutationPostJavaTraceArtifactFileReportService.Create();

		Assert.Contains(report.Files, file =>
			file.Action == 2
			&& file.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
			&& file.ArtifactPath == "parity-artifacts/find-group/mutation-post/java/cm-find-group-direct-mutation-post-boundary-action-2-java.json"
			&& file.ExpectedTraceName == "cm-find-group-direct-mutation-post-boundary"
			&& file.ValidatorTarget == "FindGroupMutationPostJavaTraceArtifactValidatorService"
			&& file.Notes.Contains("postedSystemMessageId=1400392", StringComparison.Ordinal)
			&& file.Notes.Contains("refreshedListAction=0", StringComparison.Ordinal));
		Assert.Contains(report.Files, file =>
			file.Action == 6
			&& file.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
			&& file.ArtifactPath == "parity-artifacts/find-group/mutation-post/java/cm-find-group-direct-mutation-post-boundary-action-6-java.json"
			&& file.ExpectedTraceName == "cm-find-group-direct-mutation-post-boundary"
			&& file.ValidatorTarget == "FindGroupMutationPostJavaTraceArtifactValidatorService"
			&& file.Notes.Contains("postedSystemMessageId=1400393", StringComparison.Ordinal)
			&& file.Notes.Contains("refreshedListAction=4", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_AllowsCustomArtifactRootWithoutChangingFileNames()
	{
		var report = FindGroupMutationPostJavaTraceArtifactFileReportService.Create("custom-root/java");

		Assert.Equal("custom-root/java", report.ArtifactRoot);
		Assert.Equal(
			[
				"custom-root/java/cm-find-group-direct-mutation-post-boundary-action-2-java.json",
				"custom-root/java/cm-find-group-direct-mutation-post-boundary-action-6-java.json",
			],
			report.Files.Select(file => file.ArtifactPath));
	}

	[Fact]
	public void FileNameForAction_UsesStableMutationPostPattern()
	{
		Assert.Equal(
			"cm-find-group-direct-mutation-post-boundary-action-2-java.json",
			FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(2));
		Assert.Equal(
			"cm-find-group-direct-mutation-post-boundary-action-6-java.json",
			FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(6));
	}
}
