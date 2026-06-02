using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportServiceTests
{
	[Fact]
	public void Create_DefaultReportSelectsConsistencyAuditBeforeJavaCapture()
	{
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedExecutorConsistencyAudit, report.Status);
		Assert.False(report.IsLive);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutorConsistencyAudit, report.SelectedField);
		Assert.Equal("executorConsistencyAuditAccepted", report.SelectedEvidenceField);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.ExecutorConsistencyAudit, report.SelectedCommandKind);
		Assert.Contains("FindGroupMutationPostProjectedValueExecutorConsistencyAuditServiceTests", report.SelectedCommand, StringComparison.Ordinal);
		Assert.False(report.IsJavaCaptureSelected);
		Assert.False(report.IsCSharpCaptureSelected);
		Assert.False(report.ShouldRunSelectedCommand);
		Assert.False(report.CanRunJavaCapture);
		Assert.False(report.CanRunCSharpCapture);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanStartExecutableImplementation);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Contains("Do not run Java capture", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains("executorConsistencyAuditAccepted", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(report.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutorConsistencyAudit
			&& row.IsPrimaryDecision
			&& row.CommandKind == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.ExecutorConsistencyAudit
			&& row.CurrentEvidence.Contains("captureExecutionBlockerSummaryRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("captureAcceptanceMatrixRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("liveCapturePreflightRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("runtimeComparisonHandoffRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("consistencyAuditRowEvidence=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("ExecutorEvidenceBridge", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("ResultEmissionBlocker", StringComparison.Ordinal)
			&& !row.ShouldRunCommand);
		Assert.Contains(report.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows
			&& !row.IsPrimaryDecision
			&& row.CommandKind == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.JavaArtifactCapture
			&& row.Notes.Contains("only selectable after executor consistency", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.BoundaryExecutorObservation
			&& row.CommandKind == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.CSharpBoundaryCapture
			&& row.Notes.Contains("invoked the side-effect executor after packet acceptance", StringComparison.Ordinal)
			&& row.Notes.Contains("does not prove posted/refreshed registry send ordering", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RegistrySendObservation
			&& row.CommandKind == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.CSharpBoundaryCapture
			&& row.Notes.Contains("direct registry sends in Java order", StringComparison.Ordinal)
			&& row.Notes.Contains("does not prove the boundary invoked the executor", StringComparison.Ordinal));
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Contains("addRecruitment/addApplication", report.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_JavaArtifactRowsPrimarySelectsJavaCaptureButStillDoesNotRunIt()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService.Create() with
		{
			Status = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.BlockedAcceptanceEvidenceMissing,
			PrimaryBlockingField = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows,
			PrimaryBlockingEvidenceField = "javaArtifactRowsPresent",
			SmallestNextEvidenceCommand = "mvn -pl game-server -am test \"-Dtest=FindGroupMutationPostTraceCaptureTest\"",
			ShouldRunRuntimeComparison = false,
		};

		var report = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportService.Create(summary);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedJavaArtifactCaptureEvidence, report.Status);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows, report.SelectedField);
		Assert.Equal("javaArtifactRowsPresent", report.SelectedEvidenceField);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.JavaArtifactCapture, report.SelectedCommandKind);
		Assert.True(report.IsJavaCaptureSelected);
		Assert.False(report.ShouldRunSelectedCommand);
		Assert.False(report.CanRunJavaCapture);
		Assert.Contains("Java artifact capture is the next focused evidence command", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains("does not run Maven", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_CSharpBoundaryRowsPrimarySelectsGuardedBoundaryCaptureButStillDoesNotRunIt()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService.Create() with
		{
			Status = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.BlockedAcceptanceEvidenceMissing,
			PrimaryBlockingField = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.CSharpBoundaryRows,
			PrimaryBlockingEvidenceField = "csharpBoundaryRowsAccepted",
			SmallestNextEvidenceCommand = "dotnet test dotnetConversion\\tests\\Aion.GameServer.Tests\\Aion.GameServer.Tests.csproj --filter \"FullyQualifiedName~GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture\" --no-restore",
		};

		var report = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportService.Create(summary);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedCSharpBoundaryCaptureEvidence, report.Status);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.CSharpBoundaryRows, report.SelectedField);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.CSharpBoundaryCapture, report.SelectedCommandKind);
		Assert.True(report.IsCSharpCaptureSelected);
		Assert.False(report.ShouldRunSelectedCommand);
		Assert.False(report.CanRunCSharpCapture);
		Assert.Contains("C# guarded boundary capture is the next focused evidence command", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains("does not run C# capture", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ReadyRuntimeComparisonSummaryStillDoesNotExecuteComparison()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService.Create() with
		{
			Status = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.ReadyForRuntimeComparison,
			PrimaryBlockingField = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RuntimeComparisonExecution,
			PrimaryBlockingEvidenceField = "runtimeComparisonExecuted",
			SmallestNextEvidenceCommand = "future focused Java/C# runtime comparison command",
			ShouldRunRuntimeComparison = true,
		};

		var report = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportService.Create(summary);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.ReadyForRuntimeComparison, report.Status);
		Assert.True(report.SourceSummaryAllowsRuntimeComparison);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.RuntimeComparison, report.SelectedCommandKind);
		Assert.False(report.ShouldRunSelectedCommand);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Contains("does not execute comparison", report.ExecutionDecision, StringComparison.Ordinal);
	}
}
