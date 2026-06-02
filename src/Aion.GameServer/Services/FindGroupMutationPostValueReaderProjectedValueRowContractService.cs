namespace Aion.GameServer.Services;

public enum FindGroupMutationPostValueReaderProjectedValueRowContractStatus
{
	BlockedFunctionExecutionPreflightNotReady,
	BlockedExecutorImplementationPlanNotReady,
	ReadyForProjectedRowsBlocked,
}

public enum FindGroupMutationPostValueReaderProjectedValueRowStatus
{
	BlockedFunctionExecutionPreflightNotReady,
	BlockedExecutorImplementationPlanNotReady,
	BlockedReaderInvocationDeferred,
	IgnoredRuntimeContextOnly,
}

public enum FindGroupMutationPostValueReaderProjectedValueReadStatus
{
	NotRead,
	IgnoredRuntimeContext,
}

public sealed record FindGroupMutationPostValueReaderProjectedValueRow(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostProjectedRowComparisonValueReaderKind ReaderKind,
	FindGroupMutationPostProjectedRowComparisonValueReadMode ReadMode,
	FindGroupMutationPostValueReaderProjectedValueRowStatus Status,
	FindGroupMutationPostValueReaderProjectedValueReadStatus JavaReadStatus,
	FindGroupMutationPostValueReaderProjectedValueReadStatus CSharpReadStatus,
	string ValueType,
	string JavaReaderFunction,
	string CSharpReaderFunction,
	string JavaValue,
	string CSharpValue,
	bool RequiresJavaRow,
	bool RequiresCSharpRow,
	bool RequiresReaderFunctions,
	bool PreservesCollectionOrder,
	bool CanReadJavaValue,
	bool CanReadCSharpValue,
	bool CanCompareValue,
	bool CanEmitResult,
	string Blocker,
	string JavaSource,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostValueReaderProjectedValueRowContract(
	FindGroupMutationPostValueReaderProjectedValueRowContractStatus Status,
	IReadOnlyList<FindGroupMutationPostValueReaderProjectedValueRow> Rows,
	bool HasFunctionExecutionPreflight,
	bool HasExecutorImplementationPlan,
	bool HasTypedValueReaderPreflight,
	bool HasProjectedValueRows,
	int RequiredEqualityFieldCount,
	int IgnoredRuntimeContextFieldCount,
	bool CanInvokeReaderFunctions,
	bool CanReadJavaValues,
	bool CanReadCSharpValues,
	bool CanProjectValues,
	bool CanCompareValues,
	bool CanAttachRuntimeContext,
	bool CanEmitResults,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live projected-value row contract for future
/// CM_FIND_GROUP action 2/6 reader execution. It defines per-field Java/C#
/// value rows but never invokes readers, reads values, compares, or emits
/// results.
/// </summary>
public static class FindGroupMutationPostValueReaderProjectedValueRowContractService
{
	private const string NotReadValue = "<not-read>";
	private const string IgnoredRuntimeContextValue = "<ignored-runtime-context>";

	public static FindGroupMutationPostValueReaderProjectedValueRowContract Create(
		FindGroupMutationPostValueReaderFunctionExecutionPreflight? functionExecutionPreflight = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract? executorImplementationPlan = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract? typedValueReaderPreflight = null)
	{
		functionExecutionPreflight ??= FindGroupMutationPostValueReaderFunctionExecutionPreflightService.Create();
		executorImplementationPlan ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService.Create();
		typedValueReaderPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();

		var status = StatusFor(functionExecutionPreflight, executorImplementationPlan);
		var rows = typedValueReaderPreflight.Fields
			.Select(field => CreateRow(field, status, functionExecutionPreflight, executorImplementationPlan))
			.ToArray();

		return new FindGroupMutationPostValueReaderProjectedValueRowContract(
			status,
			rows,
			HasFunctionExecutionPreflight: functionExecutionPreflight.Rows.Count > 0,
			HasExecutorImplementationPlan: executorImplementationPlan.Steps.Count > 0,
			HasTypedValueReaderPreflight: typedValueReaderPreflight.Fields.Count > 0,
			HasProjectedValueRows: rows.Length > 0,
			RequiredEqualityFieldCount: rows.Count(row => row.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue),
			IgnoredRuntimeContextFieldCount: rows.Count(row => row.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext),
			CanInvokeReaderFunctions: false,
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanAttachRuntimeContext: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			functionExecutionPreflight.TraceName,
			functionExecutionPreflight.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostValueReaderProjectedValueRowContractStatus StatusFor(
		FindGroupMutationPostValueReaderFunctionExecutionPreflight functionExecutionPreflight,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract executorImplementationPlan)
	{
		if (functionExecutionPreflight.Status != FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.ReadyForFunctionExecutionBlocked)
			return FindGroupMutationPostValueReaderProjectedValueRowContractStatus.BlockedFunctionExecutionPreflightNotReady;

		if (executorImplementationPlan.Status != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus.BlockedExecutorImplementationDeferred)
			return FindGroupMutationPostValueReaderProjectedValueRowContractStatus.BlockedExecutorImplementationPlanNotReady;

		return FindGroupMutationPostValueReaderProjectedValueRowContractStatus.ReadyForProjectedRowsBlocked;
	}

	private static FindGroupMutationPostValueReaderProjectedValueRow CreateRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField field,
		FindGroupMutationPostValueReaderProjectedValueRowContractStatus contractStatus,
		FindGroupMutationPostValueReaderFunctionExecutionPreflight functionExecutionPreflight,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract executorImplementationPlan)
	{
		var ignoredContext = field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext;
		var rowStatus = RowStatusFor(field, contractStatus);
		var javaReaderFunction = JavaReaderFunctionFor(field.ReaderKind);
		var csharpReaderFunction = CSharpReaderFunctionFor(field.ReaderKind);
		var functionPreflightRows = functionExecutionPreflight.Rows.Count == 0
			? "none"
			: string.Join(" | ", functionExecutionPreflight.Rows.Select(row => $"{row.Stage}={row.Evidence}"));

		return new FindGroupMutationPostValueReaderProjectedValueRow(
			field.Order,
			field.Action,
			field.MutationKind,
			field.FieldName,
			field.ReaderKind,
			field.ReadMode,
			rowStatus,
			ignoredContext ? FindGroupMutationPostValueReaderProjectedValueReadStatus.IgnoredRuntimeContext : FindGroupMutationPostValueReaderProjectedValueReadStatus.NotRead,
			ignoredContext ? FindGroupMutationPostValueReaderProjectedValueReadStatus.IgnoredRuntimeContext : FindGroupMutationPostValueReaderProjectedValueReadStatus.NotRead,
			field.ExpectedClrType,
			javaReaderFunction,
			csharpReaderFunction,
			ignoredContext ? IgnoredRuntimeContextValue : NotReadValue,
			ignoredContext ? IgnoredRuntimeContextValue : NotReadValue,
			RequiresJavaRow: field.RequiresJavaReader,
			RequiresCSharpRow: field.RequiresCSharpReader,
			RequiresReaderFunctions: field.RequiresJavaReader || field.RequiresCSharpReader,
			field.PreservesCollectionOrder,
			CanReadJavaValue: false,
			CanReadCSharpValue: false,
			CanCompareValue: false,
			CanEmitResult: false,
			BlockerFor(rowStatus, field),
			"CM_FIND_GROUP.runImpl action 2 -> FindGroupService.addRecruitment; action 6 -> FindGroupService.addApplication",
			$"functionPreflightStatus={functionExecutionPreflight.Status}; executorPlanStatus={executorImplementationPlan.Status}; fieldStatus={field.Status}; javaJsonPath={field.JavaJsonPath}; csharpAccessor={field.CSharpAccessor}; valueType={field.ExpectedClrType}; functionPreflightRows={functionPreflightRows}",
			NotesFor(field));
	}

	private static FindGroupMutationPostValueReaderProjectedValueRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField field,
		FindGroupMutationPostValueReaderProjectedValueRowContractStatus contractStatus)
	{
		if (field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext)
			return FindGroupMutationPostValueReaderProjectedValueRowStatus.IgnoredRuntimeContextOnly;

		return contractStatus switch
		{
			FindGroupMutationPostValueReaderProjectedValueRowContractStatus.BlockedFunctionExecutionPreflightNotReady => FindGroupMutationPostValueReaderProjectedValueRowStatus.BlockedFunctionExecutionPreflightNotReady,
			FindGroupMutationPostValueReaderProjectedValueRowContractStatus.BlockedExecutorImplementationPlanNotReady => FindGroupMutationPostValueReaderProjectedValueRowStatus.BlockedExecutorImplementationPlanNotReady,
			_ => FindGroupMutationPostValueReaderProjectedValueRowStatus.BlockedReaderInvocationDeferred,
		};
	}

	private static string JavaReaderFunctionFor(FindGroupMutationPostProjectedRowComparisonValueReaderKind readerKind)
	{
		return readerKind switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar => "ReadJavaBooleanScalar",
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List => "ReadJavaOrderedInt32List",
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar => "ReadJavaStringScalar",
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar => "ReadJavaEnumStringScalar",
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext => "AttachJavaMismatchContext",
			_ => "ReadJavaInt32Scalar",
		};
	}

	private static string CSharpReaderFunctionFor(FindGroupMutationPostProjectedRowComparisonValueReaderKind readerKind)
	{
		return readerKind switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar => "ReadCSharpBooleanScalar",
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List => "ReadCSharpOrderedInt32List",
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar => "ReadCSharpStringScalar",
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar => "ReadCSharpEnumStringScalar",
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext => "AttachCSharpMismatchContext",
			_ => "ReadCSharpInt32Scalar",
		};
	}

	private static string BlockerFor(
		FindGroupMutationPostValueReaderProjectedValueRowStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField field)
	{
		return status switch
		{
			FindGroupMutationPostValueReaderProjectedValueRowStatus.IgnoredRuntimeContextOnly => $"{field.FieldName} is runtime context and cannot participate in equality or create a standalone result.",
			FindGroupMutationPostValueReaderProjectedValueRowStatus.BlockedFunctionExecutionPreflightNotReady => $"Projected value row for {field.FieldName} is blocked until value-reader function execution preflight is ready.",
			FindGroupMutationPostValueReaderProjectedValueRowStatus.BlockedExecutorImplementationPlanNotReady => $"Projected value row for {field.FieldName} is blocked until the value-reader executor implementation plan is ready.",
			_ => $"Projected value row for {field.FieldName} is defined, but Java/C# reader invocation, value reads, comparison, and result emission remain deferred.",
		};
	}

	private static string NotesFor(FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField field)
	{
		if (field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext)
			return "Diagnostic context may attach only after MissingJavaRow, MissingCSharpRow, or FieldMismatch exists; it must never enable Matched output.";

		if (field.PreservesCollectionOrder)
			return "Future projected value must preserve Java refreshed-list materialized order before equality comparison.";

		return "Projected value row is shape-only and intentionally carries no Java or C# runtime value.";
	}

	private static string DecisionFor(FindGroupMutationPostValueReaderProjectedValueRowContractStatus status)
	{
		return status switch
		{
			FindGroupMutationPostValueReaderProjectedValueRowContractStatus.BlockedFunctionExecutionPreflightNotReady => "Projected-value row contract is blocked until value-reader function execution preflight reaches deferred invocation readiness.",
			FindGroupMutationPostValueReaderProjectedValueRowContractStatus.BlockedExecutorImplementationPlanNotReady => "Projected-value row contract is blocked until the value-reader executor implementation plan reaches deferred implementation readiness.",
			_ => "Projected-value row contract is defined, but every Java/C# value remains unread and comparison, materialization, result emission, runtime comparison, and verified parity remain blocked.",
		};
	}
}
