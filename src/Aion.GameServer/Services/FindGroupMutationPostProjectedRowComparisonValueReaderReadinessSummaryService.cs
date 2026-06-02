namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus
{
	BlockedDesignNotReady,
	BlockedMissingAcceptedRows,
	BlockedReaderImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage
{
	DesignContract,
	PreflightContract,
	ReaderSkeleton,
	BlockedResultReport,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus
{
	Blocked,
	Deferred,
	ReadyForFutureInput,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage Stage,
	FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus Status,
	bool HasExpectedShape,
	bool BlocksValueReading,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary(
	FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow> Stages,
	bool HasDesignContract,
	bool HasPreflightContract,
	bool HasValueReaderSkeleton,
	bool HasBlockedResultReport,
	bool HasRequiredFieldMappings,
	bool HasAllPairedRows,
	bool CanReadValues,
	bool CanCompareValues,
	bool CanEmitComparisonResult,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live staged readiness summary for future
/// CM_FIND_GROUP action 2/6 value reading. It links the design, typed-reader
/// preflight, skeleton, and blocked-result report, but it never reads values or
/// emits comparison rows.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract? designContract = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract? preflightContract = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton? skeleton = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReport? blockedReport = null)
	{
		designContract ??= FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create();
		preflightContract ??= FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(designContract);
		skeleton ??= FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create(designContract);
		blockedReport ??= FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(skeleton);

		var status = DetermineStatus(designContract, preflightContract, skeleton);
		var stages = new[]
		{
			DesignStage(designContract),
			PreflightStage(preflightContract),
			SkeletonStage(skeleton),
			BlockedReportStage(blockedReport),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary(
			status,
			stages,
			HasDesignContract: designContract.Fields.Count > 0,
			HasPreflightContract: preflightContract.Fields.Count > 0,
			HasValueReaderSkeleton: skeleton.Attempts.Count > 0,
			HasBlockedResultReport: blockedReport.Rows.Count > 0,
			designContract.HasRequiredFieldMappings,
			skeleton.HasAllPairedRows,
			CanReadValues: false,
			CanCompareValues: false,
			CanEmitComparisonResult: false,
			DecisionFor(status),
			designContract.TraceName,
			designContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus DetermineStatus(
		FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract designContract,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract preflightContract,
		FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton skeleton)
	{
		if (designContract.Status == FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus.BlockedExecutionGateNotReady
			|| preflightContract.Status == FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus.BlockedDesignNotReady
			|| skeleton.Status == FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedDesignNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedDesignNotReady;

		if (!skeleton.HasAllPairedRows)
			return FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedMissingAcceptedRows;

		return FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedReaderImplementationDeferred;
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow DesignStage(
		FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract designContract)
	{
		var deferred = designContract.Status == FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus.BlockedValueReaderNotImplemented;
		return new FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow(
			1,
			FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.DesignContract,
			deferred ? FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Deferred : FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Blocked,
			HasExpectedShape: designContract.Fields.Count > 0 && designContract.JavaJsonPaths.Count > 0 && designContract.CSharpAccessors.Count > 0,
			BlocksValueReading: !deferred || !designContract.HasRequiredFieldMappings,
			$"status={designContract.Status}; fields={designContract.Fields.Count}; javaPaths={designContract.JavaJsonPaths.Count}; csharpAccessors={designContract.CSharpAccessors.Count}; canReadJavaValues={designContract.CanReadJavaValues}; canReadCSharpValues={designContract.CanReadCSharpValues}",
			designContract.ExecutionDecision);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow PreflightStage(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract preflightContract)
	{
		var deferred = preflightContract.Status == FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus.BlockedTypedReadersDeferred;
		return new FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow(
			2,
			FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.PreflightContract,
			deferred ? FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Deferred : FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Blocked,
			HasExpectedShape: preflightContract.Fields.Count > 0 && preflightContract.HasSchemaV1TypeMap,
			BlocksValueReading: true,
			$"status={preflightContract.Status}; fields={preflightContract.Fields.Count}; readerKinds={preflightContract.ReaderKinds.Count}; hasSchemaV1TypeMap={preflightContract.HasSchemaV1TypeMap}; canReadJavaValues={preflightContract.CanReadJavaValues}; canReadCSharpValues={preflightContract.CanReadCSharpValues}",
			preflightContract.ExecutionDecision);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow SkeletonStage(
		FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton skeleton)
	{
		var deferred = skeleton.Status == FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedReaderImplementationDeferred;
		var readyForFutureInput = skeleton.Status == FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedMissingAcceptedRows;
		return new FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow(
			3,
			FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.ReaderSkeleton,
			deferred
				? FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Deferred
				: readyForFutureInput
					? FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.ReadyForFutureInput
					: FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Blocked,
			HasExpectedShape: skeleton.Attempts.Count > 0,
			BlocksValueReading: !deferred,
			$"status={skeleton.Status}; attempts={skeleton.Attempts.Count}; hasAllPairedRows={skeleton.HasAllPairedRows}; canReadValues={skeleton.CanReadValues}; canCompareValues={skeleton.CanCompareValues}",
			skeleton.ExecutionDecision);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow BlockedReportStage(
		FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReport blockedReport)
	{
		return new FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStageRow(
			4,
			FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.BlockedResultReport,
			FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Blocked,
			HasExpectedShape: blockedReport.Rows.Count == 4,
			BlocksValueReading: true,
			$"status={blockedReport.Status}; rows={blockedReport.Rows.Count}; totalAttempts={blockedReport.TotalAttempts}; missingJavaRows={blockedReport.MissingJavaRowAttempts}; missingCSharpRows={blockedReport.MissingCSharpRowAttempts}; deferredReaderImplementation={blockedReport.DeferredReaderImplementationAttempts}",
			blockedReport.ExecutionDecision);
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedDesignNotReady => "Value-reader readiness remains blocked before design/runtime-evidence readiness.",
			FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedMissingAcceptedRows => "Value-reader readiness remains blocked because accepted Java/C# rows are not fully paired.",
			_ => "Value-reader readiness remains blocked because reader implementation is intentionally deferred.",
		};
	}
}
