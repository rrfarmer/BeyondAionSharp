namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus
{
	BlockedEvidenceSummaryNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedExecutableImplementationNotAllowed,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep Step,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus PlanStatus,
	bool HasImplementationPlanStep,
	bool HasEvidenceSummary,
	bool RequiresRuntimeEvidence,
	bool BlocksExecutableCode,
	bool CanWriteExecutableCode,
	bool CanExecute,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditRow> Rows,
	bool HasImplementationPlan,
	bool HasEvidenceSummary,
	bool HasAnyRuntimeEvidence,
	bool CanWriteExecutableExecutor,
	bool CanExecuteExecutor,
	bool CanReadValues,
	bool CanCompareValues,
	bool CanEmitResults,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live implementation readiness audit for the
/// future CM_FIND_GROUP action 2/6 value-reader executor. It joins the concrete
/// implementation plan with the evidence summary, but it never writes executable
/// reader/comparator code.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract? evidenceSummary = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract? implementationPlan = null)
	{
		evidenceSummary ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractService.Create();
		implementationPlan ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService.Create();

		var status = StatusFor(evidenceSummary);
		var rows = implementationPlan.Steps
			.Select(step => AuditRow(step, evidenceSummary, status))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit(
			status,
			rows,
			HasImplementationPlan: implementationPlan.Steps.Count > 0,
			HasEvidenceSummary: evidenceSummary.Rows.Count > 0,
			HasAnyRuntimeEvidence: false,
			CanWriteExecutableExecutor: false,
			CanExecuteExecutor: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			evidenceSummary.TraceName,
			evidenceSummary.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract evidenceSummary)
	{
		return evidenceSummary.Status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedEvidenceSummaryNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedRuntimeEvidenceMissing,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedExecutableImplementationNotAllowed,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditRow AuditRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepRow planStep,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract evidenceSummary,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus auditStatus)
	{
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditRow(
			planStep.Order,
			planStep.Step,
			planStep.Status,
			HasImplementationPlanStep: true,
			HasEvidenceSummary: evidenceSummary.Rows.Count > 0,
			RequiresRuntimeEvidence: RequiresRuntimeEvidence(planStep.Step),
			BlocksExecutableCode: true,
			CanWriteExecutableCode: false,
			CanExecute: false,
			RequiredEvidenceFor(planStep.Step),
			$"auditStatus={auditStatus}; evidenceSummaryStatus={evidenceSummary.Status}; planStatus={planStep.Status}; hasAnyRuntimeEvidence={evidenceSummary.HasAnyRuntimeEvidence}; canImplementExecutor={evidenceSummary.CanImplementExecutor}; canExecuteExecutor={evidenceSummary.CanExecuteExecutor}; canEmitResults={evidenceSummary.CanEmitResults}",
			NotesFor(planStep.Step));
	}

	private static bool RequiresRuntimeEvidence(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep step) =>
		step is not FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment;

	private static string RequiredEvidenceFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep step)
	{
		return step switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.RowIdentityPairing => "Runtime-backed Java rows and accepted live C# boundary rows must exist before row identity pairing code can execute.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.JavaTypedValueRead => "Runtime-backed Java artifact rows must exist before Java typed value readers can be executable.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.CSharpTypedValueRead => "Accepted live C# trace-export rows must exist before C# typed value readers can be executable.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison => "Projected Java and C# values plus row identity pairing must exist before equality comparison code can execute.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection => "Materialized matched, missing-row, or field-mismatch decisions must exist before result selection can execute.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment => "A parent MissingJavaRow, MissingCSharpRow, or FieldMismatch result must exist before ignored runtime context can attach.",
			_ => "Result-emission gate, materialized rows, and runtime comparison evidence must exist before result emission code can execute.",
		};
	}

	private static string NotesFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep step)
	{
		return step switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.RowIdentityPairing => "Pairing implementation must wait for runtime row sources; synthetic shape rows are not enough.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.JavaTypedValueRead => "Java readers must read capture-backed schema-v1 rows, not repository fixture metadata.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.CSharpTypedValueRead => "C# readers must read accepted live boundary exports with executor and registry observations.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison => "Equality comparison must preserve Java-derived field semantics and ordered-list handling.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection => "Missing-row and mismatch decisions must come from runtime row identity/value evidence.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment => "Ignored runtime context remains diagnostic and cannot create a result.",
			_ => "Emission remains blocked until deterministic runtime comparison evidence exists.",
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedEvidenceSummaryNotReady => "Value-reader executor implementation readiness audit is blocked until evidence summary metadata is ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedRuntimeEvidenceMissing => "Value-reader executor implementation readiness audit is blocked because runtime evidence required by the executor summary is missing.",
			_ => "Value-reader executor implementation readiness audit is complete as metadata, but executable reader/comparator code remains disallowed until runtime comparison evidence exists.",
		};
	}
}
