using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueContractServiceTests
{
	[Fact]
	public void Create_DefaultContractBlocksBeforeValueProjection()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueContractStatus.BlockedExecutorSkeletonNotReady, contract.Status);
		Assert.False(contract.IsLive);
		Assert.True(contract.HasExecutorSkeleton);
		Assert.True(contract.HasResultContract);
		Assert.False(contract.HasAllPairedInputs);
		Assert.False(contract.CanProjectValues);
		Assert.False(contract.CanEmitMatched);
		Assert.False(contract.CanEmitFieldMismatch);
		Assert.Equal(42, contract.Fields.Count);
		Assert.Contains("Value projection is blocked", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", contract.TraceName);
		Assert.Contains("addRecruitment/addApplication", contract.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_NamesRequiredEqualityValueSourcesWithoutReadingValues()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create();

		Assert.Contains(contract.Fields, field =>
			field.Action == 2
			&& field.FieldName == "postedSystemMessageId"
			&& field.Status == FindGroupMutationPostProjectedRowComparisonValueSourceStatus.RequiredEqualityValueSource
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.DirectPacketMismatch
			&& field.RequiresJavaValue
			&& field.RequiresCSharpValue
			&& !field.CanEmitMatched
			&& !field.CanEmitFieldMismatch
			&& field.JavaValueSource.Contains("Blocked Java row field", StringComparison.Ordinal)
			&& field.CSharpValueSource.Contains("Blocked C# row field", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.Action == 6
			&& field.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.MutationStateMismatch
			&& field.Notes.Contains("future executor may emit Matched or FieldMismatch", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_KeepsRuntimeOnlyFieldsAsIgnoredContext()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create();

		Assert.Equal(["traceSource", "serverEpochSeconds"], contract.IgnoredRuntimeFields);
		Assert.DoesNotContain("traceSource", contract.EqualityProjectionFields);
		Assert.DoesNotContain("serverEpochSeconds", contract.EqualityProjectionFields);
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "traceSource"
			&& field.Status == FindGroupMutationPostProjectedRowComparisonValueSourceStatus.IgnoredRuntimeContextValue
			&& !field.RequiresJavaValue
			&& !field.RequiresCSharpValue
			&& field.Blocker.Contains("Ignored runtime context", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "serverEpochSeconds"
			&& field.Status == FindGroupMutationPostProjectedRowComparisonValueSourceStatus.IgnoredRuntimeContextValue);
	}

	[Fact]
	public void Create_PairedInputsAreValueProjectionDeferredNotCompared()
	{
		var executor = PairedExecutorSkeleton();

		var contract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create(executor);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueContractStatus.ReadyForFutureValueProjectionButDeferred, contract.Status);
		Assert.True(contract.HasAllPairedInputs);
		Assert.False(contract.CanProjectValues);
		Assert.False(contract.CanEmitMatched);
		Assert.False(contract.CanEmitFieldMismatch);
		Assert.Contains("not projected or compared", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(contract.Fields, field =>
			field.Action == 2
			&& field.FieldName == "postedSystemMessageId"
			&& field.JavaValueSource.Contains("Future Java row field", StringComparison.Ordinal)
			&& field.CSharpValueSource.Contains("Future C# row field", StringComparison.Ordinal)
			&& field.Blocker.Contains("Values not projected or compared yet", StringComparison.Ordinal)
			&& !field.CanEmitMatched
			&& !field.CanEmitFieldMismatch);
	}

	[Fact]
	public void Create_ReadyExecutorMissingOnePairBlocksValueSources()
	{
		var executor = new FindGroupMutationPostProjectedRowComparisonExecutorSkeleton(
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.BlockedMissingPairedRows,
			[
				new FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow(
					1,
					2,
					FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment,
					"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
					FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedValueComparisonDeferred,
					HasAcceptedJavaRow: true,
					HasAcceptedCSharpRow: true,
					ComparesValues: false,
					"action, mutationKind, fieldName, differenceKind, javaValue, csharpValue, javaSource",
					"action=2",
					"paired"),
				new FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow(
					2,
					6,
					FindGroupDirectPacketMutationPostTraceMutationKind.Application,
					"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
					FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingCSharpRow,
					HasAcceptedJavaRow: true,
					HasAcceptedCSharpRow: false,
					ComparesValues: false,
					"action, mutationKind, rowIdentity, javaRowReference",
					"action=6",
					"missing C#"),
			],
			HasDryRunContract: true,
			HasResultSkeleton: true,
			HasAllPairedInputs: false,
			ShouldAttemptExecutor: true,
			CanCompareValues: false,
			"missing pair",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

		var contract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create(executor);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueContractStatus.BlockedMissingValueSources, contract.Status);
		Assert.Contains(contract.Fields, field =>
			field.Action == 6
			&& field.Status == FindGroupMutationPostProjectedRowComparisonValueSourceStatus.RequiredEqualityValueSource
			&& field.Blocker == "Missing accepted C# row value source.");
	}

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeleton PairedExecutorSkeleton() =>
		new(
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.ReadyForFutureValueComparisonButDeferred,
			[
				new FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow(
					1,
					2,
					FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment,
					"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
					FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedValueComparisonDeferred,
					HasAcceptedJavaRow: true,
					HasAcceptedCSharpRow: true,
					ComparesValues: false,
					"action, mutationKind, fieldName, differenceKind, javaValue, csharpValue, javaSource",
					"action=2",
					"paired"),
				new FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow(
					2,
					6,
					FindGroupDirectPacketMutationPostTraceMutationKind.Application,
					"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
					FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedValueComparisonDeferred,
					HasAcceptedJavaRow: true,
					HasAcceptedCSharpRow: true,
					ComparesValues: false,
					"action, mutationKind, fieldName, differenceKind, javaValue, csharpValue, javaSource",
					"action=6",
					"paired"),
			],
			HasDryRunContract: true,
			HasResultSkeleton: true,
			HasAllPairedInputs: true,
			ShouldAttemptExecutor: true,
			CanCompareValues: false,
			"deferred",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
}
