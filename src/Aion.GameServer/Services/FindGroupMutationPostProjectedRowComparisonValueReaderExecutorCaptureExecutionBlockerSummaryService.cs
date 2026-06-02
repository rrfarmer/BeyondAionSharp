namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus
{
	BlockedLiveCapturePreflightNotReady,
	BlockedAcceptanceEvidenceMissing,
	BlockedRuntimeComparisonExecution,
	ReadyForRuntimeComparison,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason
{
	LiveCapturePreflightNotReady,
	MissingJavaArtifactEvidence,
	MissingCSharpBoundaryEvidence,
	MissingExecutorObservation,
	MissingRegistryObservation,
	MissingValueProjection,
	MissingMaterializedResults,
	MissingResultEmission,
	MissingRuntimeComparison,
	ExecutableImplementationDeferred,
	ReadyNoBlocker,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField Field,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep SourceStep,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus MatrixStatus,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason Reason,
	bool BlocksRuntimeComparison,
	bool BlocksExecutableImplementation,
	bool BlocksVerifiedParity,
	string EvidenceField,
	string NextEvidenceCommand,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummary(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryRow> Rows,
	int BlockingRuntimeComparisonCount,
	int BlockingExecutableImplementationCount,
	int BlockingVerifiedParityCount,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField PrimaryBlockingField,
	string PrimaryBlockingEvidenceField,
	string SmallestNextEvidenceCommand,
	bool ShouldRunRuntimeComparison,
	bool CanStartExecutableImplementation,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live go/no-go summary for future CM_FIND_GROUP
/// action 2/6 value-reader runtime comparison execution. It consumes the
/// capture acceptance matrix and names the smallest next evidence-producing
/// command, but it does not execute Java capture, C# capture, or comparison.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummary Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContract? acceptanceMatrix = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract? liveCapturePreflight = null)
	{
		liveCapturePreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.Create();
		acceptanceMatrix ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService.Create(liveCapturePreflight);

		var rows = acceptanceMatrix.Rows
			.Select(row => CreateRow(row, liveCapturePreflight))
			.ToArray();
		var status = StatusFor(acceptanceMatrix);
		var primaryBlocker = rows.FirstOrDefault(row => row.BlocksRuntimeComparison)
			?? rows.First(row => row.BlocksVerifiedParity);

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummary(
			status,
			rows,
			BlockingRuntimeComparisonCount: rows.Count(row => row.BlocksRuntimeComparison),
			BlockingExecutableImplementationCount: rows.Count(row => row.BlocksExecutableImplementation),
			BlockingVerifiedParityCount: rows.Count(row => row.BlocksVerifiedParity),
			primaryBlocker.Field,
			primaryBlocker.EvidenceField,
			primaryBlocker.NextEvidenceCommand,
			ShouldRunRuntimeComparison: acceptanceMatrix.CanRunRuntimeComparison,
			CanStartExecutableImplementation: acceptanceMatrix.CanStartExecutableImplementation,
			CanClaimVerifiedParity: acceptanceMatrix.CanClaimVerifiedParity,
			DecisionFor(status, primaryBlocker),
			acceptanceMatrix.TraceName,
			acceptanceMatrix.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryRow CreateRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixRow matrixRow,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract liveCapturePreflight)
	{
		var runbookRow = liveCapturePreflight.Rows.FirstOrDefault(row => row.Step == matrixRow.SourceStep);
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryRow(
			matrixRow.Order,
			matrixRow.Field,
			matrixRow.SourceStep,
			matrixRow.Status,
			ReasonFor(matrixRow.Status),
			matrixRow.BlocksRuntimeComparison,
			matrixRow.BlocksExecutableImplementation,
			matrixRow.BlocksVerifiedParity,
			matrixRow.EvidenceField,
			runbookRow?.Command ?? "No focused evidence command is available for this acceptance field.",
			matrixRow.RequiredEvidence,
			matrixRow.CurrentEvidence,
			matrixRow.BlocksRuntimeComparison
				? matrixRow.RuntimeComparisonBlocker
				: matrixRow.RuntimeComparisonBlocker + " This row does not block runtime comparison start, but it still blocks executable implementation and verified parity.");
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContract acceptanceMatrix)
	{
		if (acceptanceMatrix.CanRunRuntimeComparison)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.ReadyForRuntimeComparison;

		return acceptanceMatrix.Status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedLiveCapturePreflightNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.BlockedLiveCapturePreflightNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedAcceptanceEvidenceMissing => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.BlockedAcceptanceEvidenceMissing,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.BlockedRuntimeComparisonExecution,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason ReasonFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus matrixStatus)
	{
		return matrixStatus switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.BlockedLiveCapturePreflightNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.LiveCapturePreflightNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingJavaArtifactEvidence => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingJavaArtifactEvidence,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingCSharpBoundaryEvidence => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingCSharpBoundaryEvidence,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingExecutorObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingExecutorObservation,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingRegistryObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingRegistryObservation,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingValueProjection => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingValueProjection,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingMaterializedResults => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingMaterializedResults,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingResultEmission => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingResultEmission,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingRuntimeComparison => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingRuntimeComparison,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.ExecutableImplementationDeferred => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.ExecutableImplementationDeferred,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.ReadyNoBlocker,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryRow primaryBlocker)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.ReadyForRuntimeComparison => "Value-reader runtime comparison may start, but this summary did not execute comparison or prove verified parity.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.BlockedLiveCapturePreflightNotReady => $"Value-reader runtime comparison must not start; first blocker is {primaryBlocker.EvidenceField}, and the smallest next evidence command is: {primaryBlocker.NextEvidenceCommand}",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.BlockedAcceptanceEvidenceMissing => $"Value-reader runtime comparison must not start; first runtime blocker is {primaryBlocker.EvidenceField}, and the smallest next evidence command is: {primaryBlocker.NextEvidenceCommand}",
			_ => $"Value-reader runtime comparison remains blocked by {primaryBlocker.EvidenceField}; executable implementation also remains deferred until every evidence field passes.",
		};
	}
}
