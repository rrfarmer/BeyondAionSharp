namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus
{
	BlockedPreflightNotReady,
	BlockedContextAttachmentDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger
{
	MissingJavaRow,
	MissingCSharpRow,
	FieldMismatch,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextFieldStatus
{
	BlockedPreflightNotReady,
	BlockedContextAttachmentDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextField(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostProjectedRowComparisonValueReaderKind ReaderKind,
	FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextFieldStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger> AllowedTriggers,
	bool IsEqualityInput,
	bool CanReadContextValues,
	bool CanAttachContext,
	string JavaJsonPath,
	string CSharpAccessor,
	string AttachmentRule,
	string Blocker,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextField> Fields,
	IReadOnlyList<string> RuntimeContextFieldNames,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger> AllowedTriggers,
	bool HasValueReaderPreflight,
	bool HasRuntimeContextFields,
	bool CanReadContextValues,
	bool CanAttachContext,
	bool CanEmitComparisonResult,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live mismatch-context preflight for future
/// CM_FIND_GROUP action 2/6 projected-row comparison. It names runtime-only
/// fields that may be attached after a real missing-row or FieldMismatch result,
/// but it never reads Java JSON or C# trace-export values.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService
{
	private static readonly FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger[] ContextTriggers =
	[
		FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger.MissingJavaRow,
		FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger.MissingCSharpRow,
		FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger.FieldMismatch,
	];

	public static FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract? valueReaderPreflight = null)
	{
		valueReaderPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();
		var status = valueReaderPreflight.Status == FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus.BlockedDesignNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus.BlockedPreflightNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus.BlockedContextAttachmentDeferred;
		var fields = valueReaderPreflight.Fields
			.Where(field => field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext)
			.Select((field, index) => CreateField(index + 1, field, status))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContract(
			status,
			fields,
			fields.Select(field => field.FieldName).Distinct(StringComparer.Ordinal).ToArray(),
			ContextTriggers,
			HasValueReaderPreflight: valueReaderPreflight.Fields.Count > 0,
			HasRuntimeContextFields: fields.Length > 0,
			CanReadContextValues: false,
			CanAttachContext: false,
			CanEmitComparisonResult: false,
			DecisionFor(status),
			valueReaderPreflight.TraceName,
			valueReaderPreflight.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextField CreateField(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField field,
		FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus preflightStatus)
	{
		var status = preflightStatus == FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus.BlockedPreflightNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextFieldStatus.BlockedPreflightNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextFieldStatus.BlockedContextAttachmentDeferred;

		return new FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextField(
			order,
			field.Action,
			field.MutationKind,
			field.FieldName,
			field.ReaderKind,
			status,
			ContextTriggers,
			IsEqualityInput: false,
			CanReadContextValues: false,
			CanAttachContext: false,
			field.JavaJsonPath,
			field.CSharpAccessor,
			$"Attach {field.FieldName} only after a real MissingJavaRow, MissingCSharpRow, or FieldMismatch result exists for action={field.Action}; never use it to decide equality.",
			BlockerFor(status, field),
			"Runtime-only context remains unavailable until a real comparison result exists and still must not affect equality.");
	}

	private static string BlockerFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextFieldStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField field)
	{
		return status == FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextFieldStatus.BlockedPreflightNotReady
			? $"Mismatch context for {field.FieldName} is blocked until typed-reader preflight reaches deferred-reader readiness."
			: $"Mismatch context for {field.FieldName} is named but cannot be attached until a real missing-row or FieldMismatch result is emitted.";
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus.BlockedPreflightNotReady => "Mismatch-context preflight is blocked until value-reader typed-reader preflight reaches implementation-readiness.",
			_ => "Mismatch-context preflight is blocked because ignored runtime fields can only attach after real missing-row or FieldMismatch results.",
		};
	}
}
