namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus
{
	BlockedDryRunNotReady,
	BlockedMissingPairedRows,
	ReadyForFutureValueComparisonButDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus
{
	BlockedMissingJavaRow,
	BlockedMissingCSharpRow,
	BlockedValueComparisonDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string RequiredRowIdentity,
	FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus Status,
	bool HasAcceptedJavaRow,
	bool HasAcceptedCSharpRow,
	bool ComparesValues,
	string PlannedResultShape,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonExecutorSkeleton(
	FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow> Rows,
	bool HasDryRunContract,
	bool HasResultSkeleton,
	bool HasAllPairedInputs,
	bool ShouldAttemptExecutor,
	bool CanCompareValues,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live skeleton for the future CM_FIND_GROUP action 2/6
/// projected-row comparison executor. It consumes readiness rows, but it never
/// compares Java/C# values or emits real match/mismatch results.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonExecutorSkeletonService
{
	public static FindGroupMutationPostProjectedRowComparisonExecutorSkeleton Create(
		FindGroupMutationPostProjectedRowComparisonDryRunContract? dryRunContract = null,
		FindGroupMutationPostProjectedRowComparisonResultSkeleton? resultSkeleton = null)
	{
		dryRunContract ??= FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create();
		resultSkeleton ??= FindGroupMutationPostProjectedRowComparisonResultSkeletonService.Create(dryRunContract);

		var rows = dryRunContract.PairedRowReadiness
			.Select((row, index) => CreateRow(index + 1, row, resultSkeleton))
			.ToArray();
		var hasAllPairedInputs = rows.Length > 0
			&& rows.All(row => row.HasAcceptedJavaRow && row.HasAcceptedCSharpRow);
		var shouldAttemptExecutor = dryRunContract.ShouldCompareRows
			&& dryRunContract.Status == FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor;
		var status = DetermineStatus(shouldAttemptExecutor, hasAllPairedInputs);

		return new FindGroupMutationPostProjectedRowComparisonExecutorSkeleton(
			status,
			rows,
			HasDryRunContract: dryRunContract.Fields.Count > 0,
			HasResultSkeleton: resultSkeleton.Rows.Count > 0,
			hasAllPairedInputs,
			shouldAttemptExecutor,
			CanCompareValues: false,
			status == FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.ReadyForFutureValueComparisonButDeferred
				? "All paired inputs are present, but this skeleton deliberately defers Java/C# value comparison."
				: "Comparison executor not invoked; one or more dry-run or paired-row gates are blocked.",
			dryRunContract.TraceName,
			dryRunContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus DetermineStatus(
		bool shouldAttemptExecutor,
		bool hasAllPairedInputs)
	{
		if (!shouldAttemptExecutor)
			return FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.BlockedDryRunNotReady;

		if (!hasAllPairedInputs)
			return FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.BlockedMissingPairedRows;

		return FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.ReadyForFutureValueComparisonButDeferred;
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow CreateRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunPairedRowReadiness readiness,
		FindGroupMutationPostProjectedRowComparisonResultSkeleton resultSkeleton)
	{
		var status = DetermineRowStatus(readiness);
		return new FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow(
			order,
			readiness.Action,
			readiness.MutationKind,
			readiness.RequiredRowIdentity,
			status,
			readiness.HasAcceptedJavaRow,
			readiness.HasAcceptedCSharpRow,
			ComparesValues: false,
			PlannedResultShapeFor(status, resultSkeleton),
			readiness.Evidence,
			status == FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedValueComparisonDeferred
				? "Paired row input exists, but this skeleton does not compare field values or choose matched/mismatch output."
				: "Paired row input is incomplete; this skeleton emits only a blocked planned row.");
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus DetermineRowStatus(
		FindGroupMutationPostProjectedRowComparisonDryRunPairedRowReadiness readiness)
	{
		if (!readiness.HasAcceptedJavaRow)
			return FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingJavaRow;

		if (!readiness.HasAcceptedCSharpRow)
			return FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingCSharpRow;

		return FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedValueComparisonDeferred;
	}

	private static string PlannedResultShapeFor(
		FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus status,
		FindGroupMutationPostProjectedRowComparisonResultSkeleton resultSkeleton)
	{
		var outputKind = status switch
		{
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingJavaRow => FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingCSharpRow => FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
			_ => FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
		};

		return resultSkeleton.Rows.Single(row => row.OutputKind == outputKind).ResultShape;
	}
}
