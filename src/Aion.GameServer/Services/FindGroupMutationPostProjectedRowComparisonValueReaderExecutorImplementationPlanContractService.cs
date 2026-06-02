namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus
{
	BlockedExecutorReadinessGateNotReady,
	BlockedExecutorImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep
{
	RowIdentityPairing,
	JavaTypedValueRead,
	CSharpTypedValueRead,
	EqualityComparison,
	ResultSelection,
	MismatchContextAttachment,
	ResultEmission,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus
{
	BlockedExecutorReadinessGateNotReady,
	BlockedRuntimeRowsMissing,
	BlockedReaderImplementationDeferred,
	BlockedComparisonDeferred,
	BlockedResultSelectionDeferred,
	BlockedContextAttachmentDeferred,
	BlockedResultEmissionDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep Step,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus Status,
	int EqualityFieldCount,
	int RuntimeContextFieldCount,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind> OutputKinds,
	bool RequiresAcceptedJavaRows,
	bool RequiresAcceptedCSharpRows,
	bool RequiresJavaValueReader,
	bool RequiresCSharpValueReader,
	bool RequiresProjectedValues,
	bool RequiresResultSchema,
	bool CanImplement,
	bool CanExecute,
	bool CanReadValues,
	bool CanCompareValues,
	bool CanAttachContext,
	bool CanEmitResults,
	string ImplementationTask,
	string PrerequisiteProvider,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepRow> Steps,
	int EqualityFieldCount,
	int RuntimeContextFieldCount,
	bool HasExecutorReadinessGate,
	bool HasComparatorPreflight,
	bool HasRuntimeEvidence,
	bool CanImplementExecutor,
	bool CanExecuteExecutor,
	bool CanReadJavaValues,
	bool CanReadCSharpValues,
	bool CanCompareValues,
	bool CanAttachRuntimeContext,
	bool CanEmitResults,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live implementation plan for a future
/// CM_FIND_GROUP action 2/6 value-reader executor. It enumerates the concrete
/// row pairing, typed read, comparison, context, and result-emission tasks
/// without implementing or executing them.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateReport? executorReadinessGate = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract? comparatorPreflight = null)
	{
		executorReadinessGate ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateService.Create();
		comparatorPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService.Create();

		var status = executorReadinessGate.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedExecutorImplementationDeferred
			? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus.BlockedExecutorImplementationDeferred
			: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus.BlockedExecutorReadinessGateNotReady;
		var steps = new[]
		{
			StepRow(
				1,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.RowIdentityPairing,
				StatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.RowIdentityPairing),
				comparatorPreflight,
				OutputKinds: [],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresJavaValueReader: false,
				RequiresCSharpValueReader: false,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Pair accepted Java and C# rows by action, mutationKind, activePlayerObjectId, and mutatedEntryObjectId before any field reader executes.",
				"FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService",
				"Java action 2 maps to Recruitment; Java action 6 maps to Application; both rows must use the mutation-post schema-v1 row identity."),
			StepRow(
				2,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.JavaTypedValueRead,
				StatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.JavaTypedValueRead),
				comparatorPreflight,
				OutputKinds: [],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: false,
				RequiresJavaValueReader: true,
				RequiresCSharpValueReader: false,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Read every required equality value from the Java schema-v1 JSON row using the typed scalar, enum/string, and ordered-list readers.",
				"FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService",
				"Java values must come from runtime-backed mutation-post artifacts, not shape-only repository fixtures."),
			StepRow(
				3,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.CSharpTypedValueRead,
				StatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.CSharpTypedValueRead),
				comparatorPreflight,
				OutputKinds: [],
				RequiresAcceptedJavaRows: false,
				RequiresAcceptedCSharpRows: true,
				RequiresJavaValueReader: false,
				RequiresCSharpValueReader: true,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Read every required equality value from the accepted live C# trace-export row using the same typed reader family as Java.",
				"FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService",
				"C# values must come from accepted live boundary rows with executor and registry observations, not disabled projections."),
			StepRow(
				4,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison,
				StatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison),
				comparatorPreflight,
				[
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresJavaValueReader: true,
				RequiresCSharpValueReader: true,
				RequiresProjectedValues: true,
				RequiresResultSchema: true,
				"Compare projected Java/C# equality values field-by-field and preserve ordered-list equality for visibleEntryObjectIdsAfterMutation.",
				"FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService",
				"Matched is allowed only when every equality field matches; otherwise the executor must select FieldMismatch."),
			StepRow(
				5,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection,
				StatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection),
				comparatorPreflight,
				[
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresJavaValueReader: false,
				RequiresCSharpValueReader: false,
				RequiresProjectedValues: true,
				RequiresResultSchema: true,
				"Select exactly one comparison result per row identity: Matched, MissingJavaRow, MissingCSharpRow, or FieldMismatch.",
				"FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService",
				"Missing-row decisions come from row identity matching; field mismatch decisions come from projected equality values."),
			StepRow(
				6,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment,
				StatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment),
				comparatorPreflight,
				[FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext],
				RequiresAcceptedJavaRows: false,
				RequiresAcceptedCSharpRows: false,
				RequiresJavaValueReader: false,
				RequiresCSharpValueReader: false,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Attach ignored runtime context only after MissingJavaRow, MissingCSharpRow, or FieldMismatch exists.",
				"FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService",
				"traceSource and serverEpochSeconds remain diagnostic context and must never affect equality or create a standalone result."),
			StepRow(
				7,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultEmission,
				StatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultEmission),
				comparatorPreflight,
				[
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
				],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresJavaValueReader: true,
				RequiresCSharpValueReader: true,
				RequiresProjectedValues: true,
				RequiresResultSchema: true,
				"Emit result rows only after row identity, typed reads, equality comparison, result selection, and context attachment are complete.",
				"FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService",
				"Result emission remains disabled until runtime-backed Java and C# rows can be compared deterministically."),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract(
			status,
			steps,
			comparatorPreflight.EqualityFieldCount,
			comparatorPreflight.RuntimeContextFieldCount,
			HasExecutorReadinessGate: executorReadinessGate.Rows.Count > 0,
			HasComparatorPreflight: comparatorPreflight.Stages.Count > 0,
			HasRuntimeEvidence: executorReadinessGate.HasRuntimeEvidence,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanCompareValues: false,
			CanAttachRuntimeContext: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			comparatorPreflight.TraceName,
			comparatorPreflight.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepRow StepRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep step,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract comparatorPreflight,
		IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind> OutputKinds,
		bool RequiresAcceptedJavaRows,
		bool RequiresAcceptedCSharpRows,
		bool RequiresJavaValueReader,
		bool RequiresCSharpValueReader,
		bool RequiresProjectedValues,
		bool RequiresResultSchema,
		string implementationTask,
		string prerequisiteProvider,
		string notes) =>
		new(
			order,
			step,
			status,
			comparatorPreflight.EqualityFieldCount,
			comparatorPreflight.RuntimeContextFieldCount,
			OutputKinds,
			RequiresAcceptedJavaRows,
			RequiresAcceptedCSharpRows,
			RequiresJavaValueReader,
			RequiresCSharpValueReader,
			RequiresProjectedValues,
			RequiresResultSchema,
			CanImplement: false,
			CanExecute: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanAttachContext: false,
			CanEmitResults: false,
			implementationTask,
			prerequisiteProvider,
			$"comparatorStatus={comparatorPreflight.Status}; equalityFields={comparatorPreflight.EqualityFieldCount}; runtimeContextFields={comparatorPreflight.RuntimeContextFieldCount}; canProjectValues={comparatorPreflight.CanProjectValues}; canCompareValues={comparatorPreflight.CanCompareValues}; canEmitResults={comparatorPreflight.CanEmitResults}",
			notes);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep step)
	{
		if (status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus.BlockedExecutorReadinessGateNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedExecutorReadinessGateNotReady;

		return step switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.RowIdentityPairing => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedRuntimeRowsMissing,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.JavaTypedValueRead => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedReaderImplementationDeferred,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.CSharpTypedValueRead => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedReaderImplementationDeferred,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedComparisonDeferred,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedResultSelectionDeferred,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedContextAttachmentDeferred,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedResultEmissionDeferred,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus.BlockedExecutorReadinessGateNotReady => "Value-reader executor implementation plan is blocked until the executor readiness gate reaches deferred-implementation readiness.",
			_ => "Value-reader executor implementation plan is ordered, but row pairing, typed reads, comparison, context attachment, result emission, and verified parity remain intentionally deferred.",
		};
	}
}
