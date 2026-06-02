namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus
{
	BlockedReadinessSummaryNotReady,
	BlockedImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessArea
{
	TypedEqualityReaders,
	MismatchContextAttachment,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus
{
	BlockedReadinessSummaryNotReady,
	BlockedTypedReadersNotImplemented,
	BlockedContextAttachmentDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessArea Area,
	FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus Status,
	int BlockingFieldCount,
	bool CanImplement,
	bool CanReadValues,
	bool CanAttachContext,
	bool CanEmitComparisonResult,
	string RequiredImplementation,
	string ExistingMetadataProvider,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklist(
	FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRow> Rows,
	bool HasValueReaderReadinessSummary,
	bool HasTypedReaderBlockers,
	bool HasMismatchContextBlockers,
	bool CanImplementTypedReaders,
	bool CanAttachMismatchContext,
	bool CanReadValues,
	bool CanCompareValues,
	bool CanEmitComparisonResult,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live implementation-readiness checklist for
/// future CM_FIND_GROUP action 2/6 value readers. It separates typed equality
/// reader blockers from mismatch-context attachment blockers before any reader
/// implementation is written.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklist Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary? readinessSummary = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract? typedReaderPreflight = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContract? mismatchContextPreflight = null)
	{
		if (typedReaderPreflight is null || mismatchContextPreflight is null || readinessSummary is null)
		{
			var design = FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create();
			typedReaderPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);
			mismatchContextPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create(typedReaderPreflight);
			readinessSummary ??= FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create(
				design,
				typedReaderPreflight,
				mismatchContextPreflight);
		}

		var status = readinessSummary.Status == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedDesignNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus.BlockedReadinessSummaryNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus.BlockedImplementationDeferred;
		var rows = new[]
		{
			TypedReaderRow(typedReaderPreflight, status),
			MismatchContextRow(mismatchContextPreflight, status),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklist(
			status,
			rows,
			HasValueReaderReadinessSummary: readinessSummary.Stages.Count > 0,
			HasTypedReaderBlockers: typedReaderPreflight.Fields.Any(field => field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue),
			HasMismatchContextBlockers: mismatchContextPreflight.Fields.Count > 0,
			CanImplementTypedReaders: false,
			CanAttachMismatchContext: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanEmitComparisonResult: false,
			DecisionFor(status),
			readinessSummary.TraceName,
			readinessSummary.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRow TypedReaderRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract typedReaderPreflight,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus checklistStatus)
	{
		var requiredFields = typedReaderPreflight.Fields
			.Count(field => field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue);
		var status = checklistStatus == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus.BlockedReadinessSummaryNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus.BlockedReadinessSummaryNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus.BlockedTypedReadersNotImplemented;

		return new FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRow(
			1,
			FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessArea.TypedEqualityReaders,
			status,
			requiredFields,
			CanImplement: false,
			CanReadValues: false,
			CanAttachContext: false,
			CanEmitComparisonResult: false,
			"Implement typed Java JSON and C# trace-export readers for required equality fields before value comparison can run.",
			"FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService",
			$"preflightStatus={typedReaderPreflight.Status}; requiredFields={requiredFields}; readerKinds={typedReaderPreflight.ReaderKinds.Count}; canReadJavaValues={typedReaderPreflight.CanReadJavaValues}; canReadCSharpValues={typedReaderPreflight.CanReadCSharpValues}",
			"Typed equality readers are separate from runtime-only mismatch context and must not read ignored context fields as equality inputs.");
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRow MismatchContextRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContract mismatchContextPreflight,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus checklistStatus)
	{
		var status = checklistStatus == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus.BlockedReadinessSummaryNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus.BlockedReadinessSummaryNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus.BlockedContextAttachmentDeferred;

		return new FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRow(
			2,
			FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessArea.MismatchContextAttachment,
			status,
			mismatchContextPreflight.Fields.Count,
			CanImplement: false,
			CanReadValues: false,
			CanAttachContext: false,
			CanEmitComparisonResult: false,
			"Implement runtime-only context attachment only after real MissingJavaRow, MissingCSharpRow, or FieldMismatch results exist.",
			"FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService",
			$"contextStatus={mismatchContextPreflight.Status}; contextFields={string.Join("/", mismatchContextPreflight.RuntimeContextFieldNames)}; allowedTriggers={string.Join("/", mismatchContextPreflight.AllowedTriggers)}; canAttachContext={mismatchContextPreflight.CanAttachContext}",
			"Mismatch context attachment is separate from typed equality readers and must never enable Matched output or equality comparison.");
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus.BlockedReadinessSummaryNotReady => "Value-reader implementation checklist is blocked until readiness summary metadata reaches deferred implementation readiness.",
			_ => "Value-reader implementation checklist is blocked because typed readers and mismatch-context attachment remain intentionally unimplemented.",
		};
	}
}
