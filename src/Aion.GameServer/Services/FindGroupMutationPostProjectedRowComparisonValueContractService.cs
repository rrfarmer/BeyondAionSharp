namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueContractStatus
{
	BlockedExecutorSkeletonNotReady,
	BlockedMissingValueSources,
	ReadyForFutureValueProjectionButDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueSourceStatus
{
	RequiredEqualityValueSource,
	IgnoredRuntimeContextValue,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueField(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostComparisonDifferenceKind DifferenceKind,
	FindGroupMutationPostProjectedRowComparisonValueSourceStatus Status,
	bool RequiresJavaValue,
	bool RequiresCSharpValue,
	bool CanEmitMatched,
	bool CanEmitFieldMismatch,
	string JavaValueSource,
	string CSharpValueSource,
	string Blocker,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueContract(
	FindGroupMutationPostProjectedRowComparisonValueContractStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueField> Fields,
	IReadOnlyList<string> EqualityProjectionFields,
	IReadOnlyList<string> IgnoredRuntimeFields,
	bool HasExecutorSkeleton,
	bool HasResultContract,
	bool HasAllPairedInputs,
	bool CanProjectValues,
	bool CanEmitMatched,
	bool CanEmitFieldMismatch,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live value-source contract for future CM_FIND_GROUP
/// action 2/6 projected-row comparison. This names Java/C# value sources and
/// equality fields, but it never reads values or emits matched/mismatch results.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueContract Create(
		FindGroupMutationPostProjectedRowComparisonExecutorSkeleton? executorSkeleton = null,
		FindGroupMutationPostComparisonExecutionResultContract? resultContract = null)
	{
		executorSkeleton ??= FindGroupMutationPostProjectedRowComparisonExecutorSkeletonService.Create();
		resultContract ??= FindGroupMutationPostComparisonExecutionResultContractService.Create();

		var fields = resultContract.Fields
			.Select((field, index) => CreateField(index + 1, field, executorSkeleton))
			.ToArray();
		var status = DetermineStatus(executorSkeleton);

		return new FindGroupMutationPostProjectedRowComparisonValueContract(
			status,
			fields,
			resultContract.EqualityProjectionFields,
			resultContract.IgnoredRuntimeFields,
			HasExecutorSkeleton: executorSkeleton.Rows.Count > 0,
			HasResultContract: resultContract.Fields.Count > 0,
			executorSkeleton.HasAllPairedInputs,
			CanProjectValues: false,
			CanEmitMatched: false,
			CanEmitFieldMismatch: false,
			status == FindGroupMutationPostProjectedRowComparisonValueContractStatus.ReadyForFutureValueProjectionButDeferred
				? "Paired row inputs exist, but Java/C# field values are not projected or compared by this contract."
				: "Value projection is blocked because executor skeleton readiness or value sources are incomplete.",
			resultContract.TraceName,
			resultContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueContractStatus DetermineStatus(
		FindGroupMutationPostProjectedRowComparisonExecutorSkeleton executorSkeleton)
	{
		if (executorSkeleton.Status == FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.BlockedDryRunNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueContractStatus.BlockedExecutorSkeletonNotReady;

		if (!executorSkeleton.HasAllPairedInputs)
			return FindGroupMutationPostProjectedRowComparisonValueContractStatus.BlockedMissingValueSources;

		return FindGroupMutationPostProjectedRowComparisonValueContractStatus.ReadyForFutureValueProjectionButDeferred;
	}

	private static FindGroupMutationPostProjectedRowComparisonValueField CreateField(
		int order,
		FindGroupMutationPostComparisonExecutionResultFieldContract field,
		FindGroupMutationPostProjectedRowComparisonExecutorSkeleton executorSkeleton)
	{
		var status = field.Status == FindGroupMutationPostComparisonDifferenceFieldStatus.IgnoredForEquality
			? FindGroupMutationPostProjectedRowComparisonValueSourceStatus.IgnoredRuntimeContextValue
			: FindGroupMutationPostProjectedRowComparisonValueSourceStatus.RequiredEqualityValueSource;
		var row = executorSkeleton.Rows.SingleOrDefault(item => item.Action == field.Action && item.MutationKind == field.MutationKind);
		var hasPair = row is { HasAcceptedJavaRow: true, HasAcceptedCSharpRow: true };
		var requiresValues = status == FindGroupMutationPostProjectedRowComparisonValueSourceStatus.RequiredEqualityValueSource;

		return new FindGroupMutationPostProjectedRowComparisonValueField(
			order,
			field.Action,
			field.MutationKind,
			field.FieldName,
			field.DifferenceKind,
			status,
			RequiresJavaValue: requiresValues,
			RequiresCSharpValue: requiresValues,
			CanEmitMatched: false,
			CanEmitFieldMismatch: false,
			JavaValueSourceFor(field, hasPair),
			CSharpValueSourceFor(field, hasPair),
			BlockerFor(field, row, hasPair),
			status == FindGroupMutationPostProjectedRowComparisonValueSourceStatus.IgnoredRuntimeContextValue
				? "Runtime-only field is not an equality input; include it only as future mismatch context."
				: "Equality field requires projected Java and C# values before a future executor may emit Matched or FieldMismatch.");
	}

	private static string JavaValueSourceFor(
		FindGroupMutationPostComparisonExecutionResultFieldContract field,
		bool hasPair)
	{
		return hasPair
			? $"Future Java row field '{field.FieldName}' from accepted Java artifact row for action={field.Action}; value not read by this contract."
			: $"Blocked Java row field '{field.FieldName}' for action={field.Action}; accepted paired input is incomplete.";
	}

	private static string CSharpValueSourceFor(
		FindGroupMutationPostComparisonExecutionResultFieldContract field,
		bool hasPair)
	{
		return hasPair
			? $"Future C# row field '{field.FieldName}' from accepted live boundary row for action={field.Action}; value not read by this contract."
			: $"Blocked C# row field '{field.FieldName}' for action={field.Action}; accepted paired input is incomplete.";
	}

	private static string BlockerFor(
		FindGroupMutationPostComparisonExecutionResultFieldContract field,
		FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow? row,
		bool hasPair)
	{
		if (field.Status == FindGroupMutationPostComparisonDifferenceFieldStatus.IgnoredForEquality)
			return "Ignored runtime context; not eligible for equality comparison.";

		if (hasPair)
			return "Values not projected or compared yet; future executor must read Java and C# values before emitting Matched or FieldMismatch.";

		return row?.Status switch
		{
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingJavaRow => "Missing accepted Java row value source.",
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingCSharpRow => "Missing accepted C# row value source.",
			_ => "Missing paired Java/C# value sources.",
		};
	}
}
