namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus
{
	BlockedResultEmissionBlockerNotReady,
	BlockedEvidenceSummaryNotReady,
	BlockedExecutorImplementationUnavailable,
}

public enum FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement
{
	ResultEmissionBlocker,
	EvidenceSummary,
	ImplementationReadinessAudit,
	RuntimeComparisonHandoff,
}

public enum FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus
{
	BlockedResultEmissionBlockerNotReady,
	BlockedEvidenceSummaryNotReady,
	BlockedOutputEmissionUnavailable,
	BlockedExecutableImplementationDisabled,
	BlockedRuntimeComparisonMissing,
}

public sealed record FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRow(
	int Order,
	FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement Requirement,
	FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus Status,
	bool HasResultEmissionBlockerReport,
	bool HasEvidenceSummary,
	bool BlocksExecutableImplementation,
	bool BlocksRuntimeComparison,
	bool BlocksVerifiedParity,
	string ResultEmissionBlockerStatus,
	string EvidenceSummaryStatus,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedValueExecutorEvidenceBridge(
	FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRow> Rows,
	bool HasResultEmissionBlockerReport,
	bool HasEvidenceSummary,
	bool HasAnyRuntimeEvidence,
	bool CanWriteExecutableExecutor,
	bool CanExecuteExecutor,
	bool CanEmitResults,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live bridge from CM_FIND_GROUP action 2/6
/// projected-value emission blockers into executor evidence summary readiness.
/// It records the final go/no-go blockers before implementation readiness or
/// runtime-comparison handoff may proceed.
/// </summary>
public static class FindGroupMutationPostProjectedValueExecutorEvidenceBridgeService
{
	public static FindGroupMutationPostProjectedValueExecutorEvidenceBridge Create(
		FindGroupMutationPostProjectedValueResultEmissionBlockerReport? resultEmissionBlocker = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract? evidenceSummary = null)
	{
		resultEmissionBlocker ??= FindGroupMutationPostProjectedValueResultEmissionBlockerReportService.Create();
		evidenceSummary ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractService.Create();

		var status = StatusFor(resultEmissionBlocker, evidenceSummary);
		var rows = new[]
		{
			ResultEmissionBlockerRow(1, status, resultEmissionBlocker, evidenceSummary),
			EvidenceSummaryRow(2, status, resultEmissionBlocker, evidenceSummary),
			ImplementationReadinessRow(3, status, resultEmissionBlocker, evidenceSummary),
			RuntimeComparisonHandoffRow(4, status, resultEmissionBlocker, evidenceSummary),
		};

		return new FindGroupMutationPostProjectedValueExecutorEvidenceBridge(
			status,
			rows,
			HasResultEmissionBlockerReport: resultEmissionBlocker.Rows.Count > 0,
			HasEvidenceSummary: evidenceSummary.Rows.Count > 0,
			HasAnyRuntimeEvidence: false,
			CanWriteExecutableExecutor: false,
			CanExecuteExecutor: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			resultEmissionBlocker.TraceName,
			resultEmissionBlocker.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus StatusFor(
		FindGroupMutationPostProjectedValueResultEmissionBlockerReport resultEmissionBlocker,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract evidenceSummary)
	{
		if (resultEmissionBlocker.Status != FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable)
			return FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedResultEmissionBlockerNotReady;

		if (evidenceSummary.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady)
			return FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedEvidenceSummaryNotReady;

		return FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedExecutorImplementationUnavailable;
	}

	private static FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRow ResultEmissionBlockerRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus bridgeStatus,
		FindGroupMutationPostProjectedValueResultEmissionBlockerReport resultEmissionBlocker,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract evidenceSummary) =>
		new(
			order,
			FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.ResultEmissionBlocker,
			bridgeStatus == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedResultEmissionBlockerNotReady
				? FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedResultEmissionBlockerNotReady
				: FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedOutputEmissionUnavailable,
			HasResultEmissionBlockerReport: resultEmissionBlocker.Rows.Count > 0,
			HasEvidenceSummary: evidenceSummary.Rows.Count > 0,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksVerifiedParity: true,
			resultEmissionBlocker.Status.ToString(),
			evidenceSummary.Status.ToString(),
			"Projected-value result-emission blocker must show every output kind is blocked by value projection, row decisions, context attachment, materialization, or runtime comparison before executor implementation can be audited.",
			$"outputKinds={resultEmissionBlocker.OutputKindCount}; emittableOutputs={resultEmissionBlocker.EmittableOutputCount}; canEmitAnyResult={resultEmissionBlocker.CanEmitAnyResult}; canRunRuntimeComparison={resultEmissionBlocker.CanRunRuntimeComparison}; canClaimVerifiedParity={resultEmissionBlocker.CanClaimVerifiedParity}",
			"Output blockers are metadata only; they do not emit result rows.");

	private static FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRow EvidenceSummaryRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus bridgeStatus,
		FindGroupMutationPostProjectedValueResultEmissionBlockerReport resultEmissionBlocker,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract evidenceSummary) =>
		new(
			order,
			FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.EvidenceSummary,
			bridgeStatus == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedEvidenceSummaryNotReady
				? FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedEvidenceSummaryNotReady
				: FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedExecutableImplementationDisabled,
			HasResultEmissionBlockerReport: resultEmissionBlocker.Rows.Count > 0,
			HasEvidenceSummary: evidenceSummary.Rows.Count > 0,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksVerifiedParity: true,
			resultEmissionBlocker.Status.ToString(),
			evidenceSummary.Status.ToString(),
			"Executor evidence summary must have blocked-output preview, runtime-evidence intake, materialization preflight, result-emission gate, and runtime-comparison rows before implementation readiness can proceed.",
			$"summaryRows={evidenceSummary.Rows.Count}; hasAnyRuntimeEvidence={evidenceSummary.HasAnyRuntimeEvidence}; canImplementExecutor={evidenceSummary.CanImplementExecutor}; canExecuteExecutor={evidenceSummary.CanExecuteExecutor}; canEmitResults={evidenceSummary.CanEmitResults}; canClaimVerifiedParity={evidenceSummary.CanClaimVerifiedParity}",
			"Evidence summary is still non-live and cannot authorize executable reader/comparator code.");

	private static FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRow ImplementationReadinessRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus bridgeStatus,
		FindGroupMutationPostProjectedValueResultEmissionBlockerReport resultEmissionBlocker,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract evidenceSummary) =>
		new(
			order,
			FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.ImplementationReadinessAudit,
			bridgeStatus == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedExecutorImplementationUnavailable
				? FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedExecutableImplementationDisabled
				: FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedEvidenceSummaryNotReady,
			HasResultEmissionBlockerReport: resultEmissionBlocker.Rows.Count > 0,
			HasEvidenceSummary: evidenceSummary.Rows.Count > 0,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksVerifiedParity: true,
			resultEmissionBlocker.Status.ToString(),
			evidenceSummary.Status.ToString(),
			"Implementation readiness audit must keep row identity pairing, typed reads, equality comparison, result selection, context attachment, and result emission non-executable until runtime evidence exists.",
			$"resultEmissionBlocked={resultEmissionBlocker.Rows.Count(row => !row.CanEmitResult)}; summaryBlocksImplementation={evidenceSummary.Rows.Count(row => row.BlocksExecutorImplementation)}; canWriteExecutableExecutor=False; canExecuteExecutor=False",
			"Executable executor work remains disallowed; this bridge only prepares the audit input story.");

	private static FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRow RuntimeComparisonHandoffRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus bridgeStatus,
		FindGroupMutationPostProjectedValueResultEmissionBlockerReport resultEmissionBlocker,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract evidenceSummary) =>
		new(
			order,
			FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.RuntimeComparisonHandoff,
			FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedRuntimeComparisonMissing,
			HasResultEmissionBlockerReport: resultEmissionBlocker.Rows.Count > 0,
			HasEvidenceSummary: evidenceSummary.Rows.Count > 0,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksVerifiedParity: true,
			resultEmissionBlocker.Status.ToString(),
			evidenceSummary.Status.ToString(),
			"Runtime-comparison handoff requires deterministic Java/C# runtime or socket evidence for action 2 and action 6 after accepted live C# rows, runtime-backed Java rows, projected values, materialized results, and emission blockers are resolved.",
			$"bridgeStatus={bridgeStatus}; resultRowsRequireRuntimeComparison={resultEmissionBlocker.Rows.Count(row => row.RequiresRuntimeComparison)}; summaryRuntimeRows={evidenceSummary.Rows.Count(row => row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeComparison)}; hasAnyRuntimeEvidence={evidenceSummary.HasAnyRuntimeEvidence}",
			"Runtime comparison remains absent, so verified parity and live dispatch remain blocked.");

	private static string DecisionFor(FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedResultEmissionBlockerNotReady => "Projected-value executor evidence bridge is blocked until result-emission blocker metadata reaches unavailable-output readiness.",
			FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedEvidenceSummaryNotReady => "Projected-value executor evidence bridge is blocked until executor evidence summary metadata is ready.",
			_ => "Projected-value executor evidence bridge is defined, but executable implementation, runtime comparison handoff, live dispatch, and verified parity remain blocked.",
		};
	}
}
