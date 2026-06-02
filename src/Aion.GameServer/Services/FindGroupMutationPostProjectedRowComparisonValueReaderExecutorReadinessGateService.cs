namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus
{
	BlockedComparatorPreflightNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedExecutorImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate
{
	LiveInputHandoff,
	RuntimeEvidenceChecklist,
	ComparatorPreflight,
	ExecutorImplementation,
	LiveDispatchGuard,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus
{
	BlockedLiveInputHandoffNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedComparatorPreflightNotReady,
	BlockedExecutorImplementationDeferred,
	BlockedLiveDispatchDisabled,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate Gate,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus Status,
	bool HasPrerequisite,
	bool HasRuntimeEvidence,
	bool BlocksExecutorImplementation,
	bool CanImplementExecutor,
	bool CanExecuteExecutor,
	bool CanEnableLiveDispatch,
	string Evidence,
	string RequiredNextEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateReport(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow> Rows,
	bool HasLiveInputHandoff,
	bool HasRuntimeEvidenceChecklist,
	bool HasComparatorPreflight,
	bool HasRuntimeEvidence,
	bool CanImplementExecutor,
	bool CanExecuteExecutor,
	bool CanProjectValues,
	bool CanCompareValues,
	bool CanEmitResults,
	bool CanEnableLiveDispatch,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live go/no-go gate before implementing the
/// CM_FIND_GROUP action 2/6 value-reader executor. It combines comparator
/// preflight, runtime evidence checklist, and live-input handoff metadata
/// without reading values or executing comparison.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateReport Create(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract? liveInputHandoff = null,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist? runtimeEvidenceChecklist = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract? comparatorPreflight = null)
	{
		liveInputHandoff ??= FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create();
		runtimeEvidenceChecklist ??= FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create(liveInputHandoff);
		comparatorPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService.Create();

		var status = DetermineStatus(liveInputHandoff, runtimeEvidenceChecklist, comparatorPreflight);
		var rows = new[]
		{
			LiveInputHandoffRow(liveInputHandoff),
			RuntimeEvidenceChecklistRow(runtimeEvidenceChecklist),
			ComparatorPreflightRow(comparatorPreflight),
			ExecutorImplementationRow(comparatorPreflight, runtimeEvidenceChecklist),
			LiveDispatchGuardRow(liveInputHandoff),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateReport(
			status,
			rows,
			HasLiveInputHandoff: liveInputHandoff.Requirements.Count > 0,
			HasRuntimeEvidenceChecklist: runtimeEvidenceChecklist.Rows.Count > 0,
			HasComparatorPreflight: comparatorPreflight.Stages.Count > 0,
			HasRuntimeEvidence: runtimeEvidenceChecklist.HasAnyRuntimeEvidence,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanEnableLiveDispatch: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			comparatorPreflight.TraceName,
			comparatorPreflight.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus DetermineStatus(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract liveInputHandoff,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist runtimeEvidenceChecklist,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract comparatorPreflight)
	{
		if (comparatorPreflight.Status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedResultSchemaNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedComparatorPreflightNotReady;

		if (liveInputHandoff.Status == FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedSummaryNotReady
			|| runtimeEvidenceChecklist.Status == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedSummaryNotReady
			|| !runtimeEvidenceChecklist.HasAnyRuntimeEvidence)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedRuntimeEvidenceMissing;

		return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedExecutorImplementationDeferred;
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow LiveInputHandoffRow(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract liveInputHandoff)
	{
		var readyForRuntimeInputs = liveInputHandoff.Status == FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedMissingRuntimeArtifacts;
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow(
			1,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.LiveInputHandoff,
			readyForRuntimeInputs
				? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedRuntimeEvidenceMissing
				: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedLiveInputHandoffNotReady,
			HasPrerequisite: liveInputHandoff.Requirements.Count > 0,
			HasRuntimeEvidence: liveInputHandoff.HasRequiredRuntimeEvidence,
			BlocksExecutorImplementation: true,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanEnableLiveDispatch: false,
			$"status={liveInputHandoff.Status}; requirements={liveInputHandoff.Requirements.Count}; canStartLiveComparison={liveInputHandoff.CanStartLiveComparison}",
			"Live-input handoff must enumerate runtime-backed Java artifacts, accepted C# boundary rows, executor observation, registry observation, value projection, result emission, and runtime comparison.",
			readyForRuntimeInputs
				? "Handoff metadata is ready for runtime artifacts, but it is still not runtime evidence."
				: "Handoff is blocked before value-reader executor implementation can be considered.");
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow RuntimeEvidenceChecklistRow(
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist runtimeEvidenceChecklist)
	{
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow(
			2,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.RuntimeEvidenceChecklist,
			runtimeEvidenceChecklist.HasAnyRuntimeEvidence
				? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedExecutorImplementationDeferred
				: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedRuntimeEvidenceMissing,
			HasPrerequisite: runtimeEvidenceChecklist.Rows.Count > 0,
			runtimeEvidenceChecklist.HasAnyRuntimeEvidence,
			BlocksExecutorImplementation: true,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanEnableLiveDispatch: false,
			$"status={runtimeEvidenceChecklist.Status}; rows={runtimeEvidenceChecklist.Rows.Count}; hasAnyRuntimeEvidence={runtimeEvidenceChecklist.HasAnyRuntimeEvidence}; canStartProjectedComparison={runtimeEvidenceChecklist.CanStartProjectedComparison}",
			"Runtime evidence checklist must contain actual Java/C# runtime rows and observations before any executor can read values.",
			"Provider mappings and shape-valid non-live scaffolds do not satisfy value-reader runtime evidence.");
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow ComparatorPreflightRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract comparatorPreflight)
	{
		var readyForExecutorPlanning = comparatorPreflight.Status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedComparatorImplementationDeferred;
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow(
			3,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.ComparatorPreflight,
			readyForExecutorPlanning
				? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedExecutorImplementationDeferred
				: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedComparatorPreflightNotReady,
			HasPrerequisite: comparatorPreflight.Stages.Count > 0,
			HasRuntimeEvidence: false,
			BlocksExecutorImplementation: true,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanEnableLiveDispatch: false,
			$"status={comparatorPreflight.Status}; stages={comparatorPreflight.Stages.Count}; equalityFields={comparatorPreflight.EqualityFieldCount}; runtimeContextFields={comparatorPreflight.RuntimeContextFieldCount}",
			"Comparator preflight must reach deferred-executor readiness before the value-reader executor plan can be written.",
			readyForExecutorPlanning
				? "Comparator stage metadata is ready, but executor implementation remains intentionally deferred."
				: "Result-schema or comparator preflight metadata is not ready for executor planning.");
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow ExecutorImplementationRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract comparatorPreflight,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist runtimeEvidenceChecklist)
	{
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow(
			4,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.ExecutorImplementation,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedExecutorImplementationDeferred,
			HasPrerequisite: comparatorPreflight.Status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedComparatorImplementationDeferred,
			runtimeEvidenceChecklist.HasAnyRuntimeEvidence,
			BlocksExecutorImplementation: true,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanEnableLiveDispatch: false,
			$"canExecuteComparator={comparatorPreflight.CanExecuteComparator}; canProjectValues={comparatorPreflight.CanProjectValues}; hasRuntimeEvidence={runtimeEvidenceChecklist.HasAnyRuntimeEvidence}",
			"Implement row pairing, typed reader reads, equality comparison, result selection, and context attachment only after runtime evidence is available.",
			"This gate is a go/no-go report only and must not materialize Matched, missing-row, FieldMismatch, or ignored-context results.");
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow LiveDispatchGuardRow(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract liveInputHandoff)
	{
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow(
			5,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.LiveDispatchGuard,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedLiveDispatchDisabled,
			HasPrerequisite: liveInputHandoff.Requirements.Any(row => row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.LiveDispatchGuard),
			HasRuntimeEvidence: false,
			BlocksExecutorImplementation: true,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanEnableLiveDispatch: false,
			$"canEnableLiveDispatch={liveInputHandoff.CanEnableLiveDispatch}; isLive={liveInputHandoff.IsLive}",
			"Production CM_FIND_GROUP dispatch remains disabled until runtime comparison and broad-validation trigger documentation exist.",
			"Do not wire GameServerConnection.ProcessPacketAsync from this value-reader executor readiness gate.");
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedComparatorPreflightNotReady => "Value-reader executor implementation is not allowed because comparator preflight metadata is not ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedRuntimeEvidenceMissing => "Value-reader executor implementation is not allowed because runtime evidence and live-input handoff evidence are missing.",
			_ => "Value-reader executor implementation remains intentionally deferred even though prerequisite metadata and runtime-evidence flags are present.",
		};
	}
}
