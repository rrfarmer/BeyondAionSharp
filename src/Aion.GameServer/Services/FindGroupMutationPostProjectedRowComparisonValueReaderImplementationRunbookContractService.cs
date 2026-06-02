namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus
{
	BlockedReadinessSummaryNotReady,
	BlockedReaderImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep
{
	TypedScalarEqualityReaders,
	OrderedListEqualityReaders,
	EnumAndStringEqualityReaders,
	MismatchContextAttachment,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus
{
	BlockedReadinessSummaryNotReady,
	BlockedReaderImplementationDeferred,
	BlockedContextAttachmentDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep Step,
	FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderKind> ReaderKinds,
	int EqualityFieldCount,
	int ContextFieldCount,
	bool RequiresJavaReader,
	bool RequiresCSharpReader,
	bool PreservesCollectionOrder,
	bool CanImplement,
	bool CanReadValues,
	bool CanCompareValues,
	bool CanAttachContext,
	string ImplementationOrder,
	string PrerequisiteProvider,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepRow> Steps,
	int TotalEqualityFieldCount,
	int TotalContextFieldCount,
	bool HasValueReaderPreflight,
	bool HasImplementationReadinessChecklist,
	bool HasMismatchContextPreflight,
	bool CanImplementReaders,
	bool CanReadValues,
	bool CanCompareValues,
	bool CanAttachContext,
	bool CanEmitComparisonResult,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live implementation runbook for future
/// CM_FIND_GROUP action 2/6 value readers. It orders reader implementation
/// work from schema-v1 metadata without reading Java JSON or C# trace values.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklist? implementationChecklist = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract? typedReaderPreflight = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContract? mismatchContextPreflight = null)
	{
		if (typedReaderPreflight is null || mismatchContextPreflight is null || implementationChecklist is null)
		{
			var design = FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create();
			typedReaderPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);
			mismatchContextPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create(typedReaderPreflight);
			implementationChecklist ??= FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistService.Create(
				typedReaderPreflight: typedReaderPreflight,
				mismatchContextPreflight: mismatchContextPreflight);
		}

		var status = implementationChecklist.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus.BlockedReadinessSummaryNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReadinessSummaryNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReaderImplementationDeferred;
		var steps = new[]
		{
			ReaderStep(
				1,
				FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.TypedScalarEqualityReaders,
				status,
				typedReaderPreflight,
				[
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar,
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar,
				],
				"Implement schema-v1 int and bool equality readers first, because later reader families still depend on accepted Java/C# row pairing.",
				"Scalar readers cover ids, action codes, packet ids, booleans, and zero-count side-effect fields; they must not read ignored runtime context."),
			ReaderStep(
				2,
				FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.OrderedListEqualityReaders,
				status,
				typedReaderPreflight,
				[FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List],
				"Implement ordered integer-list readers after scalar readers and preserve Java materialized visible-entry ordering exactly.",
				"List readers are isolated because Java writes visibleEntryObjectIdsAfterMutation as an ordered array from materialized packet rows."),
			ReaderStep(
				3,
				FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.EnumAndStringEqualityReaders,
				status,
				typedReaderPreflight,
				[
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar,
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar,
				],
				"Implement ordinal string and enum-name readers after collection ordering is fixed, preserving schema-v1 names exactly.",
				"String/enum readers cover traceName, race, mutationKind, and packet type names; case and enum-name spelling must match Java JSON."),
			MismatchContextStep(4, status, mismatchContextPreflight),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract(
			status,
			steps,
			TotalEqualityFieldCount: typedReaderPreflight.Fields.Count(IsRequiredEqualityField),
			TotalContextFieldCount: mismatchContextPreflight.Fields.Count,
			HasValueReaderPreflight: typedReaderPreflight.Fields.Count > 0,
			HasImplementationReadinessChecklist: implementationChecklist.Rows.Count > 0,
			HasMismatchContextPreflight: mismatchContextPreflight.Fields.Count > 0,
			CanImplementReaders: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanAttachContext: false,
			CanEmitComparisonResult: false,
			DecisionFor(status),
			typedReaderPreflight.TraceName,
			typedReaderPreflight.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepRow ReaderStep(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep step,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus runbookStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract typedReaderPreflight,
		IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderKind> readerKinds,
		string implementationOrder,
		string notes)
	{
		var equalityFieldCount = typedReaderPreflight.Fields
			.Count(field => IsRequiredEqualityField(field) && readerKinds.Contains(field.ReaderKind));
		var status = runbookStatus == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReadinessSummaryNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus.BlockedReadinessSummaryNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus.BlockedReaderImplementationDeferred;

		return new FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepRow(
			order,
			step,
			status,
			readerKinds,
			equalityFieldCount,
			ContextFieldCount: 0,
			RequiresJavaReader: equalityFieldCount > 0,
			RequiresCSharpReader: equalityFieldCount > 0,
			PreservesCollectionOrder: readerKinds.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List),
			CanImplement: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanAttachContext: false,
			implementationOrder,
			"FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService",
			$"preflightStatus={typedReaderPreflight.Status}; readerKinds={string.Join("/", readerKinds)}; equalityFields={equalityFieldCount}; canReadJavaValues={typedReaderPreflight.CanReadJavaValues}; canReadCSharpValues={typedReaderPreflight.CanReadCSharpValues}",
			notes);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepRow MismatchContextStep(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus runbookStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContract mismatchContextPreflight)
	{
		var status = runbookStatus == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReadinessSummaryNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus.BlockedReadinessSummaryNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus.BlockedContextAttachmentDeferred;

		return new FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepRow(
			order,
			FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.MismatchContextAttachment,
			status,
			[FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext],
			EqualityFieldCount: 0,
			ContextFieldCount: mismatchContextPreflight.Fields.Count,
			RequiresJavaReader: false,
			RequiresCSharpReader: false,
			PreservesCollectionOrder: false,
			CanImplement: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanAttachContext: false,
			"Implement mismatch-context attachment last and only after real MissingJavaRow, MissingCSharpRow, or FieldMismatch result rows exist.",
			"FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService",
			$"contextStatus={mismatchContextPreflight.Status}; contextFields={string.Join("/", mismatchContextPreflight.RuntimeContextFieldNames)}; allowedTriggers={string.Join("/", mismatchContextPreflight.AllowedTriggers)}; canAttachContext={mismatchContextPreflight.CanAttachContext}",
			"Runtime context fields are not equality inputs and must not enable Matched output or change comparison decisions.");
	}

	private static bool IsRequiredEqualityField(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField field) =>
		field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue;

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReadinessSummaryNotReady => "Value-reader implementation runbook is blocked until readiness-summary metadata reaches deferred implementation readiness.",
			_ => "Value-reader implementation runbook is ordered, but reader implementation, value reads, comparison, context attachment, and result emission remain intentionally deferred.",
		};
	}
}
