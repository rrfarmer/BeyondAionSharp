namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus
{
	BlockedProjectedValueRowsNotReady,
	BlockedMaterializationPreflightNotReady,
	BlockedProjectedValuesUnread,
}

public enum FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus
{
	BlockedProjectedValueRowsNotReady,
	BlockedMaterializationPreflightNotReady,
	BlockedValueProjectionUnavailable,
	BlockedMissingRowDecisionUnavailable,
	BlockedContextAttachmentUnavailable,
}

public sealed record FindGroupMutationPostProjectedValueMaterializationBlockerRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonDryRunOutputKind OutputKind,
	FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus Status,
	int RequiredEqualityFieldCount,
	int UnreadEqualityFieldCount,
	int IgnoredRuntimeContextFieldCount,
	IReadOnlyList<string> RequiredSchemaFields,
	IReadOnlyList<string> RequiredProjectedFieldNames,
	bool RequiresProjectedValues,
	bool RequiresMissingRowDecision,
	bool AllowsRuntimeContextAttachment,
	bool HasProjectedValueRows,
	bool HasMaterializationPreflightRow,
	bool HasUnreadProjectedValues,
	bool CanMaterializeOutput,
	bool CanEmitResult,
	string BlockingEvidence,
	string RequiredEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedValueMaterializationBlockerReport(
	FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedValueMaterializationBlockerRow> Rows,
	int OutputKindCount,
	int RequiredEqualityFieldCount,
	int UnreadEqualityFieldCount,
	int IgnoredRuntimeContextFieldCount,
	bool HasProjectedValueRows,
	bool HasMaterializationPreflight,
	bool CanMaterializeMatched,
	bool CanMaterializeMissingJavaRow,
	bool CanMaterializeMissingCSharpRow,
	bool CanMaterializeFieldMismatch,
	bool CanAttachIgnoredRuntimeContext,
	bool CanEmitAnyResult,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live blocker report that joins CM_FIND_GROUP
/// action 2/6 projected-value row shape with output materialization preflight.
/// It explains why result rows cannot materialize while every Java/C# value is
/// still unread.
/// </summary>
public static class FindGroupMutationPostProjectedValueMaterializationBlockerReportService
{
	public static FindGroupMutationPostProjectedValueMaterializationBlockerReport Create(
		FindGroupMutationPostValueReaderProjectedValueRowContract? projectedValueRows = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract? materializationPreflight = null)
	{
		projectedValueRows ??= FindGroupMutationPostValueReaderProjectedValueRowContractService.Create();
		materializationPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService.Create();

		var status = StatusFor(projectedValueRows, materializationPreflight);
		var equalityRows = projectedValueRows.Rows
			.Where(row => row.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue)
			.ToArray();
		var contextRows = projectedValueRows.Rows
			.Where(row => row.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext)
			.ToArray();
		var unreadEqualityCount = equalityRows.Count(row =>
			row.JavaReadStatus == FindGroupMutationPostValueReaderProjectedValueReadStatus.NotRead
			|| row.CSharpReadStatus == FindGroupMutationPostValueReaderProjectedValueReadStatus.NotRead);
		var rows = materializationPreflight.Rows
			.Select(row => BlockerRow(row, status, projectedValueRows, equalityRows, contextRows, unreadEqualityCount))
			.ToArray();

		return new FindGroupMutationPostProjectedValueMaterializationBlockerReport(
			status,
			rows,
			OutputKindCount: rows.Length,
			RequiredEqualityFieldCount: equalityRows.Length,
			UnreadEqualityFieldCount: unreadEqualityCount,
			IgnoredRuntimeContextFieldCount: contextRows.Length,
			HasProjectedValueRows: projectedValueRows.Rows.Count > 0,
			HasMaterializationPreflight: materializationPreflight.Rows.Count > 0,
			CanMaterializeMatched: false,
			CanMaterializeMissingJavaRow: false,
			CanMaterializeMissingCSharpRow: false,
			CanMaterializeFieldMismatch: false,
			CanAttachIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			projectedValueRows.TraceName,
			projectedValueRows.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus StatusFor(
		FindGroupMutationPostValueReaderProjectedValueRowContract projectedValueRows,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract materializationPreflight)
	{
		if (projectedValueRows.Status != FindGroupMutationPostValueReaderProjectedValueRowContractStatus.ReadyForProjectedRowsBlocked)
			return FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValueRowsNotReady;

		if (materializationPreflight.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedIntakeNotReady)
			return FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedMaterializationPreflightNotReady;

		return FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread;
	}

	private static FindGroupMutationPostProjectedValueMaterializationBlockerRow BlockerRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRow preflightRow,
		FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus reportStatus,
		FindGroupMutationPostValueReaderProjectedValueRowContract projectedValueRows,
		IReadOnlyList<FindGroupMutationPostValueReaderProjectedValueRow> equalityRows,
		IReadOnlyList<FindGroupMutationPostValueReaderProjectedValueRow> contextRows,
		int unreadEqualityCount)
	{
		var requiresProjectedValues = preflightRow.OutputKind is FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched or FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch;
		var requiresMissingRowDecision = preflightRow.OutputKind is FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow or FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow;
		var allowsRuntimeContext = preflightRow.OutputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched;
		var requiredFields = requiresProjectedValues
			? equalityRows.Select(row => row.FieldName).Distinct(StringComparer.Ordinal).ToArray()
			: [];
		var projectedValueRowEvidence = projectedValueRows.Rows.Count == 0
			? "none"
			: string.Join(" | ", projectedValueRows.Rows.Select(row => $"{row.FieldName}={row.Evidence}"));

		return new FindGroupMutationPostProjectedValueMaterializationBlockerRow(
			preflightRow.Order,
			preflightRow.OutputKind,
			RowStatusFor(preflightRow.OutputKind, reportStatus),
			equalityRows.Count,
			unreadEqualityCount,
			contextRows.Count,
			preflightRow.RequiredSchemaFields,
			requiredFields,
			requiresProjectedValues,
			requiresMissingRowDecision,
			allowsRuntimeContext,
			HasProjectedValueRows: projectedValueRows.Rows.Count > 0,
			HasMaterializationPreflightRow: true,
			HasUnreadProjectedValues: unreadEqualityCount > 0,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			$"reportStatus={reportStatus}; projectedValueStatus={projectedValueRows.Status}; materializationPreflightStatus={preflightRow.Status}; unreadEqualityFields={unreadEqualityCount}; ignoredRuntimeContextFields={contextRows.Count}; canMaterializePreflightRow={preflightRow.CanMaterializeOutput}; canEmitPreflightRow={preflightRow.CanEmitResult}; projectedValueRows={projectedValueRowEvidence}",
			RequiredEvidenceFor(preflightRow.OutputKind, unreadEqualityCount),
			NotesFor(preflightRow.OutputKind));
	}

	private static FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus reportStatus)
	{
		if (reportStatus == FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValueRowsNotReady)
			return FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedProjectedValueRowsNotReady;

		if (reportStatus == FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedMaterializationPreflightNotReady)
			return FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMaterializationPreflightNotReady;

		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMissingRowDecisionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMissingRowDecisionUnavailable,
			_ => FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedContextAttachmentUnavailable,
		};
	}

	private static string RequiredEvidenceFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		int unreadEqualityCount)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => $"Matched materialization requires {unreadEqualityCount} unread equality fields to have Java and C# values, all values equal, and runtime comparison evidence.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => $"FieldMismatch materialization requires {unreadEqualityCount} unread equality fields to have Java and C# values so a concrete differing field can be selected.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "MissingJavaRow materialization requires row identity matching to prove an accepted live C# row has no matching runtime-backed Java row.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "MissingCSharpRow materialization requires row identity matching to prove a runtime-backed Java row has no matching accepted live C# row.",
			_ => "IgnoredRuntimeContext can attach only after MissingJavaRow, MissingCSharpRow, or FieldMismatch materializes; it is not a standalone output.",
		};
	}

	private static string NotesFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "Java action 2/6 parity must not materialize Matched from placeholder values.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "Mismatch output must name a real differing field, Java value, and C# value, not the unread placeholders.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "MissingJavaRow is decided by row identity matching, not by projected-value row placeholders.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "MissingCSharpRow is decided by row identity matching, not by projected-value row placeholders.",
			_ => "Runtime context remains diagnostic and may attach only to a parent missing-row or mismatch output.",
		};
	}

	private static string DecisionFor(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValueRowsNotReady => "Projected-value materialization blocker report is blocked until projected-value rows reach unread-row readiness.",
			FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedMaterializationPreflightNotReady => "Projected-value materialization blocker report is blocked until materialization preflight reaches runtime-evidence readiness.",
			_ => "Projected-value materialization blocker report is defined, but unread projected values, missing row decisions, context attachment, result emission, runtime comparison, and verified parity remain blocked.",
		};
	}
}
