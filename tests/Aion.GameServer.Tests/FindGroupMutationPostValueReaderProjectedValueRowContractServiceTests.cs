using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostValueReaderProjectedValueRowContractServiceTests
{
	[Fact]
	public void Create_DefaultContractBlocksBeforeFunctionExecutionPreflight()
	{
		var contract = FindGroupMutationPostValueReaderProjectedValueRowContractService.Create();

		Assert.Equal(FindGroupMutationPostValueReaderProjectedValueRowContractStatus.BlockedFunctionExecutionPreflightNotReady, contract.Status);
		Assert.False(contract.IsLive);
		Assert.True(contract.HasFunctionExecutionPreflight);
		Assert.True(contract.HasExecutorImplementationPlan);
		Assert.True(contract.HasTypedValueReaderPreflight);
		Assert.True(contract.HasProjectedValueRows);
		Assert.Equal(42, contract.Rows.Count);
		Assert.Equal(38, contract.RequiredEqualityFieldCount);
		Assert.Equal(4, contract.IgnoredRuntimeContextFieldCount);
		Assert.False(contract.CanInvokeReaderFunctions);
		Assert.False(contract.CanReadJavaValues);
		Assert.False(contract.CanReadCSharpValues);
		Assert.False(contract.CanProjectValues);
		Assert.False(contract.CanCompareValues);
		Assert.False(contract.CanAttachRuntimeContext);
		Assert.False(contract.CanEmitResults);
		Assert.False(contract.CanRunRuntimeComparison);
		Assert.False(contract.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", contract.TraceName);
		Assert.Contains("addRecruitment/addApplication", contract.JavaSource, StringComparison.Ordinal);
		Assert.Contains("function execution preflight", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(contract.Rows, row =>
			row.FieldName == "activePlayerObjectId"
			&& row.Status == FindGroupMutationPostValueReaderProjectedValueRowStatus.BlockedFunctionExecutionPreflightNotReady
			&& row.Evidence.Contains("functionPreflightRows=", StringComparison.Ordinal)
			&& row.Evidence.Contains("typedReaderGateRows=", StringComparison.Ordinal)
			&& row.Evidence.Contains("runtimeRowValueIntakeRows=", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharpHandoffStatus=BlockedMissingAcceptedBoundaryRows", StringComparison.Ordinal)
			&& row.JavaValue == "<not-read>"
			&& row.CSharpValue == "<not-read>");
	}

	[Fact]
	public void Create_ReadyFunctionPreflightStillBlocksWhenExecutorPlanNotReady()
	{
		var functionPreflight = FindGroupMutationPostValueReaderFunctionExecutionPreflightService.Create(ReadyReaderGate(), ReadyComparatorPreflight());

		var contract = FindGroupMutationPostValueReaderProjectedValueRowContractService.Create(functionPreflight);

		Assert.Equal(FindGroupMutationPostValueReaderProjectedValueRowContractStatus.BlockedExecutorImplementationPlanNotReady, contract.Status);
		Assert.Contains(contract.Rows, row =>
			row.FieldName == "activePlayerObjectId"
			&& row.Status == FindGroupMutationPostValueReaderProjectedValueRowStatus.BlockedExecutorImplementationPlanNotReady
			&& row.Blocker.Contains("executor implementation plan", StringComparison.Ordinal)
			&& row.Evidence.Contains("functionPreflightRows=", StringComparison.Ordinal)
			&& row.Evidence.Contains("typedReaderGateRows=", StringComparison.Ordinal)
			&& row.Evidence.Contains("runtimeRowValueIntakeRows=RuntimeRowValueIntake", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharpHandoffCanFeedJavaArtifactPairing=True", StringComparison.Ordinal)
			&& row.JavaValue == "<not-read>"
			&& row.CSharpValue == "<not-read>");
	}

	[Fact]
	public void Create_ReadyInputsDefineProjectedValueRowsWithoutReading()
	{
		var contract = FindGroupMutationPostValueReaderProjectedValueRowContractService.Create(
			ReadyFunctionExecutionPreflight(),
			ReadyExecutorImplementationPlan());

		Assert.Equal(FindGroupMutationPostValueReaderProjectedValueRowContractStatus.ReadyForProjectedRowsBlocked, contract.Status);
		Assert.Contains("every Java/C# value remains unread", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(contract.Rows, row =>
			row.Action == 2
			&& row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
			&& row.FieldName == "activePlayerObjectId"
			&& row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar
			&& row.ValueType == "int"
			&& row.JavaReaderFunction == "ReadJavaInt32Scalar"
			&& row.CSharpReaderFunction == "ReadCSharpInt32Scalar"
			&& row.JavaReadStatus == FindGroupMutationPostValueReaderProjectedValueReadStatus.NotRead
			&& row.CSharpReadStatus == FindGroupMutationPostValueReaderProjectedValueReadStatus.NotRead
			&& row.JavaValue == "<not-read>"
			&& row.CSharpValue == "<not-read>"
			&& row.RequiresJavaRow
			&& row.RequiresCSharpRow
			&& row.RequiresReaderFunctions
			&& !row.CanReadJavaValue
			&& !row.CanReadCSharpValue
			&& !row.CanCompareValue
			&& !row.CanEmitResult);
		Assert.Contains(contract.Rows, row =>
			row.Action == 6
			&& row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
			&& row.FieldName == "postedSystemMessageType"
			&& row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar
			&& row.JavaReaderFunction == "ReadJavaStringScalar"
			&& row.CSharpReaderFunction == "ReadCSharpStringScalar");
	}

	[Fact]
	public void Create_OrderedListProjectedRowPreservesCollectionOrder()
	{
		var contract = FindGroupMutationPostValueReaderProjectedValueRowContractService.Create(
			ReadyFunctionExecutionPreflight(),
			ReadyExecutorImplementationPlan());

		Assert.Contains(contract.Rows, row =>
			row.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& row.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List
			&& row.ValueType == "IReadOnlyList<int>"
			&& row.JavaReaderFunction == "ReadJavaOrderedInt32List"
			&& row.CSharpReaderFunction == "ReadCSharpOrderedInt32List"
			&& row.PreservesCollectionOrder
			&& row.Notes.Contains("materialized order", StringComparison.Ordinal)
			&& !row.CanCompareValue);
	}

	[Fact]
	public void Create_IgnoredRuntimeContextRowsStayOutOfEquality()
	{
		var contract = FindGroupMutationPostValueReaderProjectedValueRowContractService.Create(
			ReadyFunctionExecutionPreflight(),
			ReadyExecutorImplementationPlan());

		Assert.Contains(contract.Rows, row =>
			row.FieldName == "traceSource"
			&& row.Status == FindGroupMutationPostValueReaderProjectedValueRowStatus.IgnoredRuntimeContextOnly
			&& row.JavaReadStatus == FindGroupMutationPostValueReaderProjectedValueReadStatus.IgnoredRuntimeContext
			&& row.CSharpReadStatus == FindGroupMutationPostValueReaderProjectedValueReadStatus.IgnoredRuntimeContext
			&& row.JavaReaderFunction == "AttachJavaMismatchContext"
			&& row.CSharpReaderFunction == "AttachCSharpMismatchContext"
			&& row.JavaValue == "<ignored-runtime-context>"
			&& row.CSharpValue == "<ignored-runtime-context>"
			&& !row.RequiresJavaRow
			&& !row.RequiresCSharpRow
			&& !row.RequiresReaderFunctions
			&& row.Blocker.Contains("cannot participate in equality", StringComparison.Ordinal)
			&& row.Notes.Contains("must never enable Matched output", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostValueReaderFunctionExecutionPreflight ReadyFunctionExecutionPreflight() =>
		FindGroupMutationPostValueReaderFunctionExecutionPreflightService.Create(ReadyReaderGate(), ReadyComparatorPreflight());

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract ReadyExecutorImplementationPlan() =>
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService.Create(
			ReadyExecutorGate(),
			ReadyComparatorPreflight());

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
					evidence: "runtimeRowValueIntakeRows=RuntimeRowValueIntake=status=ReadyForRuntimeRowsValueReadersBlocked; valueProjectionHandoffRows=ValueProjectionHandoff=status=ReadyForRuntimeValuesProjectionBlocked; csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked; csharpHandoffCanFeedJavaArtifactPairing=True"),
				ReaderGateRow(2, FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.Int32ScalarReader, FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar, "ReadJavaInt32Scalar", "ReadCSharpInt32Scalar"),
				ReaderGateRow(3, FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.BooleanScalarReader, FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar, "ReadJavaBooleanScalar", "ReadCSharpBooleanScalar"),
				ReaderGateRow(4, FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.OrderedInt32ListReader, FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List, "ReadJavaOrderedInt32List", "ReadCSharpOrderedInt32List", PreservesCollectionOrder: true),
				ReaderGateRow(5, FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.StringScalarReader, FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar, "ReadJavaStringScalar", "ReadCSharpStringScalar"),
				ReaderGateRow(6, FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.EnumStringScalarReader, FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar, "ReadJavaEnumStringScalar", "ReadCSharpEnumStringScalar"),
				ReaderGateRow(7, FindGroupMutationPostTypedValueReaderImplementationReadinessGateStage.MismatchContextAttachment, FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext, "AttachJavaMismatchContext", "AttachCSharpMismatchContext", RequiresReaders: false),
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
		bool PreservesCollectionOrder = false,
		bool RequiresReaders = true,
		string? evidence = null) =>
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
			"test reader row");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateReport ReadyExecutorGate() =>
		new(
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedExecutorImplementationDeferred,
			[
				new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow(
					1,
					FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.ExecutorImplementation,
					FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedExecutorImplementationDeferred,
					HasPrerequisite: true,
					HasRuntimeEvidence: true,
					BlocksExecutorImplementation: true,
					CanImplementExecutor: false,
					CanExecuteExecutor: false,
					CanEnableLiveDispatch: false,
					"hasRuntimeEvidence=True",
					"executor implementation still deferred",
					"test row"),
			],
			HasLiveInputHandoff: true,
			HasRuntimeEvidenceChecklist: true,
			HasComparatorPreflight: true,
			HasRuntimeEvidence: true,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanEnableLiveDispatch: false,
			CanClaimVerifiedParity: false,
			"Value-reader executor implementation remains intentionally deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract ReadyComparatorPreflight() =>
		new(
			FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedComparatorImplementationDeferred,
			[
				new FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageRow(
					1,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.RowIdentityPairing,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedRuntimeRowsMissing,
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
					"ready comparator stage",
					"evidence",
					"notes"),
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
			"Comparator implementation remains deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
}
