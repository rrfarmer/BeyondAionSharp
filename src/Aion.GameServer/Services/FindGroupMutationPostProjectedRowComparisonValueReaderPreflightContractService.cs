namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus
{
	BlockedDesignNotReady,
	BlockedTypedReadersDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderKind
{
	Int32Scalar,
	StringScalar,
	BooleanScalar,
	EnumStringScalar,
	OrderedInt32List,
	IgnoredRuntimeContext,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus
{
	BlockedDesignNotReady,
	BlockedReaderImplementationDeferred,
	IgnoredRuntimeContextOnly,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostProjectedRowComparisonValueReadMode ReadMode,
	FindGroupMutationPostProjectedRowComparisonValueReaderKind ReaderKind,
	FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus Status,
	string ExpectedClrType,
	string JavaJsonToken,
	string CSharpValueShape,
	bool RequiresJavaReader,
	bool RequiresCSharpReader,
	bool PreservesCollectionOrder,
	bool CanReadValues,
	string JavaJsonPath,
	string CSharpAccessor,
	string ReaderPrecondition,
	string Blocker,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField> Fields,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderKind> ReaderKinds,
	bool HasValueReaderDesign,
	bool HasSchemaV1TypeMap,
	bool HasRequiredTypedReaders,
	bool CanReadJavaValues,
	bool CanReadCSharpValues,
	bool CanCompareValues,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live preflight for future CM_FIND_GROUP action
/// 2/6 schema-v1 value readers. It enumerates typed readers before any Java
/// JSON or C# trace-export value is read.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract? designContract = null)
	{
		designContract ??= FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create();
		var status = designContract.Status == FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus.BlockedExecutionGateNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus.BlockedDesignNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus.BlockedTypedReadersDeferred;
		var fields = designContract.Fields
			.Select((field, index) => CreateField(index + 1, field, status))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract(
			status,
			fields,
			fields.Select(field => field.ReaderKind).Distinct().ToArray(),
			HasValueReaderDesign: designContract.Fields.Count > 0,
			HasSchemaV1TypeMap: fields.Length > 0 && fields.All(field => !string.IsNullOrWhiteSpace(field.ExpectedClrType) && !string.IsNullOrWhiteSpace(field.JavaJsonToken)),
			HasRequiredTypedReaders: fields.Any(field => field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue),
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanCompareValues: false,
			DecisionFor(status),
			designContract.TraceName,
			designContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField CreateField(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderField field,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus preflightStatus)
	{
		var readerKind = field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext
			? FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext
			: ReaderKindFor(field.FieldName);
		var typeShape = TypeShapeFor(readerKind, field.FieldName);
		var status = FieldStatusFor(field, preflightStatus);

		return new FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField(
			order,
			field.Action,
			field.MutationKind,
			field.FieldName,
			field.ReadMode,
			readerKind,
			status,
			typeShape.ExpectedClrType,
			typeShape.JavaJsonToken,
			typeShape.CSharpValueShape,
			RequiresJavaReader: field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue,
			RequiresCSharpReader: field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue,
			PreservesCollectionOrder: readerKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List,
			CanReadValues: false,
			field.JavaJsonPath,
			field.CSharpAccessor,
			PreconditionFor(readerKind, field),
			BlockerFor(status, field),
			NotesFor(readerKind, field));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus FieldStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderField field,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus preflightStatus)
	{
		if (field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext)
			return FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus.IgnoredRuntimeContextOnly;

		return preflightStatus == FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus.BlockedDesignNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus.BlockedDesignNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus.BlockedReaderImplementationDeferred;
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderKind ReaderKindFor(string fieldName)
	{
		return fieldName switch
		{
			"traceName" or "activePlayerRace" or "postedSystemMessageType" or "refreshedListPacketType" => FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar,
			"mutationKind" => FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar,
			"boundaryAccepted" or "stateMutationRecordedBeforeDirectPackets" or "executorInvokedFromBoundary" or "registrySendsObservedInOrder" => FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar,
			"visibleEntryObjectIdsAfterMutation" => FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar,
		};
	}

	private static TypeShape TypeShapeFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderKind readerKind,
		string fieldName)
	{
		return readerKind switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar => new TypeShape("string", "JSON string", "string"),
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar => new TypeShape("FindGroupDirectPacketMutationPostTraceMutationKind", "JSON string enum name", "enum value serialized by name"),
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar => new TypeShape("bool", "JSON boolean", "bool"),
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List => new TypeShape("IReadOnlyList<int>", "JSON array of integers", "IReadOnlyList<int> preserving order"),
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext => TypeShapeFor(ReaderKindFor(fieldName), fieldName),
			_ => new TypeShape("int", "JSON integer", "int"),
		};
	}

	private static string PreconditionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderKind readerKind,
		FindGroupMutationPostProjectedRowComparisonValueReaderField field)
	{
		if (readerKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext)
			return $"Keep {field.FieldName} unavailable for equality; future mismatch context may read it only after a real comparison result exists.";

		return readerKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List
			? $"Validate {field.JavaJsonPath} is a JSON integer array and {field.CSharpAccessor} is an ordered integer list before comparing action={field.Action}."
			: $"Validate {field.JavaJsonPath} and {field.CSharpAccessor} both expose schema-v1 {TypeShapeFor(readerKind, field.FieldName).ExpectedClrType} before comparing action={field.Action}.";
	}

	private static string BlockerFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderField field)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus.IgnoredRuntimeContextOnly => "Runtime context field is ignored for equality and cannot enable comparison.",
			FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus.BlockedReaderImplementationDeferred => $"Typed reader for {field.FieldName} is named but not implemented.",
			_ => $"Typed reader for {field.FieldName} is blocked until value-reader design readiness is satisfied.",
		};
	}

	private static string NotesFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderKind readerKind,
		FindGroupMutationPostProjectedRowComparisonValueReaderField field)
	{
		if (readerKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List)
			return "Java serializer writes visible entry ids in stream/materialized packet order; future reader must preserve ordering exactly.";

		if (field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext)
			return "Runtime context stays out of equality and is available only for future mismatch diagnostics.";

		return "Preflight names the typed reader only; it does not inspect Java JSON, C# exports, or equality values.";
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus.BlockedDesignNotReady => "Value-reader preflight is blocked until the value-reader design reaches implementation-readiness.",
			_ => "Value-reader preflight is blocked because concrete typed readers are enumerated but intentionally unimplemented.",
		};
	}

	private sealed record TypeShape(string ExpectedClrType, string JavaJsonToken, string CSharpValueShape);
}
