using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportServiceTests
{
	[Fact]
	public void Create_DefaultReportVerifiesEveryCaptureCommandProvider()
	{
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportStatus.ConsistentRuntimeEvidenceMissing, report.Status);
		Assert.False(report.IsLive);
		Assert.True(report.AllProvidersConsistent);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Equal("aion.findGroupMutationPost.serverEpochSeconds", report.TimestampProperty);
		Assert.Equal(1700000000, report.DeterministicServerEpochSeconds);
		Assert.Equal("-Daion.findGroupMutationPost.serverEpochSeconds=1700000000", report.ExpectedTimestampCommandFragment);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.ExecutorConsistencyAudit, report.CommandDecisionSelectedKind);
		Assert.Equal("executorConsistencyAuditAccepted", report.CommandDecisionSelectedEvidenceField);
		Assert.Contains("captureExecutionBlockerSummaryRows=", report.CommandDecisionRowsEvidence, StringComparison.Ordinal);
		Assert.Contains("captureAcceptanceMatrixRows=", report.CommandDecisionRowsEvidence, StringComparison.Ordinal);
		Assert.Contains("liveCapturePreflightRows=", report.CommandDecisionRowsEvidence, StringComparison.Ordinal);
		Assert.Contains("runtimeComparisonHandoffRows=", report.CommandDecisionRowsEvidence, StringComparison.Ordinal);
		Assert.Contains("consistencyAuditRowEvidence=", report.CommandDecisionRowsEvidence, StringComparison.Ordinal);
		Assert.Contains("ExecutorEvidenceBridge", report.CommandDecisionRowsEvidence, StringComparison.Ordinal);
		Assert.Contains("ResultEmissionBlocker", report.CommandDecisionRowsEvidence, StringComparison.Ordinal);
		Assert.True(report.CommandDecisionDefersJavaCaptureBeforeConsistency);
		Assert.Equal(Enum.GetValues<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider>(), report.Rows.Select(row => row.Provider));
		Assert.All(report.Rows, row =>
		{
			Assert.True(row.HasCaptureFlag);
			Assert.True(row.HasTimestampProperty);
			Assert.True(row.HasDeterministicTimestampValue);
			Assert.True(row.HasExpectedArtifactRoot);
			Assert.True(row.IsConsistent);
		});
		Assert.Contains(report.Rows, row =>
			row.Provider == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider.JavaArtifactCaptureRunbook
			&& !row.RequiresArtifactRoot);
		Assert.Contains(report.Rows, row =>
			row.Provider == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider.LiveCapturePreflightRunbook
			&& row.RequiresArtifactRoot);
		Assert.Contains(report.Rows, row =>
			row.Provider == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider.JavaArtifactRootValidationCommandReport
			&& row.RequiresArtifactRoot);
		Assert.Contains("runtime evidence", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains("comparison remain missing", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_InconsistentRunbookCommandBlocksBeforeCapture()
	{
		var runbook = FindGroupMutationPostJavaArtifactCaptureRunbookService.Create() with
		{
			FocusedMavenCommand = "mvn -pl game-server -am test \"-Dtest=FindGroupMutationPostTraceCaptureTest\" \"-Daion.findGroupMutationPost.capture=true\"",
		};

		var report = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportService.Create(
			javaCaptureRunbook: runbook);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportStatus.InconsistentCommandProviders, report.Status);
		Assert.False(report.AllProvidersConsistent);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Contains(report.Rows, row =>
			row.Provider == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider.JavaArtifactCaptureRunbook
			&& row.HasCaptureFlag
			&& !row.HasTimestampProperty
			&& !row.HasDeterministicTimestampValue
			&& !row.IsConsistent);
		Assert.Contains("do not agree", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_CustomArtifactRootMustAppearInRootAwareProviders()
	{
		var artifactRoot = Path.Combine(Path.GetTempPath(), $"find-group-capture-root-{Guid.NewGuid():N}");
		var runbook = FindGroupMutationPostJavaArtifactCaptureRunbookService.Create() with
		{
			ArtifactRoot = artifactRoot,
		};

		var report = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportService.Create(
			artifactRoot,
			javaCaptureRunbook: runbook);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportStatus.ConsistentRuntimeEvidenceMissing, report.Status);
		Assert.Equal(artifactRoot, report.ArtifactRoot);
		Assert.True(report.CommandDecisionDefersJavaCaptureBeforeConsistency);
		Assert.Contains(report.Rows, row =>
			row.Provider == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider.JavaArtifactCaptureRunbook
			&& !row.RequiresArtifactRoot
			&& row.IsConsistent);
		Assert.All(report.Rows.Where(row => row.RequiresArtifactRoot), row =>
		{
			Assert.True(row.HasExpectedArtifactRoot);
			Assert.Contains($"-Daion.findGroupMutationPost.artifactRoot={artifactRoot}", row.Command, StringComparison.Ordinal);
		});
	}
}
