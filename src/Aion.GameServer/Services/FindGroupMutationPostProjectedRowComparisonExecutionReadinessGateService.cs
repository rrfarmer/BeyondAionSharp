namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus
{
	BlockedLiveInputHandoffNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedComparatorNotAllowed,
}

public enum FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate
{
	LiveInputHandoff,
	RuntimeEvidenceChecklist,
	RuntimeEvidencePresence,
	ValueProjection,
	ResultEmission,
	RuntimeComparison,
	LiveDispatchApproval,
}

public enum FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus
{
	BlockedUpstreamNotReady,
	BlockedMissingRuntimeEvidence,
	BlockedImplementationDeferred,
	BlockedLiveDispatchDisabled,
}

public sealed record FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate Gate,
	FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus Status,
	bool IsSatisfied,
	bool BlocksComparatorImplementation,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateReport(
	FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow> Rows,
	bool HasLiveInputHandoff,
	bool HasRuntimeEvidenceChecklist,
	bool HasRuntimeEvidence,
	bool CanImplementComparator,
	bool CanExecuteComparator,
	bool CanClaimVerifiedParity,
	bool CanEnableLiveDispatch,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: final non-live go/no-go gate before implementing a
/// CM_FIND_GROUP action 2/6 projected-row comparator. It combines handoff and
/// evidence checklist blockers, but it never compares rows.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateService
{
	public static FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateReport Create(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract? liveInputHandoff = null,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist? runtimeEvidenceChecklist = null)
	{
		liveInputHandoff ??= FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create();
		runtimeEvidenceChecklist ??= FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create(liveInputHandoff);
		var status = DetermineStatus(liveInputHandoff, runtimeEvidenceChecklist);
		var rows = new[]
		{
			LiveInputHandoffRow(liveInputHandoff),
			RuntimeEvidenceChecklistRow(runtimeEvidenceChecklist),
			RuntimeEvidencePresenceRow(runtimeEvidenceChecklist),
			DeferredImplementationRow(
				4,
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.ValueProjection,
				"Projected Java/C# equality values for every required result-contract field.",
				$"hasRuntimeEvidence={runtimeEvidenceChecklist.HasAnyRuntimeEvidence}; canStartProjectedComparison={runtimeEvidenceChecklist.CanStartProjectedComparison}",
				"Comparator implementation remains blocked until runtime rows are available for value projection."),
			DeferredImplementationRow(
				5,
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.ResultEmission,
				"Real Matched, MissingJavaRow, MissingCSharpRow, FieldMismatch, and IgnoredRuntimeContext outputs from projected comparison.",
				$"canClaimVerifiedParity={runtimeEvidenceChecklist.CanClaimVerifiedParity}",
				"Result emission remains blocked until a comparator can inspect projected values."),
			DeferredImplementationRow(
				6,
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.RuntimeComparison,
				"Deterministic Java/C# runtime or socket comparison for action 2/6 mutation-post behavior.",
				$"checklistStatus={runtimeEvidenceChecklist.Status}; hasRuntimeEvidence={runtimeEvidenceChecklist.HasAnyRuntimeEvidence}",
				"Runtime comparison must execute before verified parity can be claimed."),
			LiveDispatchApprovalRow(liveInputHandoff),
		};

		return new FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateReport(
			status,
			rows,
			HasLiveInputHandoff: liveInputHandoff.Requirements.Count > 0,
			HasRuntimeEvidenceChecklist: runtimeEvidenceChecklist.Rows.Count > 0,
			HasRuntimeEvidence: false,
			CanImplementComparator: false,
			CanExecuteComparator: false,
			CanClaimVerifiedParity: false,
			CanEnableLiveDispatch: false,
			DecisionFor(status),
			liveInputHandoff.TraceName,
			liveInputHandoff.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus DetermineStatus(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract liveInputHandoff,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist runtimeEvidenceChecklist)
	{
		if (liveInputHandoff.Status == FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedSummaryNotReady
			|| runtimeEvidenceChecklist.Status == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedSummaryNotReady)
			return FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedLiveInputHandoffNotReady;

		if (!runtimeEvidenceChecklist.HasAnyRuntimeEvidence)
			return FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedRuntimeEvidenceMissing;

		return FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedComparatorNotAllowed;
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow LiveInputHandoffRow(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract liveInputHandoff)
	{
		var satisfied = liveInputHandoff.Status == FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedMissingRuntimeArtifacts;
		return new FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow(
			1,
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.LiveInputHandoff,
			satisfied
				? FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedMissingRuntimeEvidence
				: FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedUpstreamNotReady,
			satisfied,
			BlocksComparatorImplementation: true,
			"Live-input handoff must reach runtime-artifact readiness and enumerate all action 2/6 runtime inputs.",
			$"status={liveInputHandoff.Status}; requirements={liveInputHandoff.Requirements.Count}; canStartLiveComparison={liveInputHandoff.CanStartLiveComparison}",
			satisfied
				? "Handoff metadata is ready, but runtime artifacts are still missing."
				: "Handoff is blocked before runtime artifact collection.");
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow RuntimeEvidenceChecklistRow(
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist runtimeEvidenceChecklist)
	{
		var satisfied = runtimeEvidenceChecklist.Status == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedRuntimeEvidenceMissing;
		return new FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow(
			2,
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.RuntimeEvidenceChecklist,
			satisfied
				? FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedMissingRuntimeEvidence
				: FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedUpstreamNotReady,
			satisfied,
			BlocksComparatorImplementation: true,
			"Runtime evidence checklist must map every handoff requirement to an evidence provider and required next evidence.",
			$"status={runtimeEvidenceChecklist.Status}; rows={runtimeEvidenceChecklist.Rows.Count}; hasExistingNonLiveProviders={runtimeEvidenceChecklist.HasExistingNonLiveProviders}",
			satisfied
				? "Checklist exists, but provider mappings are not runtime evidence."
				: "Checklist is blocked before runtime artifact readiness.");
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow RuntimeEvidencePresenceRow(
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist runtimeEvidenceChecklist)
	{
		return new FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow(
			3,
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.RuntimeEvidencePresence,
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedMissingRuntimeEvidence,
			IsSatisfied: false,
			BlocksComparatorImplementation: true,
			"Runtime-backed Java artifacts and accepted live C# boundary rows with executor and registry observations.",
			$"hasAnyRuntimeEvidence={runtimeEvidenceChecklist.HasAnyRuntimeEvidence}; canStartProjectedComparison={runtimeEvidenceChecklist.CanStartProjectedComparison}",
			"Non-live scaffolds, provider names, shape-valid artifacts, and disabled C# rows do not satisfy runtime evidence.");
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow DeferredImplementationRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate gate,
		string requiredEvidence,
		string currentEvidence,
		string notes)
	{
		return new FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow(
			order,
			gate,
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedImplementationDeferred,
			IsSatisfied: false,
			BlocksComparatorImplementation: true,
			requiredEvidence,
			currentEvidence,
			notes);
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow LiveDispatchApprovalRow(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract liveInputHandoff)
	{
		return new FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRow(
			7,
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.LiveDispatchApproval,
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedLiveDispatchDisabled,
			IsSatisfied: false,
			BlocksComparatorImplementation: true,
			"Production live dispatch remains disabled until runtime comparison proves parity and a broad-validation trigger is documented.",
			$"canEnableLiveDispatch={liveInputHandoff.CanEnableLiveDispatch}; isLive={liveInputHandoff.IsLive}",
			"Do not enable GameServerConnection.ProcessPacketAsync CmFindGroup dispatch from this gate.");
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedLiveInputHandoffNotReady => "Projected-row comparator implementation is not allowed because the live-input handoff is not ready for runtime artifacts.",
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedRuntimeEvidenceMissing => "Projected-row comparator implementation is not allowed because runtime evidence is missing even though handoff/checklist metadata exists.",
			_ => "Projected-row comparator implementation is not allowed until runtime evidence, value projection, result emission, and runtime comparison are complete.",
		};
	}
}
