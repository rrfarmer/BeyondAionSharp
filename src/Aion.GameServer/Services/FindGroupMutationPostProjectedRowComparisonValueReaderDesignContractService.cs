namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus
{
	BlockedExecutionGateNotReady,
	BlockedValueReaderNotImplemented,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReadMode
{
	RequiredEqualityValue,
	IgnoredRuntimeContext,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus
{
	BlockedMissingRuntimeRows,
	BlockedReaderNotImplemented,
	IgnoredRuntimeContextOnly,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderField(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostComparisonDifferenceKind DifferenceKind,
	FindGroupMutationPostProjectedRowComparisonValueReadMode ReadMode,
	FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus Status,
	bool RequiresJavaRead,
	bool RequiresCSharpRead,
	bool CanReadValues,
	string JavaJsonPath,
	string CSharpAccessor,
	string ReaderRule,
	string Blocker,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderField> Fields,
	IReadOnlyList<string> JavaJsonPaths,
	IReadOnlyList<string> CSharpAccessors,
	bool HasExecutionReadinessGate,
	bool HasValueContract,
	bool HasRequiredFieldMappings,
	bool CanReadJavaValues,
	bool CanReadCSharpValues,
	bool CanCompareValues,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live value-reader design contract for future
/// CM_FIND_GROUP action 2/6 projected-row comparison. It names how Java JSON
/// fields and C# trace-export properties will be read, but reads no values.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract Create(
		FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateReport? executionGate = null,
		FindGroupMutationPostProjectedRowComparisonValueContract? valueContract = null)
	{
		executionGate ??= FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateService.Create();
		valueContract ??= FindGroupMutationPostProjectedRowComparisonValueContractService.Create();
		var status = executionGate.Status == FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedLiveInputHandoffNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus.BlockedExecutionGateNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus.BlockedValueReaderNotImplemented;
		var fields = valueContract.Fields
			.Select((field, index) => CreateField(index + 1, field, executionGate))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract(
			status,
			fields,
			fields.Select(field => field.JavaJsonPath).Distinct(StringComparer.Ordinal).ToArray(),
			fields.Select(field => field.CSharpAccessor).Distinct(StringComparer.Ordinal).ToArray(),
			HasExecutionReadinessGate: executionGate.Rows.Count > 0,
			HasValueContract: valueContract.Fields.Count > 0,
			HasRequiredFieldMappings: fields.Any() && fields.All(field => !string.IsNullOrWhiteSpace(field.JavaJsonPath) && !string.IsNullOrWhiteSpace(field.CSharpAccessor)),
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanCompareValues: false,
			DecisionFor(status),
			valueContract.TraceName,
			valueContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderField CreateField(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueField field,
		FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateReport executionGate)
	{
		var readMode = field.Status == FindGroupMutationPostProjectedRowComparisonValueSourceStatus.IgnoredRuntimeContextValue
			? FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext
			: FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue;
		var status = readMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext
			? FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus.IgnoredRuntimeContextOnly
			: executionGate.HasRuntimeEvidence
				? FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus.BlockedReaderNotImplemented
				: FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus.BlockedMissingRuntimeRows;

		return new FindGroupMutationPostProjectedRowComparisonValueReaderField(
			order,
			field.Action,
			field.MutationKind,
			field.FieldName,
			field.DifferenceKind,
			readMode,
			status,
			RequiresJavaRead: readMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue,
			RequiresCSharpRead: readMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue,
			CanReadValues: false,
			JavaJsonPathFor(field.FieldName),
			CSharpAccessorFor(field.FieldName),
			ReaderRuleFor(field, readMode),
			BlockerFor(status, field),
			readMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext
				? "Runtime-only field is read only for mismatch context after another field differs."
				: "Future reader must project the same typed value from Java JSON and C# trace export before comparison.");
	}

	private static string JavaJsonPathFor(string fieldName)
	{
		return $"$.traces[*].{fieldName}";
	}

	private static string CSharpAccessorFor(string fieldName)
	{
		return $"FindGroupDirectPacketMutationPostBoundaryTraceExport.{ToPascalCase(fieldName)}";
	}

	private static string ReaderRuleFor(
		FindGroupMutationPostProjectedRowComparisonValueField field,
		FindGroupMutationPostProjectedRowComparisonValueReadMode readMode)
	{
		if (readMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext)
			return $"Do not compare {field.FieldName}; retain Java and C# values only if a future mismatch report needs runtime context.";

		return $"Read Java {JavaJsonPathFor(field.FieldName)} and C# {CSharpAccessorFor(field.FieldName)} for action={field.Action}, mutationKind={field.MutationKind}; preserve schema-v1 type and collection ordering before equality comparison.";
	}

	private static string BlockerFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus status,
		FindGroupMutationPostProjectedRowComparisonValueField field)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus.IgnoredRuntimeContextOnly => "Runtime context field is intentionally ignored for equality.",
			FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus.BlockedReaderNotImplemented => $"Runtime rows may be available, but the value reader for {field.FieldName} is not implemented.",
			_ => $"Cannot read {field.FieldName} until accepted Java runtime rows and C# live boundary rows exist.",
		};
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus.BlockedExecutionGateNotReady => "Value-reader design is blocked until the execution-readiness gate reaches runtime-evidence readiness.",
			_ => "Value-reader design is blocked because Java/C# runtime rows exist only as future inputs and no value reader is implemented.",
		};
	}

	private static string ToPascalCase(string fieldName)
	{
		if (string.IsNullOrEmpty(fieldName))
			return fieldName;

		return char.ToUpperInvariant(fieldName[0]) + fieldName[1..];
	}
}
