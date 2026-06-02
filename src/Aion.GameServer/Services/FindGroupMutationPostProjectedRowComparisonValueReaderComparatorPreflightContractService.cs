namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus
{
	BlockedResultSchemaNotReady,
	BlockedComparatorImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage
{
	RowIdentityPairing,
	TypedReaderExecution,
	EqualityValueComparison,
	ResultSelection,
	MismatchContextAttachment,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus
{
	BlockedResultSchemaNotReady,
	BlockedRuntimeRowsMissing,
	BlockedReaderExecutionDeferred,
	BlockedComparisonDeferred,
	BlockedResultSelectionDeferred,
	BlockedContextAttachmentDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage Stage,
	FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus Status,
	int EqualityFieldCount,
	int RuntimeContextFieldCount,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind> OutputKinds,
	bool RequiresAcceptedJavaRows,
	bool RequiresAcceptedCSharpRows,
	bool RequiresProjectedValues,
	bool RequiresResultSchema,
	bool CanExecute,
	bool CanProjectValues,
	bool CanCompareValues,
	bool CanEmitResults,
	string RequiredProducer,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageRow> Stages,
	int EqualityFieldCount,
	int RuntimeContextFieldCount,
	bool HasImplementationRunbook,
	bool HasResultSchema,
	bool CanExecuteComparator,
	bool CanProjectValues,
	bool CanCompareValues,
	bool CanAttachRuntimeContext,
	bool CanEmitResults,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live comparator preflight for future
/// CM_FIND_GROUP action 2/6 value-reader execution. It maps runbook and result
/// schema metadata into executor stages, but it does not compare values.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract? implementationRunbook = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContract? resultSchema = null)
	{
		implementationRunbook ??= FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService.Create();
		resultSchema ??= FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService.Create(implementationRunbook);

		var status = resultSchema.Status == FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus.BlockedRunbookNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedResultSchemaNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedComparatorImplementationDeferred;
		var stages = new[]
		{
			StageRow(
				1,
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.RowIdentityPairing,
				StageStatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.RowIdentityPairing),
				resultSchema,
				[],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Future row matcher after accepted Java runtime trace rows and accepted live C# boundary rows exist.",
				"Pair by action/mutationKind/activePlayerObjectId/mutatedEntryObjectId before any value reader executes."),
			StageRow(
				2,
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.TypedReaderExecution,
				StageStatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.TypedReaderExecution),
				resultSchema,
				[],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Future value-reader executor after typed scalar, ordered-list, and enum/string readers are implemented.",
				"Reader execution must follow the implementation runbook and preserve ordered-list values before comparison."),
			StageRow(
				3,
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.EqualityValueComparison,
				StageStatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.EqualityValueComparison),
				resultSchema,
				[
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresProjectedValues: true,
				RequiresResultSchema: true,
				"Future comparator after every required Java/C# equality value is projected.",
				"Comparison must select Matched only when every equality value matches, otherwise select a FieldMismatch."),
			StageRow(
				4,
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.ResultSelection,
				StageStatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.ResultSelection),
				resultSchema,
				[
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresProjectedValues: true,
				RequiresResultSchema: true,
				"Future result selector after row identity and equality comparison have produced one deterministic outcome.",
				"Result selection cannot emit rows from preflight metadata and must wait for a real executor decision."),
			StageRow(
				5,
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.MismatchContextAttachment,
				StageStatusFor(status, FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.MismatchContextAttachment),
				resultSchema,
				[FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext],
				RequiresAcceptedJavaRows: false,
				RequiresAcceptedCSharpRows: false,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Future context attacher after MissingJavaRow, MissingCSharpRow, or FieldMismatch exists.",
				"Runtime context must never affect equality or enable a standalone result."),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract(
			status,
			stages,
			resultSchema.EqualityFieldCount,
			resultSchema.RuntimeContextFieldCount,
			HasImplementationRunbook: implementationRunbook.Steps.Count > 0,
			HasResultSchema: resultSchema.Rows.Count > 0,
			CanExecuteComparator: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanAttachRuntimeContext: false,
			CanEmitResults: false,
			DecisionFor(status),
			resultSchema.TraceName,
			resultSchema.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageRow StageRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage stage,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContract resultSchema,
		IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind> outputKinds,
		bool RequiresAcceptedJavaRows,
		bool RequiresAcceptedCSharpRows,
		bool RequiresProjectedValues,
		bool RequiresResultSchema,
		string requiredProducer,
		string notes) =>
		new(
			order,
			stage,
			status,
			resultSchema.EqualityFieldCount,
			resultSchema.RuntimeContextFieldCount,
			outputKinds,
			RequiresAcceptedJavaRows,
			RequiresAcceptedCSharpRows,
			RequiresProjectedValues,
			RequiresResultSchema,
			CanExecute: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			requiredProducer,
			$"resultSchemaStatus={resultSchema.Status}; equalityFields={resultSchema.EqualityFieldCount}; runtimeContextFields={resultSchema.RuntimeContextFieldCount}; canProjectValues={resultSchema.CanProjectValues}; canEmitResults={resultSchema.CanEmitMatched || resultSchema.CanEmitFieldMismatch || resultSchema.CanEmitMissingJavaRow || resultSchema.CanEmitMissingCSharpRow}",
			notes);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus StageStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage stage)
	{
		if (status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedResultSchemaNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedResultSchemaNotReady;

		return stage switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.RowIdentityPairing => FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedRuntimeRowsMissing,
			FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.TypedReaderExecution => FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedReaderExecutionDeferred,
			FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.EqualityValueComparison => FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedComparisonDeferred,
			FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.ResultSelection => FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedResultSelectionDeferred,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedContextAttachmentDeferred,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedResultSchemaNotReady => "Value-reader comparator preflight is blocked until result-schema metadata reaches deferred-comparator readiness.",
			_ => "Value-reader comparator preflight is staged, but row pairing, reader execution, value comparison, result selection, context attachment, and result emission remain intentionally deferred.",
		};
	}
}
