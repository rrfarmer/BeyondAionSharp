namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus
{
	BlockedMaterializationPreflightNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedResultEmissionDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus
{
	BlockedMaterializationPreflightNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedMissingRowDecisionUnavailable,
	BlockedValueProjectionUnavailable,
	BlockedContextAttachmentUnavailable,
	BlockedRuntimeComparisonMissing,
	BlockedResultEmissionDisabled,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonDryRunOutputKind OutputKind,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus Status,
	IReadOnlyList<string> RequiredEmissionConditions,
	IReadOnlyList<string> RequiredSchemaFields,
	bool HasMaterializationPreflightRow,
	bool RequiresMaterializedOutput,
	bool RequiresRuntimeComparison,
	bool RequiresParentResult,
	bool CanEmitResult,
	string BlockingEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow> Rows,
	int OutputKindCount,
	int EmittableOutputCount,
	bool HasMaterializationPreflight,
	bool HasAnyRuntimeEvidence,
	bool CanEmitMatched,
	bool CanEmitMissingJavaRow,
	bool CanEmitMissingCSharpRow,
	bool CanEmitFieldMismatch,
	bool CanEmitIgnoredRuntimeContext,
	bool CanEmitAnyResult,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live result emission gate for future CM_FIND_GROUP
/// action 2/6 value-reader executor outputs. It records the final conditions a
/// materialized result must satisfy before emission, but it never emits rows.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract? materializationPreflight = null)
	{
		materializationPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService.Create();

		var status = StatusFor(materializationPreflight);
		var rows = materializationPreflight.Rows
			.Select(row => GateRow(row, status))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract(
			status,
			rows,
			OutputKindCount: rows.Length,
			EmittableOutputCount: 0,
			HasMaterializationPreflight: materializationPreflight.Rows.Count > 0,
			HasAnyRuntimeEvidence: false,
			CanEmitMatched: false,
			CanEmitMissingJavaRow: false,
			CanEmitMissingCSharpRow: false,
			CanEmitFieldMismatch: false,
			CanEmitIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			materializationPreflight.TraceName,
			materializationPreflight.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract materializationPreflight)
	{
		return materializationPreflight.Status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedIntakeNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedResultEmissionDeferred,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow GateRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRow preflightRow,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus gateStatus)
	{
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow(
			preflightRow.Order,
			preflightRow.OutputKind,
			RowStatusFor(preflightRow, gateStatus),
			ConditionsFor(preflightRow.OutputKind),
			preflightRow.RequiredSchemaFields,
			HasMaterializationPreflightRow: true,
			RequiresMaterializedOutput: preflightRow.OutputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			RequiresRuntimeComparison: true,
			RequiresParentResult: preflightRow.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			CanEmitResult: false,
			$"gateStatus={gateStatus}; preflightStatus={preflightRow.Status}; canMaterializeOutput={preflightRow.CanMaterializeOutput}; canEmitPreflightResult={preflightRow.CanEmitResult}; requiredSchemaFields={string.Join(",", preflightRow.RequiredSchemaFields)}",
			NotesFor(preflightRow.OutputKind));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRow preflightRow,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus gateStatus)
	{
		if (gateStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMaterializationPreflightNotReady;

		if (gateStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedResultEmissionDeferred)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedResultEmissionDisabled;

		return preflightRow.OutputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMissingRowDecisionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMissingRowDecisionUnavailable,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedContextAttachmentUnavailable,
		};
	}

	private static IReadOnlyList<string> ConditionsFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => [
				"Materialized matched row exists after Java/C# row identity pairing.",
				"Every schema equality field has projected Java and C# values.",
				"All projected equality values compare equal.",
				"Runtime comparison evidence exists for action 2 and action 6.",
				"No ignored runtime context is attached to Matched output.",
			],
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => [
				"Materialized MissingJavaRow exists after accepted live C# row has no matching runtime-backed Java row.",
				"Missing-row decision is produced by row identity matching, not by absent metadata.",
				"Runtime context may attach only after the missing-row decision exists.",
				"Runtime comparison evidence exists for action 2 and action 6.",
			],
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => [
				"Materialized MissingCSharpRow exists after runtime-backed Java row has no matching accepted live C# row.",
				"Missing-row decision is produced by row identity matching, not by absent metadata.",
				"Runtime context may attach only after the missing-row decision exists.",
				"Runtime comparison evidence exists for action 2 and action 6.",
			],
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => [
				"Materialized FieldMismatch exists after paired runtime rows have projected Java/C# values.",
				"Differing field name, difference kind, Java value, and C# value are selected.",
				"Runtime context may attach only after the field mismatch exists.",
				"Runtime comparison evidence exists for action 2 and action 6.",
			],
			_ => [
				"IgnoredRuntimeContext is attached to an emitted MissingJavaRow, MissingCSharpRow, or FieldMismatch result.",
				"traceSource and serverEpochSeconds are available as diagnostics only.",
				"IgnoredRuntimeContext is not emitted as an independent result row.",
				"Runtime comparison evidence exists for action 2 and action 6.",
			],
		};
	}

	private static string NotesFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "Matched emission must wait for real Java/C# value reads and equality comparison.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "MissingJavaRow emission must wait for runtime-backed proof that Java did not produce the row.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "MissingCSharpRow emission must wait for live C# boundary proof that C# did not produce the row.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "FieldMismatch emission must wait for a concrete field-level difference from projected Java/C# values.",
			_ => "IgnoredRuntimeContext remains diagnostic attachment data and never creates a standalone result.",
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady => "Value-reader executor result emission gate is blocked until materialization preflight is ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing => "Value-reader executor result emission gate is blocked because runtime evidence, row decisions, value projection, and context attachment are missing.",
			_ => "Value-reader executor result emission gate is defined, but result emission remains disabled until runtime comparison evidence exists.",
		};
	}
}
