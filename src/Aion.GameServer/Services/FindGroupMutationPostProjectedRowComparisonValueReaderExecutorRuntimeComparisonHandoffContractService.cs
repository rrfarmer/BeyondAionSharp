namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus
{
	BlockedImplementationAuditNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedExecutableImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement
{
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
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit? implementationReadinessAudit = null)
	{
		implementationReadinessAudit ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditService.Create();

		var status = StatusFor(implementationReadinessAudit);
		var rows = new[]
		{
			Row(
				1,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows,
				status,
				implementationReadinessAudit,
				"Capture-enabled Java action 2 and action 6 mutation-post artifact rows generated from CM_FIND_GROUP.readImpl/runImpl and FindGroupService.addRecruitment/addApplication hooks.",
				"Current Java rows are checked-in shape artifacts only; runtime-backed capture rows for both actions are still missing.",
				"Java rows must be schema-v1 runtime rows before Java typed readers can be executable.",
				requiresRuntimeComparisonStart: true),
			Row(
				2,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.CSharpBoundaryRows,
				status,
				implementationReadinessAudit,
				"Accepted live C# CM_FIND_GROUP boundary rows for action 2 and action 6 with packet acceptance, active-player facts, mutation facts, and side-effect guard fields.",
				"Disabled projections and fixture rows are non-live and cannot feed executable C# typed readers.",
				"C# rows must be accepted boundary exports, not disabled direct-packet projections.",
				requiresRuntimeComparisonStart: true),
			Row(
				3,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.BoundaryExecutorObservation,
				status,
				implementationReadinessAudit,
				"Observed boundary executor invocation after CM_FIND_GROUP packet acceptance and before comparison input envelope creation.",
				"Executor skeleton and envelope metadata exist, but no guarded live boundary executor observation exists.",
				"Runtime comparison cannot start until the boundary path that creates comparison inputs is observed.",
				requiresRuntimeComparisonStart: true),
			Row(
				4,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RegistrySendObservation,
				status,
				implementationReadinessAudit,
				"Observed Java-order active-player registry sends for actions 2 and 6: posted SM_SYSTEM_MESSAGE before refreshed SM_FIND_GROUP, with zero broadcasts and zero invite dispatches.",
				"Registry contracts name the expected sends but do not observe live send order.",
				"Registry observations must cover the mutation-post side effects before result output can be trusted.",
				requiresRuntimeComparisonStart: true),
			Row(
				5,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RowIdentityMatching,
				status,
				implementationReadinessAudit,
				"Runtime Java and C# rows paired by action, mutationKind, activePlayerObjectId, and mutatedEntryObjectId.",
				"Row identity pairing is still metadata-only; no runtime Java/C# row keys have been matched.",
				"Row pairing must exist before any typed value reader or missing-row result selection can execute.",
				requiresRuntimeComparisonStart: true),
			Row(
				6,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ValueProjection,
				status,
				implementationReadinessAudit,
				"Projected Java JSON and C# trace-export values for every equality field named by the value-reader schema, including ordered visibleEntryObjectIdsAfterMutation values.",
				"Value contracts name fields, but no Java JSON values or C# trace-export values are read.",
				"Value projection must prove Java/C# typed reader inputs before equality comparison code can execute.",
				requiresRuntimeComparisonStart: true),
			Row(
				7,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.Materialization,
				status,
				implementationReadinessAudit,
				"Materialized Matched, MissingJavaRow, MissingCSharpRow, FieldMismatch, or IgnoredRuntimeContext output rows from runtime row identity and projected value decisions.",
				"Materialization preflight explains blockers, but no output row can materialize.",
				"Materialization must exist before result-emission handoff or verified parity can be evaluated.",
				requiresRuntimeComparisonStart: false),
			Row(
				8,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ResultEmission,
				status,
				implementationReadinessAudit,
				"Emitted comparison result rows for Matched, MissingJavaRow, MissingCSharpRow, FieldMismatch, plus attached ignored runtime context when a parent result exists.",
				"Result-emission gate remains closed; no result rows are emittable.",
				"Result emission must be deterministic and schema-backed before parity evidence can be claimed.",
				requiresRuntimeComparisonStart: false),
			Row(
				9,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RuntimeComparison,
				status,
				implementationReadinessAudit,
				"Deterministic Java/C# runtime or socket comparison for action 2/6 mutation, packet, side-effect, value projection, materialization, and result-output observations.",
				"No runtime or socket comparison has executed.",
				"Runtime comparison is the final objective evidence gate for verified parity.",
				requiresRuntimeComparisonStart: false),
			Row(
				10,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation,
				status,
				implementationReadinessAudit,
				"Executable value-reader executor implementation may start only after Java artifacts, C# boundary rows, row identity, projected values, materialization, emission, and runtime comparison evidence are present.",
				"Implementation readiness audit blocks every executable reader/comparator/result-emission step.",
				"Executable implementation remains deferred; this handoff is metadata only.",
				requiresRuntimeComparisonStart: false),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContract(
			status,
			rows,
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

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit implementationReadinessAudit)
	{
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
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit implementationReadinessAudit,
		string requiredEvidence,
		string currentEvidence,
		string notes,
		bool requiresRuntimeComparisonStart)
	{
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRow(
			order,
			requirement,
			RowStatusFor(handoffStatus, requirement),
			HasImplementationReadinessAudit: implementationReadinessAudit.Rows.Count > 0,
			HasRuntimeEvidence: false,
			RequiredBeforeExecutableImplementation: true,
			RequiredBeforeRuntimeComparison: requiresRuntimeComparisonStart,
			RequiredBeforeVerifiedParity: true,
			CanStartExecutableImplementation: false,
			requiredEvidence,
			$"{currentEvidence} auditStatus={implementationReadinessAudit.Status}; auditRows={implementationReadinessAudit.Rows.Count}; hasAnyRuntimeEvidence={implementationReadinessAudit.HasAnyRuntimeEvidence}; canWriteExecutableExecutor={implementationReadinessAudit.CanWriteExecutableExecutor}; canExecuteExecutor={implementationReadinessAudit.CanExecuteExecutor}; canClaimVerifiedParity={implementationReadinessAudit.CanClaimVerifiedParity}",
			notes);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus handoffStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement requirement)
	{
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
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedImplementationAuditNotReady => "Value-reader executor runtime comparison handoff is blocked until implementation readiness audit metadata is ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedRuntimeEvidenceMissing => "Value-reader executor runtime comparison handoff is blocked because Java artifact rows, C# boundary rows, value projection, materialization, result emission, and runtime comparison evidence are missing.",
			_ => "Value-reader executor runtime comparison handoff is defined, but executable implementation remains deferred until runtime comparison evidence exists.",
		};
	}
}
