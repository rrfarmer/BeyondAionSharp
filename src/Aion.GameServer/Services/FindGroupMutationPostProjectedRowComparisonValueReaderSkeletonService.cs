namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus
{
	BlockedDesignNotReady,
	BlockedMissingAcceptedRows,
	BlockedReaderImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus
{
	BlockedMissingJavaRow,
	BlockedMissingCSharpRow,
	BlockedReaderImplementationDeferred,
	IgnoredRuntimeContextOnly,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReadAttempt(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostProjectedRowComparisonValueReadMode ReadMode,
	FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus Status,
	bool HasAcceptedJavaRow,
	bool HasAcceptedCSharpRow,
	bool AttemptsJavaRead,
	bool AttemptsCSharpRead,
	bool CanReadValue,
	string JavaJsonPath,
	string CSharpAccessor,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton(
	FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReadAttempt> Attempts,
	bool HasDesignContract,
	bool HasDryRunContract,
	bool HasAcceptedJavaRows,
	bool HasAcceptedCSharpRows,
	bool HasAllPairedRows,
	bool CanReadValues,
	bool CanCompareValues,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live skeleton for future CM_FIND_GROUP action 2/6
/// projected-row value reading. It consumes accepted row references and reader
/// design rows, but it does not parse JSON or access C# row values.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract? designContract = null,
		FindGroupMutationPostProjectedRowComparisonDryRunContract? dryRunContract = null)
	{
		designContract ??= FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create();
		dryRunContract ??= FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create();
		var attempts = designContract.Fields
			.Select((field, index) => CreateAttempt(index + 1, field, dryRunContract))
			.ToArray();
		var hasAllPairedRows = dryRunContract.PairedRowReadiness.Count > 0
			&& dryRunContract.PairedRowReadiness.All(row => row.HasAcceptedJavaRow && row.HasAcceptedCSharpRow);
		var status = DetermineStatus(designContract, hasAllPairedRows);

		return new FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton(
			status,
			attempts,
			HasDesignContract: designContract.Fields.Count > 0,
			HasDryRunContract: dryRunContract.Fields.Count > 0,
			HasAcceptedJavaRows: dryRunContract.AcceptedJavaRows.Count > 0,
			HasAcceptedCSharpRows: dryRunContract.AcceptedCSharpRows.Count > 0,
			hasAllPairedRows,
			CanReadValues: false,
			CanCompareValues: false,
			DecisionFor(status),
			designContract.TraceName,
			designContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus DetermineStatus(
		FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract designContract,
		bool hasAllPairedRows)
	{
		if (designContract.Status == FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus.BlockedExecutionGateNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedDesignNotReady;

		if (!hasAllPairedRows)
			return FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedMissingAcceptedRows;

		return FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedReaderImplementationDeferred;
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReadAttempt CreateAttempt(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderField field,
		FindGroupMutationPostProjectedRowComparisonDryRunContract dryRunContract)
	{
		var pairedRow = dryRunContract.PairedRowReadiness.SingleOrDefault(row => row.Action == field.Action && row.MutationKind == field.MutationKind);
		var hasJava = pairedRow?.HasAcceptedJavaRow ?? false;
		var hasCSharp = pairedRow?.HasAcceptedCSharpRow ?? false;
		var status = DetermineAttemptStatus(field, hasJava, hasCSharp);

		return new FindGroupMutationPostProjectedRowComparisonValueReadAttempt(
			order,
			field.Action,
			field.MutationKind,
			field.FieldName,
			field.ReadMode,
			status,
			hasJava,
			hasCSharp,
			AttemptsJavaRead: false,
			AttemptsCSharpRead: false,
			CanReadValue: false,
			field.JavaJsonPath,
			field.CSharpAccessor,
			$"action={field.Action}; field={field.FieldName}; hasJavaRow={hasJava}; hasCSharpRow={hasCSharp}; designStatus={field.Status}",
			NotesFor(status, field));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus DetermineAttemptStatus(
		FindGroupMutationPostProjectedRowComparisonValueReaderField field,
		bool hasJava,
		bool hasCSharp)
	{
		if (field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext)
			return FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.IgnoredRuntimeContextOnly;

		if (!hasJava)
			return FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedMissingJavaRow;

		if (!hasCSharp)
			return FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedMissingCSharpRow;

		return FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedReaderImplementationDeferred;
	}

	private static string NotesFor(
		FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderField field)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.IgnoredRuntimeContextOnly => "Runtime-only context is not read until attached to a real mismatch result.",
			FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedMissingJavaRow => $"Cannot read Java {field.JavaJsonPath} without an accepted Java runtime row.",
			FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedMissingCSharpRow => $"Cannot read C# {field.CSharpAccessor} without an accepted live C# boundary row.",
			_ => "Accepted rows are present, but this skeleton deliberately does not read or compare field values.",
		};
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedDesignNotReady => "Value-reader skeleton is blocked because the value-reader design contract is not ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedMissingAcceptedRows => "Value-reader skeleton is blocked because accepted Java/C# row references are incomplete.",
			_ => "Value-reader skeleton is blocked because value reading is intentionally deferred.",
		};
	}
}
