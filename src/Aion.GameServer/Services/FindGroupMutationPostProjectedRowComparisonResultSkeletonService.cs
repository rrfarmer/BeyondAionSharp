namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonResultSkeletonStatus
{
	BlockedDryRunNotReady,
	ReadyForFutureResultMaterialization,
}

public enum FindGroupMutationPostProjectedRowComparisonResultRowStatus
{
	PlannedMatchedRow,
	PlannedMissingJavaRow,
	PlannedMissingCSharpRow,
	PlannedFieldMismatch,
	PlannedIgnoredRuntimeContext,
}

public sealed record FindGroupMutationPostProjectedRowComparisonResultSkeletonRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonDryRunOutputKind OutputKind,
	FindGroupMutationPostProjectedRowComparisonResultRowStatus Status,
	bool RequiresLiveRows,
	string ResultShape,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonResultSkeleton(
	FindGroupMutationPostProjectedRowComparisonResultSkeletonStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonResultSkeletonRow> Rows,
	bool HasDryRunContract,
	bool RequiresJavaRows,
	bool RequiresLiveCSharpRows,
	bool RequiresFieldProjection,
	bool CanMaterializeRealResults,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: planned result skeleton for a future CM_FIND_GROUP action 2/6
/// projected-row comparison. This describes possible result rows, but it does not
/// materialize real comparison results from Java/C# row values.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonResultSkeletonService
{
	public static FindGroupMutationPostProjectedRowComparisonResultSkeleton Create(
		FindGroupMutationPostProjectedRowComparisonDryRunContract? dryRunContract = null)
	{
		dryRunContract ??= FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create();

		var rows = dryRunContract.OutputKinds
			.Select((kind, index) => CreateRow(index + 1, kind))
			.ToArray();
		var canMaterialize = dryRunContract.ShouldCompareRows
			&& dryRunContract.Status == FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor;

		return new FindGroupMutationPostProjectedRowComparisonResultSkeleton(
			canMaterialize
				? FindGroupMutationPostProjectedRowComparisonResultSkeletonStatus.ReadyForFutureResultMaterialization
				: FindGroupMutationPostProjectedRowComparisonResultSkeletonStatus.BlockedDryRunNotReady,
			rows,
			HasDryRunContract: dryRunContract.Fields.Count > 0,
			RequiresJavaRows: true,
			RequiresLiveCSharpRows: true,
			RequiresFieldProjection: dryRunContract.Fields.Any(field => field.Status == FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus.RequiredEqualityInput),
			CanMaterializeRealResults: canMaterialize,
			dryRunContract.TraceName,
			dryRunContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonResultSkeletonRow CreateRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		var status = outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => FindGroupMutationPostProjectedRowComparisonResultRowStatus.PlannedMatchedRow,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => FindGroupMutationPostProjectedRowComparisonResultRowStatus.PlannedMissingJavaRow,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => FindGroupMutationPostProjectedRowComparisonResultRowStatus.PlannedMissingCSharpRow,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => FindGroupMutationPostProjectedRowComparisonResultRowStatus.PlannedFieldMismatch,
			_ => FindGroupMutationPostProjectedRowComparisonResultRowStatus.PlannedIgnoredRuntimeContext,
		};

		return new FindGroupMutationPostProjectedRowComparisonResultSkeletonRow(
			order,
			outputKind,
			status,
			RequiresLiveRows: outputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			ResultShapeFor(outputKind),
			"Skeleton row only; no Java/C# row values were compared or materialized.");
	}

	private static string ResultShapeFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "action, mutationKind, rowIdentity, matchedFields",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "action, mutationKind, rowIdentity, csharpRowReference",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "action, mutationKind, rowIdentity, javaRowReference",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "action, mutationKind, fieldName, differenceKind, javaValue, csharpValue, javaSource",
			_ => "action, mutationKind, traceSource, serverEpochSeconds",
		};
	}
}
