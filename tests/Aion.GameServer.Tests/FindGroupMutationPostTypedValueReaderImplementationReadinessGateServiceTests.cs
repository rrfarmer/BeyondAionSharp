using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostTypedValueReaderImplementationReadinessGateServiceTests
{
	[Fact]
	public void Create_DefaultGateBlocksBeforeRuntimeRowValueIntake()
	{
		var gate = FindGroupMutationPostTypedValueReaderImplementationReadinessGateService.Create();

		Assert.Equal(FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.BlockedRuntimeRowValueIntakeNotReady, gate.Status);
		Assert.False(gate.IsLive);
		Assert.True(gate.HasRuntimeRowValueIntake);
		Assert.True(gate.HasImplementationRunbook);
		Assert.True(gate.HasTypedReaderPreflight);
		Assert.False(gate.HasRuntimeRows);
		Assert.True(gate.HasReaderFunctionPlan);
		Assert.Equal(38, gate.TotalEqualityFieldCount);
		Assert.Equal(4, gate.TotalRuntimeContextFieldCount);
		Assert.False(gate.CanImplementReaders);
		Assert.False(gate.CanReadJavaValues);
		Assert.False(gate.CanReadCSharpValues);
		Assert.False(gate.CanCompareValues);
		Assert.False(gate.CanEmitResults);
		Assert.False(gate.CanRunRuntimeComparison);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", gate.TraceName);
		Assert.Contains("addRecruitment/addApplication", gate.JavaSource, StringComparison.Ordinal);
		Assert.Contains("runtime row value intake", gate.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_RuntimeRowsStillBlockWhenRunbookIsNotReady()
	{
		var gate = FindGroupMutationPostTypedValueReaderImplementationReadinessGateService.Create(ReadyRuntimeIntake());

		Assert.Equal(FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.BlockedImplementationRunbookNotReady, gate.Status);
		Assert.True(gate.HasRuntimeRows);
		Assert.False(gate.CanImplementReaders);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.RuntimeRowValueIntake
			&& row.Status == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.ReadyForImplementationInput
			&& row.HasRuntimeRows);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.ImplementationRunbook
			&& row.Status == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.Blocked
			&& row.Evidence.Contains("BlockedReadinessSummaryNotReady", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyInputsNameConcreteReaderFunctionsWithoutImplementation()
	{
		var gate = FindGroupMutationPostTypedValueReaderImplementationReadinessGateService.Create(ReadyRuntimeIntake(), ReadyRunbook());

		Assert.Equal(FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.ReadyForReaderImplementationBlocked, gate.Status);
		Assert.True(gate.HasRuntimeRows);
		Assert.True(gate.HasReaderFunctionPlan);
		Assert.False(gate.CanImplementReaders);
		Assert.False(gate.CanReadJavaValues);
		Assert.False(gate.CanReadCSharpValues);
		Assert.False(gate.CanCompareValues);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.Int32ScalarReader
			&& row.Status == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.Deferred
			&& row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar
			&& row.JavaReaderFunction == "ReadJavaInt32Scalar"
			&& row.CSharpReaderFunction == "ReadCSharpInt32Scalar"
			&& row.RequiredImplementation.Contains("integer readers", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.BooleanScalarReader
			&& row.JavaReaderFunction == "ReadJavaBooleanScalar"
			&& row.CSharpReaderFunction == "ReadCSharpBooleanScalar"
			&& row.RequiredImplementation.Contains("boundary acceptance", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.StringScalarReader
			&& row.JavaReaderFunction == "ReadJavaStringScalar"
			&& row.CSharpReaderFunction == "ReadCSharpStringScalar"
			&& row.RequiredImplementation.Contains("ordinal string readers", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.EnumStringScalarReader
			&& row.JavaReaderFunction == "ReadJavaEnumStringScalar"
			&& row.CSharpReaderFunction == "ReadCSharpEnumStringScalar"
			&& row.RequiredImplementation.Contains("Java JSON enum spelling", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CarriesPerReaderKindCountsFromPreflight()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();

		var gate = FindGroupMutationPostTypedValueReaderImplementationReadinessGateService.Create(ReadyRuntimeIntake(), ReadyRunbook(), preflight);

		Assert.Equal(preflight.Fields.Count(field => field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue), gate.TotalEqualityFieldCount);
		Assert.Equal(preflight.Fields.Count(field => field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext), gate.TotalRuntimeContextFieldCount);
		Assert.Contains(gate.Rows, row =>
			row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar
			&& row.EqualityFieldCount == CountFields(preflight, FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar)
			&& row.RequiresJavaReader
			&& row.RequiresCSharpReader);
		Assert.Contains(gate.Rows, row =>
			row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar
			&& row.EqualityFieldCount == CountFields(preflight, FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar));
		Assert.Contains(gate.Rows, row =>
			row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar
			&& row.EqualityFieldCount == CountFields(preflight, FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar));
		Assert.Contains(gate.Rows, row =>
			row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar
			&& row.EqualityFieldCount == CountFields(preflight, FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar));
	}

	[Fact]
	public void Create_OrderedListAndMismatchContextRemainConservative()
	{
		var gate = FindGroupMutationPostTypedValueReaderImplementationReadinessGateService.Create(ReadyRuntimeIntake(), ReadyRunbook());

		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.OrderedInt32ListReader
			&& row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List
			&& row.PreservesCollectionOrder
			&& row.JavaReaderFunction == "ReadJavaOrderedInt32List"
			&& row.CSharpReaderFunction == "ReadCSharpOrderedInt32List"
			&& row.RequiredImplementation.Contains("preserve Java materialized visible-entry ordering exactly", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.MismatchContextAttachment
			&& row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext
			&& row.EqualityFieldCount == 0
			&& row.RuntimeContextFieldCount == gate.TotalRuntimeContextFieldCount
			&& !row.RequiresJavaReader
			&& !row.RequiresCSharpReader
			&& row.Notes.Contains("must never enable Matched output", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.ReaderExecution
			&& row.Status == FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.Blocked
			&& row.Notes.Contains("no values are read", StringComparison.Ordinal));
	}

	private static int CountFields(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract preflight,
		FindGroupMutationPostProjectedRowComparisonValueReaderKind readerKind) =>
		preflight.Fields.Count(field =>
			field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue
			&& field.ReaderKind == readerKind);

	private static FindGroupMutationPostRuntimeRowValueEvidenceIntakeGate ReadyRuntimeIntake() =>
		new(
			FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.ReadyForRuntimeRowsValueReadersBlocked,
			[
				new FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow(
					1,
					FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.RuntimeValueReadExecution,
					Action: null,
					MutationKind: null,
					FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Blocked,
					HasExpectedShape: true,
					HasRuntimeEvidence: true,
					BlocksValueReaders: true,
					"runtime row values",
					"hasRuntimeRows=True",
					"test intake"),
			],
			HasValueProjectionHandoff: true,
			HasRuntimeEvidenceChecklist: true,
			HasTypedValueReaderPreflight: true,
			HasJavaRuntimeArtifactRows: true,
			HasAcceptedCSharpTraceRows: true,
			HasRuntimeRowValues: true,
			RequiredEqualityReaderFieldCount: 38,
			IgnoredRuntimeContextFieldCount: 4,
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			"Runtime rows are present, but typed value-reader execution is still blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract ReadyRunbook() =>
		new(
			FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReaderImplementationDeferred,
			[
				RunbookStep(
					1,
					FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.TypedScalarEqualityReaders,
					[
						FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar,
						FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar,
					],
					EqualityFieldCount: 26,
					ContextFieldCount: 0,
					PreservesCollectionOrder: false),
				RunbookStep(
					2,
					FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.OrderedListEqualityReaders,
					[FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List],
					EqualityFieldCount: 2,
					ContextFieldCount: 0,
					PreservesCollectionOrder: true),
				RunbookStep(
					3,
					FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.EnumAndStringEqualityReaders,
					[
						FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar,
						FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar,
					],
					EqualityFieldCount: 10,
					ContextFieldCount: 0,
					PreservesCollectionOrder: false),
				RunbookStep(
					4,
					FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.MismatchContextAttachment,
					[FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext],
					EqualityFieldCount: 0,
					ContextFieldCount: 4,
					PreservesCollectionOrder: false),
			],
			TotalEqualityFieldCount: 38,
			TotalContextFieldCount: 4,
			HasValueReaderPreflight: true,
			HasImplementationReadinessChecklist: true,
			HasMismatchContextPreflight: true,
			CanImplementReaders: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanAttachContext: false,
			CanEmitComparisonResult: false,
			"reader implementation deferred",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepRow RunbookStep(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep step,
		IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderKind> readerKinds,
		int EqualityFieldCount,
		int ContextFieldCount,
		bool PreservesCollectionOrder) =>
		new(
			order,
			step,
			step == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.MismatchContextAttachment
				? FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus.BlockedContextAttachmentDeferred
				: FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus.BlockedReaderImplementationDeferred,
			readerKinds,
			EqualityFieldCount,
			ContextFieldCount,
			RequiresJavaReader: EqualityFieldCount > 0,
			RequiresCSharpReader: EqualityFieldCount > 0,
			PreservesCollectionOrder,
			CanImplement: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanAttachContext: false,
			"test implementation order",
			"test prerequisite",
			$"readerKinds={string.Join("/", readerKinds)}",
			"test runbook");
}
