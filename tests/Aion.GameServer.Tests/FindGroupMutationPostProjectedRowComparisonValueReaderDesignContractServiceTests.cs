using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractServiceTests
{
	[Fact]
	public void Create_DefaultContractBlocksBeforeExecutionGateReadiness()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus.BlockedExecutionGateNotReady, contract.Status);
		Assert.False(contract.IsLive);
		Assert.True(contract.HasExecutionReadinessGate);
		Assert.True(contract.HasValueContract);
		Assert.True(contract.HasRequiredFieldMappings);
		Assert.False(contract.CanReadJavaValues);
		Assert.False(contract.CanReadCSharpValues);
		Assert.False(contract.CanCompareValues);
		Assert.Equal(42, contract.Fields.Count);
		Assert.Contains("execution-readiness gate", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", contract.TraceName);
		Assert.Contains("addRecruitment/addApplication", contract.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_MapsRequiredFieldsToJavaJsonPathAndCSharpAccessor()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create();

		Assert.Contains(contract.Fields, field =>
			field.Action == 2
			&& field.FieldName == "postedSystemMessageId"
			&& field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue
			&& field.RequiresJavaRead
			&& field.RequiresCSharpRead
			&& !field.CanReadValues
			&& field.JavaJsonPath == "$.traces[*].postedSystemMessageId"
			&& field.CSharpAccessor == "FindGroupDirectPacketMutationPostBoundaryTraceExport.PostedSystemMessageId"
			&& field.ReaderRule.Contains("preserve schema-v1 type", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.Action == 6
			&& field.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& field.JavaJsonPath == "$.traces[*].visibleEntryObjectIdsAfterMutation"
			&& field.CSharpAccessor == "FindGroupDirectPacketMutationPostBoundaryTraceExport.VisibleEntryObjectIdsAfterMutation"
			&& field.ReaderRule.Contains("collection ordering", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_KeepsRuntimeOnlyFieldsIgnoredForEquality()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create();

		Assert.Contains(contract.Fields, field =>
			field.FieldName == "traceSource"
			&& field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext
			&& field.Status == FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus.IgnoredRuntimeContextOnly
			&& !field.RequiresJavaRead
			&& !field.RequiresCSharpRead
			&& field.Blocker.Contains("intentionally ignored", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "serverEpochSeconds"
			&& field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext
			&& field.ReaderRule.Contains("Do not compare", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeEvidenceReadyGateStillBlocksReaderImplementation()
	{
		var gate = RuntimeEvidencePresentGate();

		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create(gate);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderDesignStatus.BlockedValueReaderNotImplemented, contract.Status);
		Assert.False(contract.CanReadJavaValues);
		Assert.False(contract.CanReadCSharpValues);
		Assert.Contains("no value reader is implemented", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "postedSystemMessageId"
			&& field.Status == FindGroupMutationPostProjectedRowComparisonValueReaderFieldStatus.BlockedReaderNotImplemented
			&& field.Blocker.Contains("value reader", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ListsDistinctJavaPathsAndCSharpAccessors()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create();

		Assert.Contains("$.traces[*].action", contract.JavaJsonPaths);
		Assert.Contains("$.traces[*].visibleEntryObjectIdsAfterMutation", contract.JavaJsonPaths);
		Assert.Contains("FindGroupDirectPacketMutationPostBoundaryTraceExport.Action", contract.CSharpAccessors);
		Assert.Contains("FindGroupDirectPacketMutationPostBoundaryTraceExport.RegistrySendsObservedInOrder", contract.CSharpAccessors);
		Assert.Equal(contract.JavaJsonPaths.Count, contract.JavaJsonPaths.Distinct(StringComparer.Ordinal).Count());
		Assert.Equal(contract.CSharpAccessors.Count, contract.CSharpAccessors.Distinct(StringComparer.Ordinal).Count());
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateReport RuntimeEvidencePresentGate() =>
		new(
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
}
