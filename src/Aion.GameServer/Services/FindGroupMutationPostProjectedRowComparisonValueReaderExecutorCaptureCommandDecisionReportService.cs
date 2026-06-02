namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus
{
	BlockedExecutorConsistencyAudit,
	BlockedJavaArtifactCaptureEvidence,
	BlockedCSharpBoundaryCaptureEvidence,
	BlockedRuntimeComparisonEvidence,
	BlockedExecutableImplementation,
	ReadyForRuntimeComparison,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind
{
	ExecutorConsistencyAudit,
	JavaArtifactCapture,
	CSharpBoundaryCapture,
	RuntimeComparison,
	NoCommand,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField Field,
	string EvidenceField,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind CommandKind,
	string Command,
	bool IsPrimaryDecision,
	bool ShouldRunCommand,
	bool BlocksRuntimeComparison,
	bool BlocksExecutableImplementation,
	bool BlocksVerifiedParity,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReport(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportRow> Rows,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField SelectedField,
	string SelectedEvidenceField,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind SelectedCommandKind,
	string SelectedCommand,
	bool IsJavaCaptureSelected,
	bool IsCSharpCaptureSelected,
	bool SourceSummaryAllowsRuntimeComparison,
	bool ShouldRunSelectedCommand,
	bool CanRunJavaCapture,
	bool CanRunCSharpCapture,
	bool CanRunRuntimeComparison,
	bool CanStartExecutableImplementation,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live command-decision report for future
/// CM_FIND_GROUP action 2/6 value-reader evidence collection. It consumes the
/// capture blocker summary and selects the next focused evidence command, but
/// never executes capture, comparison, or executable implementation.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReport Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummary? executionBlockerSummary = null)
	{
		executionBlockerSummary ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService.Create();

		var selectedKind = CommandKindFor(executionBlockerSummary.PrimaryBlockingField);
		var rows = executionBlockerSummary.Rows
			.Select(row => Row(row, executionBlockerSummary.PrimaryBlockingEvidenceField))
			.ToArray();
		var status = StatusFor(executionBlockerSummary.Status, executionBlockerSummary.PrimaryBlockingField);

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReport(
			status,
			rows,
			executionBlockerSummary.PrimaryBlockingField,
			executionBlockerSummary.PrimaryBlockingEvidenceField,
			selectedKind,
			executionBlockerSummary.SmallestNextEvidenceCommand,
			IsJavaCaptureSelected: selectedKind == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.JavaArtifactCapture,
			IsCSharpCaptureSelected: selectedKind == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.CSharpBoundaryCapture,
			SourceSummaryAllowsRuntimeComparison: executionBlockerSummary.ShouldRunRuntimeComparison,
			ShouldRunSelectedCommand: false,
			CanRunJavaCapture: false,
			CanRunCSharpCapture: false,
			CanRunRuntimeComparison: false,
			CanStartExecutableImplementation: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status, executionBlockerSummary.PrimaryBlockingEvidenceField, selectedKind, executionBlockerSummary.SmallestNextEvidenceCommand),
			executionBlockerSummary.TraceName,
			executionBlockerSummary.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportRow Row(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryRow row,
		string selectedEvidenceField)
	{
		var commandKind = CommandKindFor(row.Field);
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportRow(
			row.Order,
			row.Field,
			row.EvidenceField,
			commandKind,
			row.NextEvidenceCommand,
			string.Equals(row.EvidenceField, selectedEvidenceField, StringComparison.Ordinal),
			ShouldRunCommand: false,
			row.BlocksRuntimeComparison,
			row.BlocksExecutableImplementation,
			row.BlocksVerifiedParity,
			row.RequiredEvidence,
			row.CurrentEvidence,
			NotesFor(row, commandKind));
	}

	private static string NotesFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryRow row,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind commandKind)
	{
		return row.Field switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows
				or FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactShapeValidation =>
				"Java capture is only selectable after executor consistency evidence is accepted; this report still does not run Maven.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.BoundaryExecutorObservation =>
				"Boundary executor observation proves the guarded CM_FIND_GROUP boundary invoked the side-effect executor after packet acceptance; it does not prove posted/refreshed registry send ordering.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RegistrySendObservation =>
				"Registry send observation proves the posted system message and refreshed SM_FIND_GROUP list were observed through direct registry sends in Java order; it does not prove the boundary invoked the executor.",
			_ => commandKind == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.CSharpBoundaryCapture
				? row.Notes + " This C# capture decision remains distinct from executor-observation and registry-send observation gates."
				: row.Notes,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind CommandKindFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField field)
	{
		return field switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutorConsistencyAudit => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.ExecutorConsistencyAudit,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.JavaArtifactCapture,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactShapeValidation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.JavaArtifactCapture,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.CSharpBoundaryRows => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.CSharpBoundaryCapture,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.BoundaryExecutorObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.CSharpBoundaryCapture,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RegistrySendObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.CSharpBoundaryCapture,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RuntimeComparisonExecution => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.RuntimeComparison,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutableImplementationGate => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.NoCommand,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.NoCommand,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus summaryStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField selectedField)
	{
		if (summaryStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.ReadyForRuntimeComparison)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.ReadyForRuntimeComparison;

		return CommandKindFor(selectedField) switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.ExecutorConsistencyAudit => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedExecutorConsistencyAudit,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.JavaArtifactCapture => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedJavaArtifactCaptureEvidence,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.CSharpBoundaryCapture => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedCSharpBoundaryCaptureEvidence,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.RuntimeComparison => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedRuntimeComparisonEvidence,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedExecutableImplementation,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus status,
		string selectedEvidenceField,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind selectedKind,
		string selectedCommand)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedExecutorConsistencyAudit => $"Next focused evidence command is the executor consistency audit for {selectedEvidenceField}: {selectedCommand}. Do not run Java capture until executorConsistencyAuditAccepted is satisfied.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedJavaArtifactCaptureEvidence => $"Executor consistency is no longer the selected blocker; Java artifact capture is the next focused evidence command for {selectedEvidenceField}: {selectedCommand}. This report is non-live and does not run Maven.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.BlockedCSharpBoundaryCaptureEvidence => $"C# guarded boundary capture is the next focused evidence command for {selectedEvidenceField}: {selectedCommand}. This report is non-live and does not run C# capture.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportStatus.ReadyForRuntimeComparison => $"Source summary says runtime comparison may be ready, but this command-decision report only records metadata for {selectedKind} and does not execute comparison.",
			_ => $"Next focused evidence command remains metadata-only for {selectedEvidenceField}: {selectedCommand}. Do not execute capture or claim parity from this report.",
		};
	}
}
