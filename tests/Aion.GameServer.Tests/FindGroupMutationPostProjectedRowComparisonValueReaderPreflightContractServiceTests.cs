using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractServiceTests
{
	[Fact]
	public void Create_DefaultPreflightBlocksBeforeDesignReadinessAndDoesNotRead()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus.BlockedDesignNotReady, contract.Status);
		Assert.False(contract.IsLive);
		Assert.True(contract.HasValueReaderDesign);
		Assert.True(contract.HasSchemaV1TypeMap);
		Assert.True(contract.HasRequiredTypedReaders);
		Assert.False(contract.CanReadJavaValues);
		Assert.False(contract.CanReadCSharpValues);
		Assert.False(contract.CanCompareValues);
		Assert.Equal(42, contract.Fields.Count);
		Assert.Contains("design reaches implementation-readiness", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", contract.TraceName);
		Assert.Contains("addRecruitment/addApplication", contract.JavaSource, StringComparison.Ordinal);
		Assert.All(contract.Fields, field => Assert.False(field.CanReadValues));
	}

	[Fact]
	public void Create_EnumeratesSchemaV1ReaderKinds()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();

		Assert.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar, contract.ReaderKinds);
		Assert.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar, contract.ReaderKinds);
		Assert.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar, contract.ReaderKinds);
		Assert.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar, contract.ReaderKinds);
		Assert.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List, contract.ReaderKinds);
		Assert.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext, contract.ReaderKinds);
	}

	[Fact]
	public void Create_MapsScalarReadersToJavaJsonAndCSharpShapes()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();

		Assert.Contains(contract.Fields, field =>
			field.Action == 2
			&& field.FieldName == "activePlayerObjectId"
			&& field.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar
			&& field.ExpectedClrType == "int"
			&& field.JavaJsonToken == "JSON integer"
			&& field.CSharpValueShape == "int"
			&& field.RequiresJavaReader
			&& field.RequiresCSharpReader
			&& field.JavaJsonPath == "$.traces[*].activePlayerObjectId"
			&& field.CSharpAccessor == "FindGroupDirectPacketMutationPostBoundaryTraceExport.ActivePlayerObjectId");
		Assert.Contains(contract.Fields, field =>
			field.Action == 6
			&& field.FieldName == "stateMutationRecordedBeforeDirectPackets"
			&& field.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar
			&& field.ExpectedClrType == "bool"
			&& field.JavaJsonToken == "JSON boolean");
	}

	[Fact]
	public void Create_MapsStringEnumAndOrderedListReaders()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();

		Assert.Contains(contract.Fields, field =>
			field.FieldName == "traceName"
			&& field.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar
			&& field.ExpectedClrType == "string"
			&& field.JavaJsonToken == "JSON string");
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "mutationKind"
			&& field.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar
			&& field.ExpectedClrType == "FindGroupDirectPacketMutationPostTraceMutationKind"
			&& field.JavaJsonToken == "JSON string enum name");
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& field.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List
			&& field.ExpectedClrType == "IReadOnlyList<int>"
			&& field.PreservesCollectionOrder
			&& field.ReaderPrecondition.Contains("JSON integer array", StringComparison.Ordinal)
			&& field.Notes.Contains("preserve ordering exactly", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_KeepsRuntimeContextIgnoredAndNonReading()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();

		Assert.Contains(contract.Fields, field =>
			field.FieldName == "traceSource"
			&& field.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext
			&& field.Status == FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus.IgnoredRuntimeContextOnly
			&& !field.RequiresJavaReader
			&& !field.RequiresCSharpReader
			&& field.Blocker.Contains("ignored for equality", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "serverEpochSeconds"
			&& field.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext
			&& field.ReaderPrecondition.Contains("mismatch context", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeEvidenceReadyDesignStillDefersTypedReaders()
	{
		var design = ReadyDesignContract();

		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderPreflightStatus.BlockedTypedReadersDeferred, contract.Status);
		Assert.Contains("typed readers are enumerated", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "refreshedListAction"
			&& field.Status == FindGroupMutationPostProjectedRowComparisonValueReaderPreflightFieldStatus.BlockedReaderImplementationDeferred
			&& field.Blocker.Contains("not implemented", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract ReadyDesignContract()
	{
		var gate = new FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateReport(
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedComparatorNotAllowed,
			[],
			HasLiveInputHandoff: true,
			HasRuntimeEvidenceChecklist: true,
			HasRuntimeEvidence: true,
			CanImplementComparator: false,
			CanExecuteComparator: false,
			CanClaimVerifiedParity: false,
			CanEnableLiveDispatch: false,
			"Runtime evidence exists, but comparator implementation remains deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

		return FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create(gate);
	}
}
