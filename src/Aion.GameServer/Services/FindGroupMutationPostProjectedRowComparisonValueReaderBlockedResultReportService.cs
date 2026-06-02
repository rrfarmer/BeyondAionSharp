namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus
{
	BlockedValueReaderSkeletonNotReady,
	BlockedMissingAcceptedRows,
	BlockedReaderImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind
{
	MissingJavaRows,
	MissingCSharpRows,
	IgnoredRuntimeContext,
	DeferredReaderImplementation,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus
{
	UnavailableSkeletonBlocked,
	Blocked,
	IgnoredRuntimeContextOnly,
	Deferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind Kind,
	FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus Status,
	int AttemptCount,
	bool CanReadValues,
	bool CanEmitComparisonResult,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReport(
	FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRow> Rows,
	bool HasValueReaderSkeleton,
	int TotalAttempts,
	int MissingJavaRowAttempts,
	int MissingCSharpRowAttempts,
	int IgnoredRuntimeContextAttempts,
	int DeferredReaderImplementationAttempts,
	bool AttemptsAnyJavaRead,
	bool AttemptsAnyCSharpRead,
	bool CanReadValues,
	bool CanCompareValues,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live blocked-result summary for future
/// CM_FIND_GROUP action 2/6 value-reader execution. It only counts blocked
/// skeleton attempts and never reads Java JSON or C# trace-export values.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReport Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton? skeleton = null)
	{
		skeleton ??= FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create();
		var missingJavaRows = Count(skeleton, FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedMissingJavaRow);
		var missingCSharpRows = Count(skeleton, FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedMissingCSharpRow);
		var ignoredRuntimeContext = Count(skeleton, FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.IgnoredRuntimeContextOnly);
		var deferredReaderImplementation = Count(skeleton, FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedReaderImplementationDeferred);
		var status = DetermineStatus(skeleton);
		var rows = new[]
		{
			CreateRow(1, FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.MissingJavaRows, missingJavaRows, status, "Java runtime trace rows accepted by the dry-run contract."),
			CreateRow(2, FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.MissingCSharpRows, missingCSharpRows, status, "Live C# boundary trace rows accepted by the dry-run contract."),
			CreateRow(3, FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.IgnoredRuntimeContext, ignoredRuntimeContext, status, "Runtime-only context fields that are ignored until attached to real mismatch output."),
			CreateRow(4, FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.DeferredReaderImplementation, deferredReaderImplementation, status, "Accepted paired rows with value-reader implementation still deferred."),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReport(
			status,
			rows,
			HasValueReaderSkeleton: skeleton.Attempts.Count > 0,
			TotalAttempts: skeleton.Attempts.Count,
			MissingJavaRowAttempts: missingJavaRows,
			MissingCSharpRowAttempts: missingCSharpRows,
			IgnoredRuntimeContextAttempts: ignoredRuntimeContext,
			DeferredReaderImplementationAttempts: deferredReaderImplementation,
			AttemptsAnyJavaRead: skeleton.Attempts.Any(attempt => attempt.AttemptsJavaRead),
			AttemptsAnyCSharpRead: skeleton.Attempts.Any(attempt => attempt.AttemptsCSharpRead),
			CanReadValues: false,
			CanCompareValues: false,
			DecisionFor(status),
			skeleton.TraceName,
			skeleton.JavaSource,
			IsLive: false);
	}

	private static int Count(
		FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton skeleton,
		FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus status) =>
		skeleton.Attempts.Count(attempt => attempt.Status == status);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus DetermineStatus(
		FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton skeleton)
	{
		return skeleton.Status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedDesignNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedValueReaderSkeletonNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedMissingAcceptedRows => FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedMissingAcceptedRows,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedReaderImplementationDeferred,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRow CreateRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind kind,
		int attemptCount,
		FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus reportStatus,
		string requiredInput)
	{
		return new FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRow(
			order,
			kind,
			RowStatusFor(kind, reportStatus),
			attemptCount,
			CanReadValues: false,
			CanEmitComparisonResult: false,
			$"attemptCount={attemptCount}; requiredInput={requiredInput}",
			NotesFor(kind, reportStatus));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind kind,
		FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus reportStatus)
	{
		if (kind == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.IgnoredRuntimeContext)
			return FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus.IgnoredRuntimeContextOnly;

		if (kind == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.DeferredReaderImplementation)
			return FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus.Deferred;

		return reportStatus == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedValueReaderSkeletonNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus.UnavailableSkeletonBlocked
			: FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus.Blocked;
	}

	private static string NotesFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind kind,
		FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus reportStatus)
	{
		return kind switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.MissingJavaRows => "MissingJavaRows count is a skeleton blocker summary; no Java artifact values were parsed.",
			FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.MissingCSharpRows => "MissingCSharpRows count is a skeleton blocker summary; no C# trace-export values were read.",
			FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.IgnoredRuntimeContext => "Ignored runtime context fields remain unavailable until a real comparison result needs context.",
			_ => reportStatus == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedReaderImplementationDeferred
				? "Accepted rows exist, but reader implementation is still intentionally deferred."
				: "Reader implementation remains deferred behind earlier blockers.",
		};
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedValueReaderSkeletonNotReady => "Value-reader blocked-result report is blocked because the value-reader skeleton is not ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedMissingAcceptedRows => "Value-reader blocked-result report is blocked because accepted Java/C# row references are incomplete.",
			_ => "Value-reader blocked-result report is blocked because field value reading is intentionally deferred.",
		};
	}
}
