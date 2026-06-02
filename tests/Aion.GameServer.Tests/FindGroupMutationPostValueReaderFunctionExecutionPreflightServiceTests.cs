using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostValueReaderFunctionExecutionPreflightServiceTests
{
	[Fact]
	public void Create_DefaultPreflightBlocksBeforeReaderImplementationGate()
	{
		var preflight = FindGroupMutationPostValueReaderFunctionExecutionPreflightService.Create();

		Assert.Equal(FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.BlockedReaderImplementationGateNotReady, preflight.Status);
		Assert.False(preflight.IsLive);
		Assert.True(preflight.HasTypedReaderImplementationGate);
		Assert.True(preflight.HasComparatorPreflight);
		Assert.True(preflight.HasReaderFunctionPlan);
		Assert.True(preflight.HasComparatorStages);
		Assert.False(preflight.CanInvokeReaderFunctions);
		Assert.False(preflight.CanReadJavaValues);
		Assert.False(preflight.CanReadCSharpValues);
		Assert.False(preflight.CanProjectValues);
		Assert.False(preflight.CanCompareValues);
		Assert.False(preflight.CanAttachRuntimeContext);
		Assert.False(preflight.CanEmitResults);
		Assert.False(preflight.CanRunRuntimeComparison);
		Assert.False(preflight.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", preflight.TraceName);
		Assert.Contains("addRecruitment/addApplication", preflight.JavaSource, StringComparison.Ordinal);
		Assert.Contains("typed-reader implementation gate", preflight.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(preflight.Rows, row =>
			row.Stage == FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.ReaderImplementationGate
			&& row.Evidence.Contains("typedReaderGateRows=", StringComparison.Ordinal)
			&& row.Evidence.Contains("runtimeRowValueIntakeRows=", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharpHandoffStatus=BlockedMissingAcceptedBoundaryRows", StringComparison.Ordinal)
			&& row.Notes.Contains("typed-reader gate evidence is preserved", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyReaderGateStillBlocksWhenComparatorPreflightNotReady()
	{
		var preflight = FindGroupMutationPostValueReaderFunctionExecutionPreflightService.Create(ReadyReaderGate());

		Assert.Equal(FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.BlockedComparatorPreflightNotReady, preflight.Status);
		Assert.Contains(preflight.Rows, row =>
			row.Stage == FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.ReaderImplementationGate
			&& row.Status == FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus.ReadyForInvocationInput
			&& row.JavaReaderFunctions.Contains("ReadJavaInt32Scalar")
			&& row.CSharpReaderFunctions.Contains("ReadCSharpInt32Scalar")
			&& row.Evidence.Contains("typedReaderGateRows=", StringComparison.Ordinal)
			&& row.Evidence.Contains("runtimeRowValueIntakeRows=RuntimeRowValueIntake", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharpHandoffCanFeedJavaArtifactPairing=True", StringComparison.Ordinal)
			&& row.Notes.Contains("no functions are invoked", StringComparison.Ordinal));
		Assert.Contains(preflight.Rows, row =>
			row.Stage == FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.ComparatorPreflight
			&& row.Status == FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus.Blocked
			&& row.Evidence.Contains("BlockedResultSchemaNotReady", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyInputsNameReaderInvocationRowsWithoutInvoking()
	{
		var preflight = FindGroupMutationPostValueReaderFunctionExecutionPreflightService.Create(ReadyReaderGate(), ReadyComparatorPreflight());

		Assert.Equal(FindGroupMutationPostValueReaderFunctionExecutionPreflightStatus.ReadyForFunctionExecutionBlocked, preflight.Status);
		Assert.False(preflight.CanInvokeReaderFunctions);
		Assert.False(preflight.CanReadJavaValues);
		Assert.False(preflight.CanReadCSharpValues);
		Assert.False(preflight.CanCompareValues);
		Assert.False(preflight.CanClaimVerifiedParity);
		Assert.Contains(preflight.Rows, row =>
			row.Stage == FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.JavaReaderInvocation
			&& row.Status == FindGroupMutationPostValueReaderFunctionExecutionPreflightStageStatus.Deferred
			&& row.RequiresAcceptedJavaRows
			&& !row.RequiresAcceptedCSharpRows
			&& row.JavaReaderFunctions.Contains("ReadJavaInt32Scalar")
			&& row.JavaReaderFunctions.Contains("ReadJavaBooleanScalar")
			&& row.JavaReaderFunctions.Contains("ReadJavaStringScalar")
			&& row.RequiredPrecondition.Contains("runtime-backed Java schema-v1 artifact rows", StringComparison.Ordinal)
			&& !row.CanInvokeReaderFunctions);
		Assert.Contains(preflight.Rows, row =>
			row.Stage == FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.CSharpReaderInvocation
			&& !row.RequiresAcceptedJavaRows
			&& row.RequiresAcceptedCSharpRows
			&& row.CSharpReaderFunctions.Contains("ReadCSharpInt32Scalar")
			&& row.CSharpReaderFunctions.Contains("ReadCSharpBooleanScalar")
			&& row.CSharpReaderFunctions.Contains("ReadCSharpStringScalar")
			&& row.RequiredPrecondition.Contains("production ProcessPacketAsync", StringComparison.Ordinal)
			&& !row.CanReadValues);
	}

	[Fact]
	public void Create_OrderedListAndProjectionPreconditionsStayBlocked()
	{
		var preflight = FindGroupMutationPostValueReaderFunctionExecutionPreflightService.Create(ReadyReaderGate(), ReadyComparatorPreflight());

		Assert.Contains(preflight.Rows, row =>
			row.Stage == FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.OrderedListReaderInvocation
			&& row.JavaReaderFunctions.SequenceEqual(["ReadJavaOrderedInt32List"])
			&& row.CSharpReaderFunctions.SequenceEqual(["ReadCSharpOrderedInt32List"])
			&& row.RequiredPrecondition.Contains("visibleEntryObjectIdsAfterMutation", StringComparison.Ordinal)
			&& row.Notes.Contains("materialized packet order", StringComparison.Ordinal));
		Assert.Contains(preflight.Rows, row =>
			row.Stage == FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.EqualityProjection
			&& row.RequiresProjectedValues
			&& row.RequiresReaderFunctions
			&& row.JavaReaderFunctions.Contains("ReadJavaEnumStringScalar")
			&& row.CSharpReaderFunctions.Contains("ReadCSharpEnumStringScalar")
			&& row.Notes.Contains("emits no javaValue/csharpValue pairs", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ContextAndResultEmissionRemainConservative()
	{
		var preflight = FindGroupMutationPostValueReaderFunctionExecutionPreflightService.Create(ReadyReaderGate(), ReadyComparatorPreflight());

		Assert.Contains(preflight.Rows, row =>
			row.Stage == FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.MismatchContextAttachment
			&& row.JavaReaderFunctions.SequenceEqual(["AttachJavaMismatchContext"])
			&& row.CSharpReaderFunctions.SequenceEqual(["AttachCSharpMismatchContext"])
			&& !row.RequiresProjectedValues
			&& row.Notes.Contains("must never enable Matched output", StringComparison.Ordinal));
		Assert.Contains(preflight.Rows, row =>
			row.Stage == FindGroupMutationPostValueReaderFunctionExecutionPreflightStage.ResultEmission
			&& row.RequiresAcceptedJavaRows
			&& row.RequiresAcceptedCSharpRows
			&& row.RequiresProjectedValues
			&& row.RequiresResultSchema
			&& !row.CanEmitResults
			&& row.Notes.Contains("no Matched, missing-row, FieldMismatch, or ignored-context rows", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostTypedValueReaderImplementationReadinessGate ReadyReaderGate() =>
		new(
			FindGroupMutationPostTypedValueReaderImplementationReadinessGateStatus.ReadyForReaderImplementationBlocked,
			[
				ReaderGateRow(
					1,
					FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.RuntimeRowValueIntake,
					readerKind: null,
					javaFunction: string.Empty,
					csharpFunction: string.Empty,
					PreservesCollectionOrder: false,
					evidence: "runtimeRowValueIntakeRows=RuntimeRowValueIntake=status=ReadyForRuntimeRowsValueReadersBlocked; valueProjectionHandoffRows=ValueProjectionHandoff=status=ReadyForRuntimeValuesProjectionBlocked; csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked; csharpHandoffCanFeedJavaArtifactPairing=True",
					notes: "runtime intake test row"),
				ReaderGateRow(
					2,
					FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.Int32ScalarReader,
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar,
					"ReadJavaInt32Scalar",
					"ReadCSharpInt32Scalar",
					PreservesCollectionOrder: false),
				ReaderGateRow(
					3,
					FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.BooleanScalarReader,
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar,
					"ReadJavaBooleanScalar",
					"ReadCSharpBooleanScalar",
					PreservesCollectionOrder: false),
				ReaderGateRow(
					4,
					FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.OrderedInt32ListReader,
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List,
					"ReadJavaOrderedInt32List",
					"ReadCSharpOrderedInt32List",
					PreservesCollectionOrder: true),
				ReaderGateRow(
					5,
					FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.StringScalarReader,
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar,
					"ReadJavaStringScalar",
					"ReadCSharpStringScalar",
					PreservesCollectionOrder: false),
				ReaderGateRow(
					6,
					FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.EnumStringScalarReader,
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar,
					"ReadJavaEnumStringScalar",
					"ReadCSharpEnumStringScalar",
					PreservesCollectionOrder: false),
				ReaderGateRow(
					7,
					FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.MismatchContextAttachment,
					FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext,
					"AttachJavaMismatchContext",
					"AttachCSharpMismatchContext",
					PreservesCollectionOrder: false,
					RequiresReaders: false),
			],
			HasRuntimeRowValueIntake: true,
			HasImplementationRunbook: true,
			HasTypedReaderPreflight: true,
			HasRuntimeRows: true,
			HasReaderFunctionPlan: true,
			TotalEqualityFieldCount: 38,
			TotalRuntimeContextFieldCount: 4,
			CanImplementReaders: false,
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			"reader function planning ready but blocked",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostTypedValueReaderImplementationReadinessGateRow ReaderGateRow(
		int order,
		FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage stage,
		FindGroupMutationPostProjectedRowComparisonValueReaderKind? readerKind,
		string javaFunction,
		string csharpFunction,
		bool PreservesCollectionOrder,
		bool RequiresReaders = true,
		string? evidence = null,
		string? notes = null) =>
		new(
			order,
			stage,
			readerKind,
			FindGroupMutationPostTypedValueReaderImplementationReadinessGateStageStatus.Deferred,
			EqualityFieldCount: RequiresReaders ? 1 : 0,
			RuntimeContextFieldCount: RequiresReaders ? 0 : 4,
			HasRuntimeRows: true,
			HasRunbookStep: true,
			RequiresJavaReader: RequiresReaders,
			RequiresCSharpReader: RequiresReaders,
			PreservesCollectionOrder,
			CanImplement: false,
			CanReadValues: false,
			CanCompareValues: false,
			javaFunction,
			csharpFunction,
			"planned reader function",
			evidence ?? "reader function evidence",
			notes ?? "test reader row");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract ReadyComparatorPreflight() =>
		new(
			FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedComparatorImplementationDeferred,
			[
				ComparatorStage(
					1,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.RowIdentityPairing,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedRuntimeRowsMissing),
				ComparatorStage(
					2,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.TypedReaderExecution,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedReaderExecutionDeferred),
			],
			EqualityFieldCount: 38,
			RuntimeContextFieldCount: 4,
			HasImplementationRunbook: true,
			HasResultSchema: true,
			CanExecuteComparator: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanAttachRuntimeContext: false,
			CanEmitResults: false,
			"comparator ready but deferred",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageRow ComparatorStage(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage stage,
		FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus status) =>
		new(
			order,
			stage,
			status,
			EqualityFieldCount: 38,
			RuntimeContextFieldCount: 4,
			OutputKinds: [],
			RequiresAcceptedJavaRows: true,
			RequiresAcceptedCSharpRows: true,
			RequiresProjectedValues: false,
			RequiresResultSchema: true,
			CanExecute: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			"required producer",
			"stage evidence",
			"stage notes");
}
