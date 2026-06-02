namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus
{
	BlockedExecutorConsistencyAuditNotReady,
	BlockedImplementationAuditNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedExecutableImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement
{
	ExecutorConsistencyAudit,
	JavaArtifactRows,
	CSharpBoundaryRows,
	BoundaryExecutorObservation,
	RegistrySendObservation,
	RowIdentityMatching,
	ValueProjection,
	Materialization,
	ResultEmission,
	RuntimeComparison,
	ExecutableImplementation,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus
{
	BlockedExecutorConsistencyAuditNotReady,
	BlockedImplementationAuditNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedValueProjectionMissing,
	BlockedMaterializationMissing,
	BlockedResultEmissionMissing,
	BlockedRuntimeComparisonMissing,
	BlockedExecutableImplementationDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement Requirement,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus Status,
	bool HasExecutorConsistencyAudit,
	bool HasImplementationReadinessAudit,
	bool HasRuntimeEvidence,
	bool RequiredBeforeExecutableImplementation,
	bool RequiredBeforeRuntimeComparison,
	bool RequiredBeforeVerifiedParity,
	bool CanStartExecutableImplementation,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRow> Rows,
	bool HasExecutorConsistencyAudit,
	bool HasImplementationReadinessAudit,
	bool HasAnyRuntimeEvidence,
	bool CanStartExecutableImplementation,
	bool CanStartRuntimeComparison,
	bool CanReadValues,
	bool CanCompareValues,
	bool CanMaterializeOutputs,
	bool CanEmitResults,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live handoff contract for the future
/// CM_FIND_GROUP action 2/6 runtime comparison executor. It names the exact
/// Java artifact, C# boundary, value projection, materialization, result
/// emission, and runtime comparison evidence required before executable
/// reader/comparator code can start.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContract Create(
		FindGroupMutationPostProjectedValueExecutorConsistencyAudit? executorConsistencyAudit = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit? implementationReadinessAudit = null)
	{
		executorConsistencyAudit ??= DefaultExecutorConsistencyAuditBlocker();
		implementationReadinessAudit ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditService.Create();

		var status = StatusFor(executorConsistencyAudit, implementationReadinessAudit);
		var rows = new[]
		{
			Row(
				1,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutorConsistencyAudit,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Projected-value executor consistency audit must prove materialization blockers, result-emission gate rows, result-emission blockers, evidence summary, and executor bridge metadata agree before runtime-comparison handoff proceeds.",
				"Executor consistency audit is non-live blocker metadata and has not authorized materialization, emission, executable implementation, runtime comparison, or live dispatch.",
				"Consistency audit must be internally consistent before any capture execution blocker can treat downstream handoff rows as stable.",
				requiresRuntimeComparisonStart: true),
			Row(
				2,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Capture-enabled Java action 2 and action 6 mutation-post artifact rows generated from CM_FIND_GROUP.readImpl/runImpl and FindGroupService.addRecruitment/addApplication hooks.",
				"Current Java rows are checked-in shape artifacts only; runtime-backed capture rows for both actions are still missing.",
				"Java rows must be schema-v1 runtime rows before Java typed readers can be executable.",
				requiresRuntimeComparisonStart: true),
			Row(
				3,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.CSharpBoundaryRows,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Accepted live C# CM_FIND_GROUP boundary rows for action 2 and action 6 with packet acceptance, active-player facts, mutation facts, and side-effect guard fields.",
				"Disabled projections and fixture rows are non-live and cannot feed executable C# typed readers.",
				"C# rows must be accepted boundary exports, not disabled direct-packet projections.",
				requiresRuntimeComparisonStart: true),
			Row(
				4,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.BoundaryExecutorObservation,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Observed boundary executor invocation after CM_FIND_GROUP packet acceptance and before comparison input envelope creation.",
				"Executor skeleton and envelope metadata exist, but no guarded live boundary executor observation exists.",
				"Runtime comparison cannot start until the boundary path that creates comparison inputs is observed.",
				requiresRuntimeComparisonStart: true),
			Row(
				5,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RegistrySendObservation,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Observed Java-order active-player registry sends for actions 2 and 6: posted SM_SYSTEM_MESSAGE before refreshed SM_FIND_GROUP, with zero broadcasts and zero invite dispatches.",
				"Registry contracts name the expected sends but do not observe live send order.",
				"Registry observations must cover the mutation-post side effects before result output can be trusted.",
				requiresRuntimeComparisonStart: true),
			Row(
				6,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RowIdentityMatching,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Runtime Java and C# rows paired by action, mutationKind, activePlayerObjectId, and mutatedEntryObjectId.",
				"Row identity pairing is still metadata-only; no runtime Java/C# row keys have been matched.",
				"Row pairing must exist before any typed value reader or missing-row result selection can execute.",
				requiresRuntimeComparisonStart: true),
			Row(
				7,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ValueProjection,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Projected Java JSON and C# trace-export values for every equality field named by the value-reader schema, including ordered visibleEntryObjectIdsAfterMutation values.",
				"Value contracts name fields, but no Java JSON values or C# trace-export values are read.",
				"Value projection must prove Java/C# typed reader inputs before equality comparison code can execute.",
				requiresRuntimeComparisonStart: true),
			Row(
				8,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.Materialization,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Materialized Matched, MissingJavaRow, MissingCSharpRow, FieldMismatch, or IgnoredRuntimeContext output rows from runtime row identity and projected value decisions.",
				"Materialization preflight explains blockers, but no output row can materialize.",
				"Materialization must exist before result-emission handoff or verified parity can be evaluated.",
				requiresRuntimeComparisonStart: false),
			Row(
				9,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ResultEmission,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Emitted comparison result rows for Matched, MissingJavaRow, MissingCSharpRow, FieldMismatch, plus attached ignored runtime context when a parent result exists.",
				"Result-emission gate remains closed; no result rows are emittable.",
				"Result emission must be deterministic and schema-backed before parity evidence can be claimed.",
				requiresRuntimeComparisonStart: false),
			Row(
				10,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RuntimeComparison,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Deterministic Java/C# runtime or socket comparison for action 2/6 mutation, packet, side-effect, value projection, materialization, and result-output observations.",
				"No runtime or socket comparison has executed.",
				"Runtime comparison is the final objective evidence gate for verified parity.",
				requiresRuntimeComparisonStart: false),
			Row(
				11,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation,
				status,
				executorConsistencyAudit,
				implementationReadinessAudit,
				"Executable value-reader executor implementation may start only after Java artifacts, C# boundary rows, row identity, projected values, materialization, emission, and runtime comparison evidence are present.",
				"Implementation readiness audit blocks every executable reader/comparator/result-emission step.",
				"Executable implementation remains deferred; this handoff is metadata only.",
				requiresRuntimeComparisonStart: false),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContract(
			status,
			rows,
			HasExecutorConsistencyAudit: executorConsistencyAudit.Rows.Count > 0,
			HasImplementationReadinessAudit: implementationReadinessAudit.Rows.Count > 0,
			HasAnyRuntimeEvidence: false,
			CanStartExecutableImplementation: false,
			CanStartRuntimeComparison: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			implementationReadinessAudit.TraceName,
			implementationReadinessAudit.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAudit DefaultExecutorConsistencyAuditBlocker() =>
		new(
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedMaterializationBlockerNotReady,
			[
				DefaultExecutorConsistencyAuditRow(1, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.MaterializationBlocker),
				DefaultExecutorConsistencyAuditRow(2, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ResultEmissionGate),
				DefaultExecutorConsistencyAuditRow(3, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ResultEmissionBlocker),
				DefaultExecutorConsistencyAuditRow(4, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.EvidenceSummary),
				DefaultExecutorConsistencyAuditRow(5, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ExecutorEvidenceBridge),
				DefaultExecutorConsistencyAuditRow(6, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.RuntimeComparisonAndLiveDispatch),
			],
			HasMaterializationBlockerReport: false,
			HasResultEmissionGate: false,
			HasResultEmissionBlockerReport: false,
			HasEvidenceSummary: false,
			HasExecutorEvidenceBridge: false,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanWriteExecutableExecutor: false,
			CanRunRuntimeComparison: false,
			CanEnableLiveDispatch: false,
			CanClaimVerifiedParity: false,
			"Runtime-comparison handoff uses a local consistency-audit blocker by default to avoid recursive non-live provider construction.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow DefaultExecutorConsistencyAuditRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement requirement) =>
		new(
			order,
			requirement,
			requirement == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.RuntimeComparisonAndLiveDispatch
				? FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedRuntimeComparisonMissing
				: FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedMaterializationUnavailable,
			HasProvider: false,
			BlocksMaterialization: true,
			BlocksResultEmission: true,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksLiveDispatch: true,
			BlocksVerifiedParity: true,
			"NotConstructed",
			"Caller must provide an explicit projected-value executor consistency audit before runtime-comparison handoff can proceed.",
			"Default local blocker prevents recursive construction through downstream capture/runbook metadata.",
			"Runtime-comparison handoff remains non-live and blocked.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus StatusFor(
		FindGroupMutationPostProjectedValueExecutorConsistencyAudit executorConsistencyAudit,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit implementationReadinessAudit)
	{
		if (executorConsistencyAudit.Status != FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.ConsistentBlockedReadiness)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedExecutorConsistencyAuditNotReady;

		return implementationReadinessAudit.Status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedEvidenceSummaryNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedImplementationAuditNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedRuntimeEvidenceMissing => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedRuntimeEvidenceMissing,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedExecutableImplementationDeferred,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRow Row(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement requirement,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus handoffStatus,
		FindGroupMutationPostProjectedValueExecutorConsistencyAudit executorConsistencyAudit,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit implementationReadinessAudit,
		string requiredEvidence,
		string currentEvidence,
		string notes,
		bool requiresRuntimeComparisonStart)
	{
		var consistencyAuditRowEvidence = ConsistencyAuditRowEvidence(executorConsistencyAudit);
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRow(
			order,
			requirement,
			RowStatusFor(handoffStatus, requirement),
			HasExecutorConsistencyAudit: executorConsistencyAudit.Rows.Count > 0,
			HasImplementationReadinessAudit: implementationReadinessAudit.Rows.Count > 0,
			HasRuntimeEvidence: false,
			RequiredBeforeExecutableImplementation: true,
			RequiredBeforeRuntimeComparison: requiresRuntimeComparisonStart,
			RequiredBeforeVerifiedParity: true,
			CanStartExecutableImplementation: false,
			requiredEvidence,
			$"{currentEvidence} consistencyAuditStatus={executorConsistencyAudit.Status}; consistencyAuditRows={executorConsistencyAudit.Rows.Count}; consistencyCanRunRuntimeComparison={executorConsistencyAudit.CanRunRuntimeComparison}; consistencyCanClaimVerifiedParity={executorConsistencyAudit.CanClaimVerifiedParity}; consistencyAuditRowEvidence={consistencyAuditRowEvidence}; auditStatus={implementationReadinessAudit.Status}; auditRows={implementationReadinessAudit.Rows.Count}; hasAnyRuntimeEvidence={implementationReadinessAudit.HasAnyRuntimeEvidence}; canWriteExecutableExecutor={implementationReadinessAudit.CanWriteExecutableExecutor}; canExecuteExecutor={implementationReadinessAudit.CanExecuteExecutor}; canClaimVerifiedParity={implementationReadinessAudit.CanClaimVerifiedParity}",
			notes);
	}

	private static string ConsistencyAuditRowEvidence(FindGroupMutationPostProjectedValueExecutorConsistencyAudit executorConsistencyAudit)
	{
		return executorConsistencyAudit.Rows.Count == 0
			? "none"
			: string.Join(" | ", executorConsistencyAudit.Rows.Select(row => $"{row.Requirement}={row.CurrentEvidence}"));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus handoffStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement requirement)
	{
		if (handoffStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedExecutorConsistencyAuditNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedExecutorConsistencyAuditNotReady;

		if (handoffStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedImplementationAuditNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedImplementationAuditNotReady;

		if (handoffStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedExecutableImplementationDeferred
			&& requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedExecutableImplementationDeferred;

		return requirement switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ValueProjection => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedValueProjectionMissing,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.Materialization => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedMaterializationMissing,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ResultEmission => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedResultEmissionMissing,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RuntimeComparison => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedRuntimeComparisonMissing,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedExecutableImplementationDeferred,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedRuntimeEvidenceMissing,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedExecutorConsistencyAuditNotReady => "Value-reader executor runtime comparison handoff is blocked until projected-value executor consistency audit metadata is internally consistent.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedImplementationAuditNotReady => "Value-reader executor runtime comparison handoff is blocked until implementation readiness audit metadata is ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedRuntimeEvidenceMissing => "Value-reader executor runtime comparison handoff is blocked because Java artifact rows, C# boundary rows, value projection, materialization, result emission, and runtime comparison evidence are missing.",
			_ => "Value-reader executor runtime comparison handoff is defined, but executable implementation remains deferred until runtime comparison evidence exists.",
		};
	}
}
