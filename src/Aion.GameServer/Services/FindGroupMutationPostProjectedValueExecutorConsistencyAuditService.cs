namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus
{
	BlockedMaterializationBlockerNotReady,
	BlockedResultEmissionBlockerNotReady,
	BlockedExecutorEvidenceBridgeNotReady,
	ConsistentBlockedReadiness,
}

public enum FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement
{
	MaterializationBlocker,
	ResultEmissionGate,
	ResultEmissionBlocker,
	EvidenceSummary,
	ExecutorEvidenceBridge,
	RuntimeComparisonAndLiveDispatch,
}

public enum FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus
{
	BlockedMaterializationUnavailable,
	BlockedEmissionUnavailable,
	BlockedEvidenceSummaryUnavailable,
	BlockedExecutableImplementationDisabled,
	BlockedRuntimeComparisonMissing,
	ConsistentBlocked,
}

public sealed record FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow(
	int Order,
	FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement Requirement,
	FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus Status,
	bool HasProvider,
	bool BlocksMaterialization,
	bool BlocksResultEmission,
	bool BlocksExecutableImplementation,
	bool BlocksRuntimeComparison,
	bool BlocksLiveDispatch,
	bool BlocksVerifiedParity,
	string ProviderStatus,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedValueExecutorConsistencyAudit(
	FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow> Rows,
	bool HasMaterializationBlockerReport,
	bool HasResultEmissionGate,
	bool HasResultEmissionBlockerReport,
	bool HasEvidenceSummary,
	bool HasExecutorEvidenceBridge,
	bool CanMaterializeOutputs,
	bool CanEmitResults,
	bool CanWriteExecutableExecutor,
	bool CanRunRuntimeComparison,
	bool CanEnableLiveDispatch,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live consistency audit for CM_FIND_GROUP action
/// 2/6 projected-value executor readiness. It cross-checks materialization,
/// emission, evidence-summary, and bridge blockers before any implementation or
/// runtime-comparison handoff can treat metadata as executable readiness.
/// </summary>
public static class FindGroupMutationPostProjectedValueExecutorConsistencyAuditService
{
	public static FindGroupMutationPostProjectedValueExecutorConsistencyAudit Create(
		FindGroupMutationPostProjectedValueMaterializationBlockerReport? materializationBlockers = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract? resultEmissionGate = null,
		FindGroupMutationPostProjectedValueResultEmissionBlockerReport? resultEmissionBlocker = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract? evidenceSummary = null,
		FindGroupMutationPostProjectedValueExecutorEvidenceBridge? executorEvidenceBridge = null)
	{
		materializationBlockers ??= FindGroupMutationPostProjectedValueMaterializationBlockerReportService.Create();
		resultEmissionGate ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService.Create();
		resultEmissionBlocker ??= FindGroupMutationPostProjectedValueResultEmissionBlockerReportService.Create(materializationBlockers, resultEmissionGate);
		evidenceSummary ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractService.Create(resultEmissionGate: resultEmissionGate);
		executorEvidenceBridge ??= FindGroupMutationPostProjectedValueExecutorEvidenceBridgeService.Create(resultEmissionBlocker, evidenceSummary);

		var status = StatusFor(materializationBlockers, resultEmissionGate, resultEmissionBlocker, evidenceSummary, executorEvidenceBridge);
		var rows = new[]
		{
			MaterializationRow(1, status, materializationBlockers),
			ResultEmissionGateRow(2, status, resultEmissionGate),
			ResultEmissionBlockerRow(3, status, resultEmissionBlocker),
			EvidenceSummaryRow(4, status, evidenceSummary),
			ExecutorEvidenceBridgeRow(5, status, executorEvidenceBridge),
			RuntimeComparisonAndLiveDispatchRow(6, status, executorEvidenceBridge),
		};

		return new FindGroupMutationPostProjectedValueExecutorConsistencyAudit(
			status,
			rows,
			HasMaterializationBlockerReport: materializationBlockers.Rows.Count > 0,
			HasResultEmissionGate: resultEmissionGate.Rows.Count > 0,
			HasResultEmissionBlockerReport: resultEmissionBlocker.Rows.Count > 0,
			HasEvidenceSummary: evidenceSummary.Rows.Count > 0,
			HasExecutorEvidenceBridge: executorEvidenceBridge.Rows.Count > 0,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanWriteExecutableExecutor: false,
			CanRunRuntimeComparison: false,
			CanEnableLiveDispatch: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			materializationBlockers.TraceName,
			materializationBlockers.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus StatusFor(
		FindGroupMutationPostProjectedValueMaterializationBlockerReport materializationBlockers,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract resultEmissionGate,
		FindGroupMutationPostProjectedValueResultEmissionBlockerReport resultEmissionBlocker,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract evidenceSummary,
		FindGroupMutationPostProjectedValueExecutorEvidenceBridge executorEvidenceBridge)
	{
		if (materializationBlockers.Status != FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread)
			return FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedMaterializationBlockerNotReady;

		if (resultEmissionGate.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady
			|| resultEmissionBlocker.Status != FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable)
			return FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedResultEmissionBlockerNotReady;

		if (evidenceSummary.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady
			|| executorEvidenceBridge.Status != FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedExecutorImplementationUnavailable)
			return FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedExecutorEvidenceBridgeNotReady;

		return FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.ConsistentBlockedReadiness;
	}

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow MaterializationRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus auditStatus,
		FindGroupMutationPostProjectedValueMaterializationBlockerReport materializationBlockers) =>
		new(
			order,
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.MaterializationBlocker,
			auditStatus == FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedMaterializationBlockerNotReady
				? FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedMaterializationUnavailable
				: FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.ConsistentBlocked,
			HasProvider: materializationBlockers.Rows.Count > 0,
			BlocksMaterialization: true,
			BlocksResultEmission: true,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksLiveDispatch: true,
			BlocksVerifiedParity: true,
			materializationBlockers.Status.ToString(),
			"Materialization blocker must enumerate all projected-value output kinds and keep every output non-materializable until real Java/C# values, row decisions, and context attachment exist.",
			$"outputKinds={materializationBlockers.OutputKindCount}; unreadEqualityFields={materializationBlockers.UnreadEqualityFieldCount}; ignoredRuntimeContextFields={materializationBlockers.IgnoredRuntimeContextFieldCount}; canEmitAnyResult={materializationBlockers.CanEmitAnyResult}; canClaimVerifiedParity={materializationBlockers.CanClaimVerifiedParity}",
			"Projected-value placeholders cannot authorize materialized comparison outputs.");

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow ResultEmissionGateRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus auditStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract resultEmissionGate) =>
		new(
			order,
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ResultEmissionGate,
			auditStatus == FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedResultEmissionBlockerNotReady
				? FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedEmissionUnavailable
				: FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.ConsistentBlocked,
			HasProvider: resultEmissionGate.Rows.Count > 0,
			BlocksMaterialization: false,
			BlocksResultEmission: true,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksLiveDispatch: true,
			BlocksVerifiedParity: true,
			resultEmissionGate.Status.ToString(),
			"Result-emission gate must require materialized output, runtime comparison evidence, and output-specific conditions before any result can emit.",
			$"outputKinds={resultEmissionGate.OutputKindCount}; emittableOutputs={resultEmissionGate.EmittableOutputCount}; hasAnyRuntimeEvidence={resultEmissionGate.HasAnyRuntimeEvidence}; canEmitAnyResult={resultEmissionGate.CanEmitAnyResult}; canClaimVerifiedParity={resultEmissionGate.CanClaimVerifiedParity}",
			"Emission remains metadata only and cannot substitute for runtime comparison.");

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow ResultEmissionBlockerRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus auditStatus,
		FindGroupMutationPostProjectedValueResultEmissionBlockerReport resultEmissionBlocker) =>
		new(
			order,
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ResultEmissionBlocker,
			auditStatus == FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedResultEmissionBlockerNotReady
				? FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedEmissionUnavailable
				: FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.ConsistentBlocked,
			HasProvider: resultEmissionBlocker.Rows.Count > 0,
			BlocksMaterialization: true,
			BlocksResultEmission: true,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksLiveDispatch: true,
			BlocksVerifiedParity: true,
			resultEmissionBlocker.Status.ToString(),
			"Result-emission blocker must agree that every output kind is non-emittable before executor bridge metadata can proceed.",
			$"outputKinds={resultEmissionBlocker.OutputKindCount}; emittableOutputs={resultEmissionBlocker.EmittableOutputCount}; canEmitAnyResult={resultEmissionBlocker.CanEmitAnyResult}; canRunRuntimeComparison={resultEmissionBlocker.CanRunRuntimeComparison}; canClaimVerifiedParity={resultEmissionBlocker.CanClaimVerifiedParity}",
			"Blocked emission is a prerequisite for safe implementation-readiness reporting, not evidence of executable parity.");

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow EvidenceSummaryRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus auditStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract evidenceSummary) =>
		new(
			order,
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.EvidenceSummary,
			auditStatus == FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedExecutorEvidenceBridgeNotReady
				? FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedEvidenceSummaryUnavailable
				: FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.ConsistentBlocked,
			HasProvider: evidenceSummary.Rows.Count > 0,
			BlocksMaterialization: true,
			BlocksResultEmission: true,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksLiveDispatch: true,
			BlocksVerifiedParity: true,
			evidenceSummary.Status.ToString(),
			"Evidence summary must include blocked-output preview, runtime-evidence intake, materialization preflight, result-emission gate, and runtime-comparison blockers.",
			$"rows={evidenceSummary.Rows.Count}; hasAnyRuntimeEvidence={evidenceSummary.HasAnyRuntimeEvidence}; canImplementExecutor={evidenceSummary.CanImplementExecutor}; canExecuteExecutor={evidenceSummary.CanExecuteExecutor}; canEmitResults={evidenceSummary.CanEmitResults}; canClaimVerifiedParity={evidenceSummary.CanClaimVerifiedParity}",
			"Evidence summary rows remain non-live and block executable reader/comparator work.");

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow ExecutorEvidenceBridgeRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus auditStatus,
		FindGroupMutationPostProjectedValueExecutorEvidenceBridge executorEvidenceBridge)
	{
		var executorEvidenceBridgeRows = BridgeRowEvidence(executorEvidenceBridge);
		return new(
			order,
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ExecutorEvidenceBridge,
			auditStatus == FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedExecutorEvidenceBridgeNotReady
				? FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedExecutableImplementationDisabled
				: FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.ConsistentBlocked,
			HasProvider: executorEvidenceBridge.Rows.Count > 0,
			BlocksMaterialization: true,
			BlocksResultEmission: true,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksLiveDispatch: true,
			BlocksVerifiedParity: true,
			executorEvidenceBridge.Status.ToString(),
			"Executor evidence bridge must keep implementation, execution, result emission, runtime comparison, and verified parity disabled.",
			$"rows={executorEvidenceBridge.Rows.Count}; hasAnyRuntimeEvidence={executorEvidenceBridge.HasAnyRuntimeEvidence}; canWriteExecutableExecutor={executorEvidenceBridge.CanWriteExecutableExecutor}; canExecuteExecutor={executorEvidenceBridge.CanExecuteExecutor}; canEmitResults={executorEvidenceBridge.CanEmitResults}; canRunRuntimeComparison={executorEvidenceBridge.CanRunRuntimeComparison}; canClaimVerifiedParity={executorEvidenceBridge.CanClaimVerifiedParity}; executorEvidenceBridgeRows={executorEvidenceBridgeRows}",
			"Bridge readiness is still blocked metadata and must not enable executor implementation.");
	}

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow RuntimeComparisonAndLiveDispatchRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus auditStatus,
		FindGroupMutationPostProjectedValueExecutorEvidenceBridge executorEvidenceBridge)
	{
		var executorEvidenceBridgeRows = BridgeRowEvidence(executorEvidenceBridge);
		return new(
			order,
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.RuntimeComparisonAndLiveDispatch,
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedRuntimeComparisonMissing,
			HasProvider: executorEvidenceBridge.Rows.Any(row => row.Requirement == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.RuntimeComparisonHandoff),
			BlocksMaterialization: true,
			BlocksResultEmission: true,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksLiveDispatch: true,
			BlocksVerifiedParity: true,
			auditStatus.ToString(),
			"Runtime comparison and live dispatch require deterministic Java/C# runtime evidence after value reads, row identity, materialization, emission, and executor implementation evidence exist.",
			$"bridgeStatus={executorEvidenceBridge.Status}; bridgeRuntimeRows={executorEvidenceBridge.Rows.Count(row => row.Requirement == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.RuntimeComparisonHandoff)}; canRunRuntimeComparison={executorEvidenceBridge.CanRunRuntimeComparison}; canClaimVerifiedParity={executorEvidenceBridge.CanClaimVerifiedParity}; executorEvidenceBridgeRows={executorEvidenceBridgeRows}",
			"Live dispatch and verified parity remain blocked even when the metadata chain is internally consistent.");
	}

	private static string BridgeRowEvidence(FindGroupMutationPostProjectedValueExecutorEvidenceBridge executorEvidenceBridge)
	{
		return executorEvidenceBridge.Rows.Count == 0
			? "none"
			: string.Join(" | ", executorEvidenceBridge.Rows.Select(row => $"{row.Requirement}={row.CurrentEvidence}"));
	}

	private static string DecisionFor(FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedMaterializationBlockerNotReady => "Projected-value executor consistency audit is blocked until materialization blockers reach unread projected-value readiness.",
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedResultEmissionBlockerNotReady => "Projected-value executor consistency audit is blocked until result-emission gate and result-emission blocker metadata agree that emission remains unavailable.",
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedExecutorEvidenceBridgeNotReady => "Projected-value executor consistency audit is blocked until evidence summary and executor bridge metadata agree that implementation remains unavailable.",
			_ => "Projected-value executor metadata is internally consistent, but materialization, emission, executable implementation, runtime comparison, live dispatch, and verified parity remain blocked.",
		};
	}
}
