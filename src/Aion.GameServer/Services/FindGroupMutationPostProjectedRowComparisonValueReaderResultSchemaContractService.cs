namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus
{
	BlockedRunbookNotReady,
	BlockedResultSchemaDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus
{
	BlockedRunbookNotReady,
	BlockedValueProjectionDeferred,
	BlockedMissingRowDecisionDeferred,
	BlockedContextAttachmentDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonDryRunOutputKind OutputKind,
	FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus Status,
	IReadOnlyList<string> SchemaFields,
	bool RequiresProjectedValues,
	bool RequiresMissingRowDecision,
	bool AllowsRuntimeContextAttachment,
	bool CanEmitResult,
	string RequiredProducer,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow> Rows,
	IReadOnlyList<FindGroupMutationPostComparisonDifferenceKind> DifferenceKinds,
	int EqualityFieldCount,
	int RuntimeContextFieldCount,
	bool HasImplementationRunbook,
	bool HasResultContract,
	bool CanProjectValues,
	bool CanAttachRuntimeContext,
	bool CanEmitMatched,
	bool CanEmitFieldMismatch,
	bool CanEmitMissingJavaRow,
	bool CanEmitMissingCSharpRow,
	bool CanEmitIgnoredRuntimeContext,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live value-reader result schema for future
/// CM_FIND_GROUP action 2/6 projected-row comparison outputs. It defines row
/// shapes but does not project values, attach context, or emit results.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract? implementationRunbook = null,
		FindGroupMutationPostComparisonExecutionResultContract? resultContract = null)
	{
		implementationRunbook ??= FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService.Create();
		resultContract ??= FindGroupMutationPostComparisonExecutionResultContractService.Create();

		var status = implementationRunbook.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReadinessSummaryNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus.BlockedRunbookNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus.BlockedResultSchemaDeferred;
		var rows = new[]
		{
			CreateMatchedRow(1, status, resultContract),
			CreateMissingJavaRow(2, status, resultContract),
			CreateMissingCSharpRow(3, status, resultContract),
			CreateFieldMismatchRow(4, status, resultContract),
			CreateIgnoredRuntimeContextRow(5, status, resultContract),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContract(
			status,
			rows,
			resultContract.DifferenceKinds,
			EqualityFieldCount: resultContract.Fields.Count(field => field.Status == FindGroupMutationPostComparisonDifferenceFieldStatus.RequiredForDifferenceReport),
			RuntimeContextFieldCount: resultContract.Fields.Count(field => field.Status == FindGroupMutationPostComparisonDifferenceFieldStatus.IgnoredForEquality),
			HasImplementationRunbook: implementationRunbook.Steps.Count > 0,
			HasResultContract: resultContract.Fields.Count > 0,
			CanProjectValues: false,
			CanAttachRuntimeContext: false,
			CanEmitMatched: false,
			CanEmitFieldMismatch: false,
			CanEmitMissingJavaRow: false,
			CanEmitMissingCSharpRow: false,
			CanEmitIgnoredRuntimeContext: false,
			DecisionFor(status),
			resultContract.TraceName,
			resultContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow CreateMatchedRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus schemaStatus,
		FindGroupMutationPostComparisonExecutionResultContract resultContract) =>
		CreateRow(
			order,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
			RowStatusFor(schemaStatus, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched),
			["action", "mutationKind", "rowIdentity", "matchedFields", "matchedFieldCount"],
			requiresProjectedValues: true,
			requiresMissingRowDecision: false,
			allowsRuntimeContextAttachment: false,
			"Future value-reader executor after every equality field is projected and equal.",
			$"readyForComparisonExecution={resultContract.ReadyForComparisonExecution}; equalityFields={resultContract.EqualityProjectionFields.Count}",
			"Matched rows cannot include ignored runtime context and cannot be emitted until every required Java/C# equality value is projected and equal.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow CreateMissingJavaRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus schemaStatus,
		FindGroupMutationPostComparisonExecutionResultContract resultContract) =>
		CreateRow(
			order,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
			RowStatusFor(schemaStatus, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow),
			["action", "mutationKind", "rowIdentity", "csharpRowReference", "runtimeContext"],
			requiresProjectedValues: false,
			requiresMissingRowDecision: true,
			allowsRuntimeContextAttachment: true,
			"Future row-identity matcher after accepted live C# row exists without a matching Java row.",
			$"requiresGeneratedJavaTraceRows={resultContract.RequiresGeneratedJavaTraceRows}; requiresLiveCSharpTraceRows={resultContract.RequiresLiveCSharpTraceRows}",
			"MissingJavaRow may carry ignored runtime context after the missing-row decision exists; context must not create the decision.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow CreateMissingCSharpRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus schemaStatus,
		FindGroupMutationPostComparisonExecutionResultContract resultContract) =>
		CreateRow(
			order,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
			RowStatusFor(schemaStatus, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow),
			["action", "mutationKind", "rowIdentity", "javaRowReference", "runtimeContext"],
			requiresProjectedValues: false,
			requiresMissingRowDecision: true,
			allowsRuntimeContextAttachment: true,
			"Future row-identity matcher after accepted Java row exists without a matching C# row.",
			$"requiresGeneratedJavaTraceRows={resultContract.RequiresGeneratedJavaTraceRows}; requiresLiveCSharpTraceRows={resultContract.RequiresLiveCSharpTraceRows}",
			"MissingCSharpRow may carry ignored runtime context after the missing-row decision exists; context must not create the decision.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow CreateFieldMismatchRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus schemaStatus,
		FindGroupMutationPostComparisonExecutionResultContract resultContract) =>
		CreateRow(
			order,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
			RowStatusFor(schemaStatus, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch),
			["action", "mutationKind", "rowIdentity", "fieldName", "differenceKind", "javaValue", "csharpValue", "javaSource", "runtimeContext"],
			requiresProjectedValues: true,
			requiresMissingRowDecision: false,
			allowsRuntimeContextAttachment: true,
			"Future value-reader executor after one projected Java/C# equality field differs.",
			$"differenceKinds={string.Join("/", resultContract.DifferenceKinds)}; equalityFields={resultContract.EqualityProjectionFields.Count}",
			"FieldMismatch may attach ignored runtime context only after the mismatched field, Java value, and C# value are selected.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow CreateIgnoredRuntimeContextRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus schemaStatus,
		FindGroupMutationPostComparisonExecutionResultContract resultContract) =>
		CreateRow(
			order,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			RowStatusFor(schemaStatus, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext),
			["traceSource", "serverEpochSeconds"],
			requiresProjectedValues: false,
			requiresMissingRowDecision: false,
			allowsRuntimeContextAttachment: true,
			"Future mismatch-context attachment after MissingJavaRow, MissingCSharpRow, or FieldMismatch exists.",
			$"ignoredRuntimeFields={string.Join("/", resultContract.IgnoredRuntimeFields)}",
			"IgnoredRuntimeContext is not a standalone comparison result and must never enable Matched output.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow CreateRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus status,
		IReadOnlyList<string> schemaFields,
		bool requiresProjectedValues,
		bool requiresMissingRowDecision,
		bool allowsRuntimeContextAttachment,
		string requiredProducer,
		string evidence,
		string notes) =>
		new(
			order,
			outputKind,
			status,
			schemaFields,
			requiresProjectedValues,
			requiresMissingRowDecision,
			allowsRuntimeContextAttachment,
			CanEmitResult: false,
			requiredProducer,
			evidence,
			notes);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus schemaStatus,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		if (schemaStatus == FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus.BlockedRunbookNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedRunbookNotReady;

		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedMissingRowDecisionDeferred,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedMissingRowDecisionDeferred,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext => FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedContextAttachmentDeferred,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedValueProjectionDeferred,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus.BlockedRunbookNotReady => "Value-reader result schema is blocked until implementation runbook metadata reaches deferred-result readiness.",
			_ => "Value-reader result schema is defined, but projected values, missing-row decisions, context attachment, and result emission remain intentionally deferred.",
		};
	}
}
