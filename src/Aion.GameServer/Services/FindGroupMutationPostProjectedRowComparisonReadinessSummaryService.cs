namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus
{
	BlockedDryRunNotReady,
	BlockedMissingPairedInputs,
	BlockedValueProjectionDeferred,
	BlockedResultEmissionUnavailable,
}

public enum FindGroupMutationPostProjectedRowComparisonReadinessStage
{
	DryRunContract,
	ExecutorSkeleton,
	ValueContract,
	BlockedResultReport,
}

public enum FindGroupMutationPostProjectedRowComparisonReadinessStageStatus
{
	Blocked,
	Deferred,
	ReadyForFutureInput,
}

public sealed record FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonReadinessStage Stage,
	FindGroupMutationPostProjectedRowComparisonReadinessStageStatus Status,
	bool HasExpectedShape,
	bool BlocksComparison,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonReadinessSummary(
	FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow> Stages,
	bool HasDryRunContract,
	bool HasExecutorSkeleton,
	bool HasValueContract,
	bool HasBlockedResultReport,
	bool HasAllPairedInputs,
	bool CanCompareRows,
	bool CanProjectValues,
	bool CanEmitResults,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: top-level non-live readiness summary for future
/// CM_FIND_GROUP action 2/6 projected-row comparison. It links the staged
/// contracts, but it does not execute comparison or emit results.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonReadinessSummaryService
{
	public static FindGroupMutationPostProjectedRowComparisonReadinessSummary Create(
		FindGroupMutationPostProjectedRowComparisonDryRunContract? dryRunContract = null,
		FindGroupMutationPostProjectedRowComparisonExecutorSkeleton? executorSkeleton = null,
		FindGroupMutationPostProjectedRowComparisonValueContract? valueContract = null,
		FindGroupMutationPostProjectedRowComparisonBlockedResultReport? blockedResultReport = null)
	{
		dryRunContract ??= FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create();
		executorSkeleton ??= FindGroupMutationPostProjectedRowComparisonExecutorSkeletonService.Create(dryRunContract);
		valueContract ??= FindGroupMutationPostProjectedRowComparisonValueContractService.Create(executorSkeleton);
		blockedResultReport ??= FindGroupMutationPostProjectedRowComparisonBlockedResultReportService.Create(executorSkeleton, valueContract);

		var status = DetermineStatus(dryRunContract, executorSkeleton, valueContract, blockedResultReport);
		var stages = new[]
		{
			DryRunStage(dryRunContract),
			ExecutorStage(executorSkeleton),
			ValueStage(valueContract),
			BlockedResultStage(blockedResultReport),
		};

		return new FindGroupMutationPostProjectedRowComparisonReadinessSummary(
			status,
			stages,
			HasDryRunContract: dryRunContract.Fields.Count > 0,
			HasExecutorSkeleton: executorSkeleton.Rows.Count > 0,
			HasValueContract: valueContract.Fields.Count > 0,
			HasBlockedResultReport: blockedResultReport.Rows.Count > 0,
			executorSkeleton.HasAllPairedInputs && valueContract.HasAllPairedInputs && blockedResultReport.HasAllPairedInputs,
			CanCompareRows: dryRunContract.ShouldCompareRows && executorSkeleton.CanCompareValues,
			valueContract.CanProjectValues,
			blockedResultReport.CanEmitMatched
				|| blockedResultReport.CanEmitMissingJavaRow
				|| blockedResultReport.CanEmitMissingCSharpRow
				|| blockedResultReport.CanEmitFieldMismatch
				|| blockedResultReport.CanEmitIgnoredRuntimeContext,
			DecisionFor(status),
			dryRunContract.TraceName,
			dryRunContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus DetermineStatus(
		FindGroupMutationPostProjectedRowComparisonDryRunContract dryRunContract,
		FindGroupMutationPostProjectedRowComparisonExecutorSkeleton executorSkeleton,
		FindGroupMutationPostProjectedRowComparisonValueContract valueContract,
		FindGroupMutationPostProjectedRowComparisonBlockedResultReport blockedResultReport)
	{
		if (dryRunContract.Status == FindGroupMutationPostProjectedRowComparisonDryRunStatus.BlockedByExecutionReport
			|| executorSkeleton.Status == FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.BlockedDryRunNotReady)
			return FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedDryRunNotReady;

		if (!executorSkeleton.HasAllPairedInputs || valueContract.Status == FindGroupMutationPostProjectedRowComparisonValueContractStatus.BlockedMissingValueSources)
			return FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedMissingPairedInputs;

		if (valueContract.Status == FindGroupMutationPostProjectedRowComparisonValueContractStatus.ReadyForFutureValueProjectionButDeferred)
			return FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedValueProjectionDeferred;

		if (blockedResultReport.Status == FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedValueComparisonUnavailable)
			return FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedResultEmissionUnavailable;

		return FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedResultEmissionUnavailable;
	}

	private static FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow DryRunStage(
		FindGroupMutationPostProjectedRowComparisonDryRunContract dryRunContract)
	{
		var ready = dryRunContract.Status == FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor;
		return new FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
			1,
			FindGroupMutationPostProjectedRowComparisonReadinessStage.DryRunContract,
			ready ? FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.ReadyForFutureInput : FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Blocked,
			HasExpectedShape: dryRunContract.Fields.Count > 0 && dryRunContract.Actions.Count == 2,
			BlocksComparison: !ready,
			$"status={dryRunContract.Status}; actions={dryRunContract.Actions.Count}; fields={dryRunContract.Fields.Count}; shouldCompareRows={dryRunContract.ShouldCompareRows}",
			dryRunContract.ExecutionDecision);
	}

	private static FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow ExecutorStage(
		FindGroupMutationPostProjectedRowComparisonExecutorSkeleton executorSkeleton)
	{
		var ready = executorSkeleton.Status == FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.ReadyForFutureValueComparisonButDeferred;
		return new FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
			2,
			FindGroupMutationPostProjectedRowComparisonReadinessStage.ExecutorSkeleton,
			ready ? FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Deferred : FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Blocked,
			executorSkeleton.Rows.Count == 2,
			BlocksComparison: !ready,
			$"status={executorSkeleton.Status}; rows={executorSkeleton.Rows.Count}; hasAllPairedInputs={executorSkeleton.HasAllPairedInputs}; canCompareValues={executorSkeleton.CanCompareValues}",
			executorSkeleton.ExecutionDecision);
	}

	private static FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow ValueStage(
		FindGroupMutationPostProjectedRowComparisonValueContract valueContract)
	{
		var deferred = valueContract.Status == FindGroupMutationPostProjectedRowComparisonValueContractStatus.ReadyForFutureValueProjectionButDeferred;
		return new FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
			3,
			FindGroupMutationPostProjectedRowComparisonReadinessStage.ValueContract,
			deferred ? FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Deferred : FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Blocked,
			valueContract.Fields.Count > 0,
			BlocksComparison: !deferred || !valueContract.CanProjectValues,
			$"status={valueContract.Status}; fields={valueContract.Fields.Count}; canProjectValues={valueContract.CanProjectValues}; canEmitMatched={valueContract.CanEmitMatched}; canEmitFieldMismatch={valueContract.CanEmitFieldMismatch}",
			valueContract.ExecutionDecision);
	}

	private static FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow BlockedResultStage(
		FindGroupMutationPostProjectedRowComparisonBlockedResultReport blockedResultReport)
	{
		return new FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
			4,
			FindGroupMutationPostProjectedRowComparisonReadinessStage.BlockedResultReport,
			FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Blocked,
			blockedResultReport.Rows.Count == 5,
			BlocksComparison: true,
			$"status={blockedResultReport.Status}; rows={blockedResultReport.Rows.Count}; canEmitMatched={blockedResultReport.CanEmitMatched}; canEmitFieldMismatch={blockedResultReport.CanEmitFieldMismatch}",
			blockedResultReport.ExecutionDecision);
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedDryRunNotReady => "Projected-row comparison remains blocked before future executor readiness.",
			FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedMissingPairedInputs => "Projected-row comparison remains blocked because action 2/6 Java and C# inputs are not fully paired.",
			FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedValueProjectionDeferred => "Projected-row comparison remains blocked because value projection is still deferred.",
			_ => "Projected-row comparison remains blocked because result emission is unavailable.",
		};
	}
}
