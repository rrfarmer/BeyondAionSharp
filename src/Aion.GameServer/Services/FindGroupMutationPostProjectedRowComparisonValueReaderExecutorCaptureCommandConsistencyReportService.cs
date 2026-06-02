namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportStatus
{
	ConsistentRuntimeEvidenceMissing,
	InconsistentCommandProviders,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider
{
	JavaArtifactCaptureRunbook,
	LiveCapturePreflightRunbook,
	JavaArtifactRootValidationCommandReport,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider Provider,
	string Command,
	bool RequiresArtifactRoot,
	bool HasCaptureFlag,
	bool HasTimestampProperty,
	bool HasDeterministicTimestampValue,
	bool HasExpectedArtifactRoot,
	bool IsConsistent,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReport(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportRow> Rows,
	string ArtifactRoot,
	string TimestampProperty,
	int DeterministicServerEpochSeconds,
	string ExpectedTimestampCommandFragment,
	bool AllProvidersConsistent,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind CommandDecisionSelectedKind,
	string CommandDecisionSelectedEvidenceField,
	bool CommandDecisionDefersJavaCaptureBeforeConsistency,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live consistency report for the focused Java
/// capture commands used by the value-reader runtime-comparison runbook. It
/// verifies deterministic timestamp and artifact-root command fragments only;
/// it does not execute Java capture or compare runtime rows.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReport Create(
		string artifactRoot = FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot,
		FindGroupMutationPostJavaArtifactCaptureRunbook? javaCaptureRunbook = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract? liveCapturePreflight = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReport? commandDecisionReport = null,
		FindGroupMutationPostJavaArtifactRootValidationCommandReport? artifactRootValidationReport = null)
	{
		javaCaptureRunbook ??= FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();
		if (!string.Equals(javaCaptureRunbook.ArtifactRoot, artifactRoot, StringComparison.Ordinal))
		{
			javaCaptureRunbook = javaCaptureRunbook with { ArtifactRoot = artifactRoot };
		}

		liveCapturePreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.Create(
			javaCaptureRunbook: javaCaptureRunbook);
		commandDecisionReport ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionReportService.Create(
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService.Create(
				liveCapturePreflight: liveCapturePreflight));
		artifactRootValidationReport ??= FindGroupMutationPostJavaArtifactRootValidationCommandReportService.Create(artifactRoot);

		var timestampProperty = FindGroupMutationPostJavaArtifactCaptureRunbookService.ServerEpochSecondsProperty;
		var deterministicTimestamp = FindGroupMutationPostJavaArtifactCaptureRunbookService.DeterministicServerEpochSeconds;
		var expectedTimestampCommandFragment = $"-D{timestampProperty}={deterministicTimestamp}";
		var rows = new[]
		{
			Row(
				1,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider.JavaArtifactCaptureRunbook,
				javaCaptureRunbook.FocusedMavenCommand,
				requiresArtifactRoot: false,
				artifactRoot,
				timestampProperty,
				expectedTimestampCommandFragment),
			Row(
				2,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider.LiveCapturePreflightRunbook,
				liveCapturePreflight.JavaCaptureCommand,
				requiresArtifactRoot: true,
				artifactRoot,
				timestampProperty,
				expectedTimestampCommandFragment),
			Row(
				3,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider.JavaArtifactRootValidationCommandReport,
				artifactRootValidationReport.JavaCaptureCommand,
				requiresArtifactRoot: true,
				artifactRoot,
				timestampProperty,
				expectedTimestampCommandFragment),
		};
		var allProvidersConsistent = rows.All(row => row.IsConsistent);
		var status = allProvidersConsistent
			? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportStatus.ConsistentRuntimeEvidenceMissing
			: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportStatus.InconsistentCommandProviders;

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReport(
			status,
			rows,
			artifactRoot,
			timestampProperty,
			deterministicTimestamp,
			expectedTimestampCommandFragment,
			allProvidersConsistent,
			commandDecisionReport.SelectedCommandKind,
			commandDecisionReport.SelectedEvidenceField,
			commandDecisionReport.SelectedCommandKind == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandDecisionKind.ExecutorConsistencyAudit
				&& string.Equals(commandDecisionReport.SelectedEvidenceField, "executorConsistencyAuditAccepted", StringComparison.Ordinal),
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			liveCapturePreflight.TraceName,
			liveCapturePreflight.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportRow Row(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandProvider provider,
		string command,
		bool requiresArtifactRoot,
		string artifactRoot,
		string timestampProperty,
		string expectedTimestampCommandFragment)
	{
		var hasCaptureFlag = command.Contains(
			$"-D{FindGroupMutationPostJavaArtifactCaptureRunbookService.CaptureFlag}=true",
			StringComparison.Ordinal);
		var hasTimestampProperty = command.Contains(timestampProperty, StringComparison.Ordinal);
		var hasDeterministicTimestampValue = command.Contains(expectedTimestampCommandFragment, StringComparison.Ordinal);
		var hasExpectedArtifactRoot = !requiresArtifactRoot || command.Contains(
			$"-D{FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.JavaArtifactRootProperty}={artifactRoot}",
			StringComparison.Ordinal);
		var isConsistent = hasCaptureFlag
			&& hasTimestampProperty
			&& hasDeterministicTimestampValue
			&& hasExpectedArtifactRoot;

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportRow(
			order,
			provider,
			command,
			requiresArtifactRoot,
			hasCaptureFlag,
			hasTimestampProperty,
			hasDeterministicTimestampValue,
			hasExpectedArtifactRoot,
			isConsistent,
			isConsistent
				? "Command carries the capture flag and deterministic timestamp expected by the current non-live runbook."
				: "Command is missing at least one required capture, timestamp, or artifact-root fragment.");
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportStatus.ConsistentRuntimeEvidenceMissing => "Focused Java capture command providers agree on the deterministic timestamp command fragment, but runtime evidence and Java/C# comparison remain missing.",
			_ => "Focused Java capture command providers do not agree on the deterministic timestamp or artifact-root fragments; do not run capture or comparison until the commands are reconciled.",
		};
	}
}
