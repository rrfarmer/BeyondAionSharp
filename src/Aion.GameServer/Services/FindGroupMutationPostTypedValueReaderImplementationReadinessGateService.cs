namespace Aion.GameServer.Services;

public enum FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus
{
	BlockedRuntimeRowValueIntakeNotReady,
	BlockedImplementationRunbookNotReady,
	ReadyForReaderImplementationBlocked,
}

public enum FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage
{
	RuntimeRowValueIntake,
	ImplementationRunbook,
	Int32ScalarReader,
	BooleanScalarReader,
	OrderedInt32ListReader,
	StringScalarReader,
	EnumStringScalarReader,
	MismatchContextAttachment,
	ReaderExecution,
}

public enum FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus
{
	Blocked,
	Deferred,
	ReadyForImplementationInput,
}

public sealed record FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow(
	int Order,
	FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage Stage,
	FindGroupMutationPostProjectedRowComparisonValueReaderKind? ReaderKind,
	FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus Status,
	int EqualityFieldCount,
	int RuntimeContextFieldCount,
	bool HasRuntimeRows,
	bool HasRunbookStep,
	bool RequiresJavaReader,
	bool RequiresCSharpReader,
	bool PreservesCollectionOrder,
	bool CanImplement,
	bool CanReadValues,
	bool CanCompareValues,
	string JavaReaderFunction,
	string CSharpReaderFunction,
	string RequiredImplementation,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostTypedValueReaderImplementationReadinessGate(
	FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus Status,
	IReadOnlyList<FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow> Rows,
	bool HasRuntimeRowValueIntake,
	bool HasImplementationRunbook,
	bool HasTypedReaderPreflight,
	bool HasRuntimeRows,
	bool HasReaderFunctionPlan,
	int TotalEqualityFieldCount,
	int TotalRuntimeContextFieldCount,
	bool CanImplementReaders,
	bool CanReadJavaValues,
	bool CanReadCSharpValues,
	bool CanCompareValues,
	bool CanEmitResults,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live implementation gate for future CM_FIND_GROUP
/// action 2/6 typed value readers. It names concrete reader functions for
/// schema-v1 equality fields, but it never implements or runs those readers.
/// </summary>
public static class FindGroupMutationPostTypedValueReaderImplementationReadinessGateService
{
	public static FindGroupMutationPostTypedValueReaderImplementationReadinessGate Create(
		FindGroupMutationPostRuntimeRowValueEvidenceIntakeGate? runtimeRowValueIntake = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract? implementationRunbook = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract? typedReaderPreflight = null)
	{
		runtimeRowValueIntake ??= FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateService.Create();
		implementationRunbook ??= FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService.Create();
		typedReaderPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();

		var runtimeReady = runtimeRowValueIntake.Status == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.ReadyForRuntimeRowsValueReadersBlocked
			&& runtimeRowValueIntake.HasJavaRuntimeArtifactRows
			&& runtimeRowValueIntake.HasAcceptedCSharpTraceRows
			&& runtimeRowValueIntake.HasRuntimeRowValues;
		var runbookReady = implementationRunbook.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReaderImplementationDeferred;
		var status = DetermineStatus(runtimeReady, runbookReady);
		var readerRows = new[]
		{
			IntakeRow(runtimeRowValueIntake, runtimeReady),
			RunbookRow(implementationRunbook, runbookReady),
			ReaderRow(
				3,
				FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.Int32ScalarReader,
				FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar,
				typedReaderPreflight,
				implementationRunbook,
				status,
				"ReadJavaInt32Scalar",
				"ReadCSharpInt32Scalar",
				"Implement integer readers for ids, action codes, packet ids, count fields, and other schema-v1 integer equality values."),
			ReaderRow(
				4,
				FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.BooleanScalarReader,
				FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar,
				typedReaderPreflight,
				implementationRunbook,
				status,
				"ReadJavaBooleanScalar",
				"ReadCSharpBooleanScalar",
				"Implement boolean readers for boundary acceptance, mutation ordering, executor observation, and registry ordering fields."),
			ReaderRow(
				5,
				FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.OrderedInt32ListReader,
				FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List,
				typedReaderPreflight,
				implementationRunbook,
				status,
				"ReadJavaOrderedInt32List",
				"ReadCSharpOrderedInt32List",
				"Implement ordered integer-list readers that preserve Java materialized visible-entry ordering exactly."),
			ReaderRow(
				6,
				FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.StringScalarReader,
				FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar,
				typedReaderPreflight,
				implementationRunbook,
				status,
				"ReadJavaStringScalar",
				"ReadCSharpStringScalar",
				"Implement ordinal string readers for trace name, race, and packet type names without culture-sensitive comparison."),
			ReaderRow(
				7,
				FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.EnumStringScalarReader,
				FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar,
				typedReaderPreflight,
				implementationRunbook,
				status,
				"ReadJavaEnumStringScalar",
				"ReadCSharpEnumStringScalar",
				"Implement enum-name readers that preserve Java JSON enum spelling for mutation kind values."),
			MismatchContextRow(8, typedReaderPreflight, implementationRunbook, status),
			ExecutionRow(9, runtimeRowValueIntake, implementationRunbook, status),
		};

		return new FindGroupMutationPostTypedValueReaderImplementationReadinessGate(
			status,
			readerRows,
			HasRuntimeRowValueIntake: runtimeRowValueIntake.Rows.Count > 0,
			HasImplementationRunbook: implementationRunbook.Steps.Count > 0,
			HasTypedReaderPreflight: typedReaderPreflight.Fields.Count > 0,
			HasRuntimeRows: runtimeReady,
			HasReaderFunctionPlan: readerRows.Any(row => row.ReaderKind is not null && !string.IsNullOrWhiteSpace(row.JavaReaderFunction) && !string.IsNullOrWhiteSpace(row.CSharpReaderFunction)),
			TotalEqualityFieldCount: typedReaderPreflight.Fields.Count(IsRequiredEqualityField),
			TotalRuntimeContextFieldCount: typedReaderPreflight.Fields.Count(field => field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext),
			CanImplementReaders: false,
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			runtimeRowValueIntake.TraceName,
			runtimeRowValueIntake.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus DetermineStatus(
		bool runtimeReady,
		bool runbookReady)
	{
		if (!runtimeReady)
			return FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.BlockedRuntimeRowValueIntakeNotReady;

		if (!runbookReady)
			return FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.BlockedImplementationRunbookNotReady;

		return FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.ReadyForReaderImplementationBlocked;
	}

	private static FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow IntakeRow(
		FindGroupMutationPostRuntimeRowValueEvidenceIntakeGate intake,
		bool ready) =>
		new(
			1,
			FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.RuntimeRowValueIntake,
			ReaderKind: null,
			ready ? FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.ReadyForImplementationInput : FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.Blocked,
			EqualityFieldCount: intake.RequiredEqualityReaderFieldCount,
			RuntimeContextFieldCount: intake.IgnoredRuntimeContextFieldCount,
			HasRuntimeRows: ready,
			HasRunbookStep: false,
			RequiresJavaReader: true,
			RequiresCSharpReader: true,
			PreservesCollectionOrder: false,
			CanImplement: false,
			CanReadValues: false,
			CanCompareValues: false,
			JavaReaderFunction: string.Empty,
			CSharpReaderFunction: string.Empty,
			"Runtime-backed Java artifact rows, accepted C# boundary rows, and runtime row values must exist before reader implementation can proceed.",
			$"status={intake.Status}; hasJavaRuntimeRows={intake.HasJavaRuntimeArtifactRows}; hasAcceptedCSharpRows={intake.HasAcceptedCSharpTraceRows}; hasRuntimeRowValues={intake.HasRuntimeRowValues}; canReadJavaValues={intake.CanReadJavaValues}; canReadCSharpValues={intake.CanReadCSharpValues}",
			"Runtime row intake remains a gate; it does not implement or execute readers.");

	private static FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow RunbookRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract runbook,
		bool ready) =>
		new(
			2,
			FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.ImplementationRunbook,
			ReaderKind: null,
			ready ? FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.ReadyForImplementationInput : FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.Blocked,
			runbook.TotalEqualityFieldCount,
			runbook.TotalContextFieldCount,
			HasRuntimeRows: false,
			HasRunbookStep: runbook.Steps.Count > 0,
			RequiresJavaReader: true,
			RequiresCSharpReader: true,
			PreservesCollectionOrder: runbook.Steps.Any(step => step.PreservesCollectionOrder),
			CanImplement: false,
			CanReadValues: false,
			CanCompareValues: false,
			JavaReaderFunction: string.Empty,
			CSharpReaderFunction: string.Empty,
			"Reader implementation runbook must reach deferred implementation readiness before concrete function planning can proceed.",
			$"status={runbook.Status}; equalityFields={runbook.TotalEqualityFieldCount}; contextFields={runbook.TotalContextFieldCount}; canImplementReaders={runbook.CanImplementReaders}; canReadValues={runbook.CanReadValues}",
			"Runbook ordering exists as metadata only; reader functions below are planned, not implemented.");

	private static FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow ReaderRow(
		int order,
		FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage stage,
		FindGroupMutationPostProjectedRowComparisonValueReaderKind readerKind,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract preflight,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract runbook,
		FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus gateStatus,
		string javaReaderFunction,
		string csharpReaderFunction,
		string requiredImplementation)
	{
		var fieldCount = preflight.Fields.Count(field => IsRequiredEqualityField(field) && field.ReaderKind == readerKind);
		var step = RunbookStepFor(runbook, readerKind);
		return new FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow(
			order,
			stage,
			readerKind,
			RowStatusFor(gateStatus),
			fieldCount,
			RuntimeContextFieldCount: 0,
			HasRuntimeRows: gateStatus != FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.BlockedRuntimeRowValueIntakeNotReady,
			HasRunbookStep: step is not null,
			RequiresJavaReader: fieldCount > 0,
			RequiresCSharpReader: fieldCount > 0,
			PreservesCollectionOrder: readerKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List,
			CanImplement: false,
			CanReadValues: false,
			CanCompareValues: false,
			javaReaderFunction,
			csharpReaderFunction,
			requiredImplementation,
			$"preflightStatus={preflight.Status}; readerKind={readerKind}; equalityFields={fieldCount}; runbookStep={step?.Step.ToString() ?? "missing"}; runbookStatus={runbook.Status}; canReadJavaValues={preflight.CanReadJavaValues}; canReadCSharpValues={preflight.CanReadCSharpValues}",
			"Function names are implementation targets only; this gate does not read Java JSON, C# trace exports, or equality values.");
	}

	private static FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow MismatchContextRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract preflight,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract runbook,
		FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus gateStatus)
	{
		var contextCount = preflight.Fields.Count(field => field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext);
		var step = RunbookStepFor(runbook, FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext);
		return new FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow(
			order,
			FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.MismatchContextAttachment,
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext,
			RowStatusFor(gateStatus),
			EqualityFieldCount: 0,
			contextCount,
			HasRuntimeRows: false,
			HasRunbookStep: step is not null,
			RequiresJavaReader: false,
			RequiresCSharpReader: false,
			PreservesCollectionOrder: false,
			CanImplement: false,
			CanReadValues: false,
			CanCompareValues: false,
			"AttachJavaMismatchContext",
			"AttachCSharpMismatchContext",
			"Attach runtime-only mismatch context only after real MissingJavaRow, MissingCSharpRow, or FieldMismatch output exists.",
			$"contextFields={contextCount}; runbookStep={step?.Step.ToString() ?? "missing"}; runbookStatus={runbook.Status}",
			"Context attachment is not an equality reader and must never enable Matched output.");
	}

	private static FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow ExecutionRow(
		int order,
		FindGroupMutationPostRuntimeRowValueEvidenceIntakeGate intake,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract runbook,
		FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus gateStatus) =>
		new(
			order,
			FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.ReaderExecution,
			ReaderKind: null,
			FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.Blocked,
			runbook.TotalEqualityFieldCount,
			runbook.TotalContextFieldCount,
			HasRuntimeRows: gateStatus != FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.BlockedRuntimeRowValueIntakeNotReady,
			HasRunbookStep: runbook.Steps.Count > 0,
			RequiresJavaReader: true,
			RequiresCSharpReader: true,
			PreservesCollectionOrder: true,
			CanImplement: false,
			CanReadValues: false,
			CanCompareValues: false,
			JavaReaderFunction: string.Empty,
			CSharpReaderFunction: string.Empty,
			"Implement and run typed readers only after runtime row values and runbook readiness are both present.",
			$"intakeStatus={intake.Status}; runbookStatus={runbook.Status}; canImplementReaders={runbook.CanImplementReaders}; canReadValues={runbook.CanReadValues}; canCompareValues={runbook.CanCompareValues}",
			"Reader execution remains blocked; no values are read and no parity is verified.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepRow? RunbookStepFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract runbook,
		FindGroupMutationPostProjectedRowComparisonValueReaderKind readerKind) =>
		runbook.Steps.FirstOrDefault(step => step.ReaderKinds.Contains(readerKind));

	private static FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus RowStatusFor(
		FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus gateStatus) =>
		gateStatus == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.ReadyForReaderImplementationBlocked
			? FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.Deferred
			: FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.Blocked;

	private static bool IsRequiredEqualityField(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightField field) =>
		field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue;

	private static string DecisionFor(FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus status)
	{
		return status switch
		{
			FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.BlockedRuntimeRowValueIntakeNotReady => "Typed value-reader implementation is blocked until runtime row value intake has Java rows, accepted C# rows, and runtime row values.",
			FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.BlockedImplementationRunbookNotReady => "Typed value-reader implementation is blocked until the implementation runbook reaches deferred implementation readiness.",
			_ => "Typed value-reader function planning is ready, but reader implementation, value reads, comparison, result emission, and verified parity remain blocked.",
		};
	}
}
