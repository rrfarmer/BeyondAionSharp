namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus
{
	BlockedExecutorSkeletonNotReady,
	BlockedMissingValueSources,
	BlockedValueComparisonUnavailable,
}

public enum FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus
{
	UnavailableExecutorSkeletonBlocked,
	UnavailableMissingValueSources,
	UnavailableValueProjectionDeferred,
	UnavailableRuntimeContextOnly,
}

public sealed record FindGroupMutationPostProjectedRowComparisonBlockedResultRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonDryRunOutputKind OutputKind,
	FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus Status,
	bool CanEmitResult,
	string RequiredInput,
	string Blocker,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonBlockedResultReport(
	FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonBlockedResultRow> Rows,
	bool HasExecutorSkeleton,
	bool HasValueContract,
	bool HasAllPairedInputs,
	bool CanEmitMatched,
	bool CanEmitMissingJavaRow,
	bool CanEmitMissingCSharpRow,
	bool CanEmitFieldMismatch,
	bool CanEmitIgnoredRuntimeContext,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: final non-live blocked-result report for future
/// CM_FIND_GROUP action 2/6 projected-row comparison. It explains why planned
/// outputs remain unavailable, but it never emits comparison results.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonBlockedResultReportService
{
	public static FindGroupMutationPostProjectedRowComparisonBlockedResultReport Create(
		FindGroupMutationPostProjectedRowComparisonExecutorSkeleton? executorSkeleton = null,
		FindGroupMutationPostProjectedRowComparisonValueContract? valueContract = null)
	{
		executorSkeleton ??= FindGroupMutationPostProjectedRowComparisonExecutorSkeletonService.Create();
		valueContract ??= FindGroupMutationPostProjectedRowComparisonValueContractService.Create(executorSkeleton);

		var status = DetermineStatus(valueContract);
		var rows = new[]
		{
			CreateRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched, status, valueContract),
			CreateRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow, status, valueContract),
			CreateRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow, status, valueContract),
			CreateRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch, status, valueContract),
			CreateRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext, status, valueContract),
		};

		return new FindGroupMutationPostProjectedRowComparisonBlockedResultReport(
			status,
			rows,
			executorSkeleton.Rows.Count > 0,
			valueContract.Fields.Count > 0,
			valueContract.HasAllPairedInputs,
			CanEmitMatched: false,
			CanEmitMissingJavaRow: false,
			CanEmitMissingCSharpRow: false,
			CanEmitFieldMismatch: false,
			CanEmitIgnoredRuntimeContext: false,
			DecisionFor(status),
			valueContract.TraceName,
			valueContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus DetermineStatus(
		FindGroupMutationPostProjectedRowComparisonValueContract valueContract)
	{
		return valueContract.Status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueContractStatus.BlockedExecutorSkeletonNotReady => FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedExecutorSkeletonNotReady,
			FindGroupMutationPostProjectedRowComparisonValueContractStatus.BlockedMissingValueSources => FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedMissingValueSources,
			_ => FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedValueComparisonUnavailable,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonBlockedResultRow CreateRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus reportStatus,
		FindGroupMutationPostProjectedRowComparisonValueContract valueContract)
	{
		var rowStatus = outputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			? FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableRuntimeContextOnly
			: RowStatusFor(reportStatus);

		return new FindGroupMutationPostProjectedRowComparisonBlockedResultRow(
			order,
			outputKind,
			rowStatus,
			CanEmitResult: false,
			RequiredInputFor(outputKind),
			BlockerFor(outputKind, reportStatus),
			EvidenceFor(outputKind, valueContract),
			NotesFor(outputKind));
	}

	private static FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus reportStatus)
	{
		return reportStatus switch
		{
			FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedExecutorSkeletonNotReady => FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableExecutorSkeletonBlocked,
			FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedMissingValueSources => FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableMissingValueSources,
			_ => FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableValueProjectionDeferred,
		};
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedExecutorSkeletonNotReady => "No projected-row comparison results emitted because the executor skeleton is not ready.",
			FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedMissingValueSources => "No projected-row comparison results emitted because Java/C# value sources are incomplete.",
			_ => "No projected-row comparison results emitted because value projection and comparison are still deferred.",
		};
	}

	private static string RequiredInputFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "Projected Java and C# equality values for every required field, with no mismatches.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "A real executor decision that a C# row exists without a matching Java row.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "A real executor decision that a Java row exists without a matching C# row.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "Projected Java and C# equality values plus the first differing field.",
			_ => "A real mismatch result that needs runtime-only context.",
		};
	}

	private static string BlockerFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus status)
	{
		if (outputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext)
			return "Runtime context rows are unavailable until a real missing-row or field-mismatch result is emitted.";

		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedExecutorSkeletonNotReady => "Executor skeleton is blocked before result selection.",
			FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedMissingValueSources => "Required Java/C# value sources are missing before result selection.",
			_ => "Value projection is deferred; future executor must compare Java/C# values before result selection.",
		};
	}

	private static string EvidenceFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedRowComparisonValueContract valueContract)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => $"canEmitMatched={valueContract.CanEmitMatched}; equalityFields={valueContract.EqualityProjectionFields.Count}",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => $"canEmitFieldMismatch={valueContract.CanEmitFieldMismatch}; equalityFields={valueContract.EqualityProjectionFields.Count}",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => $"hasAllPairedInputs={valueContract.HasAllPairedInputs}; canProjectValues={valueContract.CanProjectValues}",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => $"hasAllPairedInputs={valueContract.HasAllPairedInputs}; canProjectValues={valueContract.CanProjectValues}",
			_ => $"ignoredRuntimeFields={string.Join("/", valueContract.IgnoredRuntimeFields)}",
		};
	}

	private static string NotesFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "Matched output is unavailable until all Java/C# equality values are projected and found equal.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "FieldMismatch output is unavailable until Java/C# values are projected and a difference is selected.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "MissingJavaRow output is unavailable until a real executor inspects row keys.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "MissingCSharpRow output is unavailable until a real executor inspects row keys.",
			_ => "Ignored runtime context is unavailable until attached to a real comparison result.",
		};
	}
}
