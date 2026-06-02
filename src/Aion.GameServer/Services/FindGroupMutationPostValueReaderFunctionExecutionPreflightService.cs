namespace Aion.GameServer.Services;

public enum FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus
{
	BlockedReaderImplementationGateNotReady,
	BlockedComparatorPreflightNotReady,
	ReadyForFunctionExecutionBlocked,
}

public enum FindGroupMutationPostValueReaderFunctionExecutionPreflightStage
{
	ReaderImplementationGate,
	ComparatorPreflight,
	RowIdentityPairing,
	JavaReaderInvocation,
	CSharpReaderInvocation,
	OrderedListReaderInvocation,
	EqualityProjection,
	MismatchContextAttachment,
	ResultEmission,
}

public enum FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus
{
	Blocked,
	Deferred,
	ReadyForInvocationInput,
}

public sealed record FindGroupMutationPostValueReaderFunctionExecutionPreflightRow(
	int Order,
	FindGroupMutationPostValueReaderFunctionExecutionPreflightStage Stage,
	FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus Status,
	int EqualityFieldCount,
	int RuntimeContextFieldCount,
	IReadOnlyList<string> JavaReaderFunctions,
	IReadOnlyList<string> CSharpReaderFunctions,
	bool RequiresAcceptedJavaRows,
	bool RequiresAcceptedCSharpRows,
	bool RequiresReaderFunctions,
	bool RequiresProjectedValues,
	bool RequiresResultSchema,
	bool CanInvokeReaderFunctions,
	bool CanReadValues,
	bool CanCompareValues,
	bool CanEmitResults,
	string RequiredPrecondition,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostValueReaderFunctionExecutionPreflight(
	FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus Status,
	IReadOnlyList<FindGroupMutationPostValueReaderFunctionExecutionPreflightRow> Rows,
	bool HasTypedReaderImplementationGate,
	bool HasComparatorPreflight,
	bool HasReaderFunctionPlan,
	bool HasComparatorStages,
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
/// Java parity breadcrumb: non-live preflight for invoking future CM_FIND_GROUP
/// action 2/6 value-reader functions. It records invocation preconditions but
/// never calls reader functions or projects values.
/// </summary>
public static class FindGroupMutationPostValueReaderFunctionExecutionPreflightService
{
	public static FindGroupMutationPostValueReaderFunctionExecutionPreflight Create(
		FindGroupMutationPostTypedValueReaderImplementationReadinessGate? typedReaderGate = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract? comparatorPreflight = null)
	{
		typedReaderGate ??= FindGroupMutationPostTypedValueReaderImplementationReadinessGateService.Create();
		comparatorPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService.Create();

		var gateReady = typedReaderGate.Status == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.ReadyForReaderImplementationBlocked;
		var comparatorReady = comparatorPreflight.Status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedComparatorImplementationDeferred;
		var status = DetermineStatus(gateReady, comparatorReady);
		var javaFunctions = typedReaderGate.Rows
			.Where(row => row.RequiresJavaReader && !string.IsNullOrWhiteSpace(row.JavaReaderFunction))
			.Select(row => row.JavaReaderFunction)
			.Distinct()
			.ToArray();
		var csharpFunctions = typedReaderGate.Rows
			.Where(row => row.RequiresCSharpReader && !string.IsNullOrWhiteSpace(row.CSharpReaderFunction))
			.Select(row => row.CSharpReaderFunction)
			.Distinct()
			.ToArray();
		var orderedJavaFunctions = typedReaderGate.Rows
			.Where(row => row.PreservesCollectionOrder && !string.IsNullOrWhiteSpace(row.JavaReaderFunction))
			.Select(row => row.JavaReaderFunction)
			.Distinct()
			.ToArray();
		var orderedCSharpFunctions = typedReaderGate.Rows
			.Where(row => row.PreservesCollectionOrder && !string.IsNullOrWhiteSpace(row.CSharpReaderFunction))
			.Select(row => row.CSharpReaderFunction)
			.Distinct()
			.ToArray();
		var rows = new[]
		{
			ReaderGateRow(typedReaderGate, gateReady),
			ComparatorPreflightRow(comparatorPreflight, comparatorReady),
			StageRow(
				3,
				FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.RowIdentityPairing,
				status,
				comparatorPreflight,
				JavaReaderFunctions: [],
				CSharpReaderFunctions: [],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresReaderFunctions: false,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Pair runtime Java and C# rows by action, mutationKind, activePlayerObjectId, and mutatedEntryObjectId before invoking any reader function.",
				"Java action 2 maps to Recruitment/addRecruitment and refreshed SM_FIND_GROUP action 0; Java action 6 maps to Application/addApplication and refreshed SM_FIND_GROUP action 4."),
			StageRow(
				4,
				FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.JavaReaderInvocation,
				status,
				comparatorPreflight,
				javaFunctions,
				CSharpReaderFunctions: [],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: false,
				RequiresReaderFunctions: true,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Invoke planned Java readers only against runtime-backed Java schema-v1 artifact rows.",
				"Java reader functions are planned names only and must not consume checked-in shape-only artifacts as runtime evidence."),
			StageRow(
				5,
				FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.CSharpReaderInvocation,
				status,
				comparatorPreflight,
				JavaReaderFunctions: [],
				csharpFunctions,
				RequiresAcceptedJavaRows: false,
				RequiresAcceptedCSharpRows: true,
				RequiresReaderFunctions: true,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Invoke planned C# readers only against accepted live boundary trace rows from production ProcessPacketAsync.",
				"C# reader functions are planned names only and must not consume disabled projections as live evidence."),
			StageRow(
				6,
				FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.OrderedListReaderInvocation,
				status,
				comparatorPreflight,
				orderedJavaFunctions,
				orderedCSharpFunctions,
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresReaderFunctions: true,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Invoke ordered-list readers only when both sides can preserve visibleEntryObjectIdsAfterMutation ordering exactly.",
				"Java refreshed-list materialized packet order must be preserved before equality projection."),
			StageRow(
				7,
				FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.EqualityProjection,
				status,
				comparatorPreflight,
				javaFunctions,
				csharpFunctions,
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresReaderFunctions: true,
				RequiresProjectedValues: true,
				RequiresResultSchema: true,
				"Project every required equality value only after all planned Java and C# reader functions are implemented and invocation inputs exist.",
				"Projection stays blocked; this preflight emits no javaValue/csharpValue pairs."),
			StageRow(
				8,
				FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.MismatchContextAttachment,
				status,
				comparatorPreflight,
				typedReaderGate.Rows
					.Where(row => row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext && !string.IsNullOrWhiteSpace(row.JavaReaderFunction))
					.Select(row => row.JavaReaderFunction)
					.Distinct()
					.ToArray(),
				typedReaderGate.Rows
					.Where(row => row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext && !string.IsNullOrWhiteSpace(row.CSharpReaderFunction))
					.Select(row => row.CSharpReaderFunction)
					.Distinct()
					.ToArray(),
				RequiresAcceptedJavaRows: false,
				RequiresAcceptedCSharpRows: false,
				RequiresReaderFunctions: false,
				RequiresProjectedValues: false,
				RequiresResultSchema: true,
				"Attach mismatch context only after MissingJavaRow, MissingCSharpRow, or FieldMismatch output exists.",
				"Mismatch context is not an equality input and must never enable Matched output."),
			StageRow(
				9,
				FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.ResultEmission,
				status,
				comparatorPreflight,
				JavaReaderFunctions: [],
				CSharpReaderFunctions: [],
				RequiresAcceptedJavaRows: true,
				RequiresAcceptedCSharpRows: true,
				RequiresReaderFunctions: true,
				RequiresProjectedValues: true,
				RequiresResultSchema: true,
				"Emit results only after row pairing, reader invocation, equality projection, comparison, result selection, and optional context attachment complete.",
				"Result emission remains blocked; no Matched, missing-row, FieldMismatch, or ignored-context rows are materialized."),
		};

		return new FindGroupMutationPostValueReaderFunctionExecutionPreflight(
			status,
			rows,
			HasTypedReaderImplementationGate: typedReaderGate.Rows.Count > 0,
			HasComparatorPreflight: comparatorPreflight.Stages.Count > 0,
			HasReaderFunctionPlan: javaFunctions.Length > 0 && csharpFunctions.Length > 0,
			HasComparatorStages: comparatorPreflight.Stages.Count > 0,
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
			typedReaderGate.TraceName,
			typedReaderGate.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus DetermineStatus(
		bool gateReady,
		bool comparatorReady)
	{
		if (!gateReady)
			return FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.BlockedReaderImplementationGateNotReady;

		if (!comparatorReady)
			return FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.BlockedComparatorPreflightNotReady;

		return FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.ReadyForFunctionExecutionBlocked;
	}

	private static FindGroupMutationPostValueReaderFunctionExecutionPreflightRow ReaderGateRow(
		FindGroupMutationPostTypedValueReaderImplementationReadinessGate gate,
		bool ready) =>
		new(
			1,
			FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.ReaderImplementationGate,
			ready ? FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus.ReadyForInvocationInput : FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus.Blocked,
			gate.TotalEqualityFieldCount,
			gate.TotalRuntimeContextFieldCount,
			gate.Rows.Where(row => !string.IsNullOrWhiteSpace(row.JavaReaderFunction)).Select(row => row.JavaReaderFunction).Distinct().ToArray(),
			gate.Rows.Where(row => !string.IsNullOrWhiteSpace(row.CSharpReaderFunction)).Select(row => row.CSharpReaderFunction).Distinct().ToArray(),
			RequiresAcceptedJavaRows: true,
			RequiresAcceptedCSharpRows: true,
			RequiresReaderFunctions: true,
			RequiresProjectedValues: false,
			RequiresResultSchema: false,
			CanInvokeReaderFunctions: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			"Typed-reader implementation gate must be ready for reader function planning before invocation preconditions can be evaluated.",
			$"status={gate.Status}; hasRuntimeRows={gate.HasRuntimeRows}; hasReaderFunctionPlan={gate.HasReaderFunctionPlan}; canReadJavaValues={gate.CanReadJavaValues}; canReadCSharpValues={gate.CanReadCSharpValues}",
			"Reader function names are metadata only and no functions are invoked.");

	private static FindGroupMutationPostValueReaderFunctionExecutionPreflightRow ComparatorPreflightRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract comparator,
		bool ready) =>
		new(
			2,
			FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.ComparatorPreflight,
			ready ? FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus.ReadyForInvocationInput : FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus.Blocked,
			comparator.EqualityFieldCount,
			comparator.RuntimeContextFieldCount,
			JavaReaderFunctions: [],
			CSharpReaderFunctions: [],
			RequiresAcceptedJavaRows: true,
			RequiresAcceptedCSharpRows: true,
			RequiresReaderFunctions: true,
			RequiresProjectedValues: true,
			RequiresResultSchema: true,
			CanInvokeReaderFunctions: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			"Comparator preflight must reach deferred comparator implementation readiness before reader invocation can be planned.",
			$"status={comparator.Status}; stages={comparator.Stages.Count}; equalityFields={comparator.EqualityFieldCount}; runtimeContextFields={comparator.RuntimeContextFieldCount}; canProjectValues={comparator.CanProjectValues}; canCompareValues={comparator.CanCompareValues}",
			"Comparator preflight names stages but does not project or compare values.");

	private static FindGroupMutationPostValueReaderFunctionExecutionPreflightRow StageRow(
		int order,
		FindGroupMutationPostValueReaderFunctionExecutionPreflightStage stage,
		FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus status,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract comparator,
		IReadOnlyList<string> JavaReaderFunctions,
		IReadOnlyList<string> CSharpReaderFunctions,
		bool RequiresAcceptedJavaRows,
		bool RequiresAcceptedCSharpRows,
		bool RequiresReaderFunctions,
		bool RequiresProjectedValues,
		bool RequiresResultSchema,
		string requiredPrecondition,
		string notes) =>
		new(
			order,
			stage,
			status == FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.ReadyForFunctionExecutionBlocked
				? FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus.Deferred
				: FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus.Blocked,
			comparator.EqualityFieldCount,
			comparator.RuntimeContextFieldCount,
			JavaReaderFunctions,
			CSharpReaderFunctions,
			RequiresAcceptedJavaRows,
			RequiresAcceptedCSharpRows,
			RequiresReaderFunctions,
			RequiresProjectedValues,
			RequiresResultSchema,
			CanInvokeReaderFunctions: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			requiredPrecondition,
			$"comparatorStatus={comparator.Status}; equalityFields={comparator.EqualityFieldCount}; runtimeContextFields={comparator.RuntimeContextFieldCount}; javaFunctions={string.Join("/", JavaReaderFunctions)}; csharpFunctions={string.Join("/", CSharpReaderFunctions)}; canProjectValues={comparator.CanProjectValues}; canEmitResults={comparator.CanEmitResults}",
			notes);

	private static string DecisionFor(FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus status)
	{
		return status switch
		{
			FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.BlockedReaderImplementationGateNotReady => "Value-reader function execution preflight is blocked until typed-reader implementation gate metadata is ready.",
			FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.BlockedComparatorPreflightNotReady => "Value-reader function execution preflight is blocked until comparator preflight metadata is ready.",
			_ => "Value-reader function execution preflight is ready to name invocation blockers, but reader invocation, value projection, comparison, result emission, and verified parity remain blocked.",
		};
	}
}
