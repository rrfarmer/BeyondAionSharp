namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus
{
	BlockedMaterializationBlockerReportNotReady,
	BlockedResultEmissionGateNotReady,
	BlockedResultEmissionUnavailable,
}

public enum FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus
{
	BlockedMaterializationBlockerReportNotReady,
	BlockedResultEmissionGateNotReady,
	BlockedValueProjectionUnavailable,
	BlockedMissingRowDecisionUnavailable,
	BlockedContextAttachmentUnavailable,
	BlockedResultEmissionDisabled,
}

public sealed record FindGroupMutationPostProjectedValueResultEmissionBlockerRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonDryRunOutputKind OutputKind,
	FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus Status,
	FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus? MaterializationBlockerStatus,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus? EmissionGateStatus,
	IReadOnlyList<string> RequiredEmissionConditions,
	IReadOnlyList<string> RequiredSchemaFields,
	IReadOnlyList<string> RequiredProjectedFieldNames,
	bool RequiresMaterializedOutput,
	bool RequiresRuntimeComparison,
	bool RequiresParentResult,
	bool HasMaterializationBlockerRow,
	bool HasEmissionGateRow,
	bool CanMaterializeOutput,
	bool CanEmitResult,
	string BlockingEvidence,
	string RequiredEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedValueResultEmissionBlockerReport(
	FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedValueResultEmissionBlockerRow> Rows,
	int OutputKindCount,
	int EmittableOutputCount,
	bool HasMaterializationBlockerReport,
	bool HasResultEmissionGate,
	bool CanEmitMatched,
	bool CanEmitMissingJavaRow,
	bool CanEmitMissingCSharpRow,
	bool CanEmitFieldMismatch,
	bool CanEmitIgnoredRuntimeContext,
	bool CanEmitAnyResult,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live CM_FIND_GROUP action 2/6 projected-value
/// result-emission blocker. It joins projected-value materialization blockers
/// with result-emission gate rows so no future executor can emit comparison
/// results from unread values, missing row decisions, or unattached context.
/// </summary>
public static class FindGroupMutationPostProjectedValueResultEmissionBlockerReportService
{
	public static FindGroupMutationPostProjectedValueResultEmissionBlockerReport Create(
		FindGroupMutationPostProjectedValueMaterializationBlockerReport? materializationBlockers = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract? resultEmissionGate = null)
	{
		materializationBlockers ??= FindGroupMutationPostProjectedValueMaterializationBlockerReportService.Create();
		resultEmissionGate ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService.Create();

		var status = StatusFor(materializationBlockers, resultEmissionGate);
		var blockerRowsByOutputKind = materializationBlockers.Rows.ToDictionary(row => row.OutputKind);
		var gateRowsByOutputKind = resultEmissionGate.Rows.ToDictionary(row => row.OutputKind);
		var outputKinds = blockerRowsByOutputKind.Keys
			.Concat(gateRowsByOutputKind.Keys)
			.Distinct()
			.OrderBy(kind => OrderFor(kind, blockerRowsByOutputKind, gateRowsByOutputKind))
			.ToArray();
		var rows = outputKinds
			.Select(kind => BlockerRow(kind, status, blockerRowsByOutputKind, gateRowsByOutputKind))
			.ToArray();

		return new FindGroupMutationPostProjectedValueResultEmissionBlockerReport(
			status,
			rows,
			OutputKindCount: rows.Length,
			EmittableOutputCount: 0,
			HasMaterializationBlockerReport: materializationBlockers.Rows.Count > 0,
			HasResultEmissionGate: resultEmissionGate.Rows.Count > 0,
			CanEmitMatched: false,
			CanEmitMissingJavaRow: false,
			CanEmitMissingCSharpRow: false,
			CanEmitFieldMismatch: false,
			CanEmitIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			materializationBlockers.TraceName,
			materializationBlockers.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus StatusFor(
		FindGroupMutationPostProjectedValueMaterializationBlockerReport materializationBlockers,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract resultEmissionGate)
	{
		if (materializationBlockers.Status != FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread)
			return FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedMaterializationBlockerReportNotReady;

		if (resultEmissionGate.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady)
			return FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionGateNotReady;

		return FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable;
	}

	private static FindGroupMutationPostProjectedValueResultEmissionBlockerRow BlockerRow(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus reportStatus,
		IReadOnlyDictionary<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind, FindGroupMutationPostProjectedValueMaterializationBlockerRow> blockerRowsByOutputKind,
		IReadOnlyDictionary<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow> gateRowsByOutputKind)
	{
		blockerRowsByOutputKind.TryGetValue(outputKind, out var blockerRow);
		gateRowsByOutputKind.TryGetValue(outputKind, out var gateRow);
		var materializationBlockerEvidence = blockerRow?.BlockingEvidence ?? "none";

		return new FindGroupMutationPostProjectedValueResultEmissionBlockerRow(
			OrderFor(outputKind, blockerRowsByOutputKind, gateRowsByOutputKind),
			outputKind,
			RowStatusFor(outputKind, reportStatus, gateRow),
			blockerRow?.Status,
			gateRow?.Status,
			gateRow?.RequiredEmissionConditions ?? [],
			gateRow?.RequiredSchemaFields ?? blockerRow?.RequiredSchemaFields ?? [],
			blockerRow?.RequiredProjectedFieldNames ?? [],
			RequiresMaterializedOutput: gateRow?.RequiresMaterializedOutput ?? outputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			RequiresRuntimeComparison: gateRow?.RequiresRuntimeComparison ?? true,
			RequiresParentResult: gateRow?.RequiresParentResult ?? outputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			HasMaterializationBlockerRow: blockerRow != null,
			HasEmissionGateRow: gateRow != null,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			$"reportStatus={reportStatus}; materializationBlockerStatus={blockerRow?.Status}; emissionGateStatus={gateRow?.Status}; hasMaterializationBlockerRow={blockerRow != null}; hasEmissionGateRow={gateRow != null}; gateCanEmitResult={gateRow?.CanEmitResult}; materializationBlockerEvidence={materializationBlockerEvidence}",
			RequiredEvidenceFor(outputKind, blockerRow, gateRow),
			NotesFor(outputKind, gateRow));
	}

	private static FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus reportStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow? gateRow)
	{
		if (reportStatus == FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedMaterializationBlockerReportNotReady)
			return FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedMaterializationBlockerReportNotReady;

		if (reportStatus == FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionGateNotReady)
			return FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedResultEmissionGateNotReady;

		if (gateRow?.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedResultEmissionDisabled)
			return FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedResultEmissionDisabled;

		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedMissingRowDecisionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedMissingRowDecisionUnavailable,
			_ => FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedContextAttachmentUnavailable,
		};
	}

	private static string RequiredEvidenceFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedValueMaterializationBlockerRow? blockerRow,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow? gateRow)
	{
		var gateConditions = gateRow == null ? "no result-emission gate row" : string.Join(" ", gateRow.RequiredEmissionConditions);
		var blockerEvidence = blockerRow?.RequiredEvidence ?? "no materialization blocker row";

		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => $"Matched emission requires materialized matched output, projected Java/C# equality values, all values equal, runtime comparison evidence, and no ignored runtime context. Blocker: {blockerEvidence} Gate: {gateConditions}",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => $"FieldMismatch emission requires materialized mismatch output, concrete field name, difference kind, Java value, C# value, optional diagnostic context, and runtime comparison evidence. Blocker: {blockerEvidence} Gate: {gateConditions}",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => $"MissingJavaRow emission requires a materialized C#-only output after row identity matching proves no runtime-backed Java row exists, plus runtime comparison evidence. Blocker: {blockerEvidence} Gate: {gateConditions}",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => $"MissingCSharpRow emission requires a materialized Java-only output after row identity matching proves no accepted live C# row exists, plus runtime comparison evidence. Blocker: {blockerEvidence} Gate: {gateConditions}",
			_ => $"IgnoredRuntimeContext requires an emitted MissingJavaRow, MissingCSharpRow, or FieldMismatch parent result and must not emit as a standalone result. Blocker: {blockerEvidence} Gate: {gateConditions}",
		};
	}

	private static string NotesFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow? gateRow)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "Java action 2/6 matched parity must not emit while projected values are placeholders or runtime comparison is missing.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "Mismatch output must not emit until a real differing field and both projected values exist.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "MissingJavaRow output must not emit until row identity matching proves Java absence from runtime artifacts.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "MissingCSharpRow output must not emit until accepted live C# boundary rows prove C# absence.",
			_ => gateRow?.Notes ?? "Runtime context remains diagnostic attachment data and is never a standalone result.",
		};
	}

	private static int OrderFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		IReadOnlyDictionary<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind, FindGroupMutationPostProjectedValueMaterializationBlockerRow> blockerRowsByOutputKind,
		IReadOnlyDictionary<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow> gateRowsByOutputKind)
	{
		if (blockerRowsByOutputKind.TryGetValue(outputKind, out var blockerRow))
			return blockerRow.Order;

		return gateRowsByOutputKind.TryGetValue(outputKind, out var gateRow) ? gateRow.Order : int.MaxValue;
	}

	private static string DecisionFor(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedMaterializationBlockerReportNotReady => "Projected-value result-emission blocker report is blocked until the materialization blocker report reaches unread projected-value readiness.",
			FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionGateNotReady => "Projected-value result-emission blocker report is blocked until the result-emission gate has materialization preflight metadata.",
			_ => "Projected-value result-emission blocker report is defined, but materialization, result emission, runtime comparison, and verified parity remain unavailable.",
		};
	}
}
