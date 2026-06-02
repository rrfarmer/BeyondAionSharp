using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonResultSkeletonServiceTests
{
	[Fact]
	public void Create_DefaultSkeletonBlocksAndDoesNotMaterializeResults()
	{
		var skeleton = FindGroupMutationPostProjectedRowComparisonResultSkeletonService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonResultSkeletonStatus.BlockedDryRunNotReady, skeleton.Status);
		Assert.False(skeleton.IsLive);
		Assert.True(skeleton.HasDryRunContract);
		Assert.True(skeleton.RequiresJavaRows);
		Assert.True(skeleton.RequiresLiveCSharpRows);
		Assert.True(skeleton.RequiresFieldProjection);
		Assert.False(skeleton.CanMaterializeRealResults);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", skeleton.TraceName);
		Assert.Contains("addRecruitment/addApplication", skeleton.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ListsSkeletonRowsForEveryDryRunOutputKind()
	{
		var skeleton = FindGroupMutationPostProjectedRowComparisonResultSkeletonService.Create();

		Assert.Equal(5, skeleton.Rows.Count);
		Assert.Equal(Enumerable.Range(1, skeleton.Rows.Count), skeleton.Rows.Select(row => row.Order));
		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			],
			skeleton.Rows.Select(row => row.OutputKind));
	}

	[Fact]
	public void Create_FieldMismatchShapeNamesJavaAndCSharpValues()
	{
		var skeleton = FindGroupMutationPostProjectedRowComparisonResultSkeletonService.Create();

		Assert.Contains(skeleton.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch
			&& row.Status == FindGroupMutationPostProjectedRowComparisonResultRowStatus.PlannedFieldMismatch
			&& row.RequiresLiveRows
			&& row.ResultShape.Contains("javaValue", StringComparison.Ordinal)
			&& row.ResultShape.Contains("csharpValue", StringComparison.Ordinal)
			&& row.ResultShape.Contains("javaSource", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingRowShapesKeepOppositeRowReferences()
	{
		var skeleton = FindGroupMutationPostProjectedRowComparisonResultSkeletonService.Create();

		Assert.Contains(skeleton.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow
			&& row.ResultShape.Contains("csharpRowReference", StringComparison.Ordinal));
		Assert.Contains(skeleton.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow
			&& row.ResultShape.Contains("javaRowReference", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_IgnoredRuntimeContextDoesNotRequireLiveRows()
	{
		var skeleton = FindGroupMutationPostProjectedRowComparisonResultSkeletonService.Create();

		Assert.Contains(skeleton.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& row.Status == FindGroupMutationPostProjectedRowComparisonResultRowStatus.PlannedIgnoredRuntimeContext
			&& !row.RequiresLiveRows
			&& row.ResultShape.Contains("serverEpochSeconds", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyDryRunAllowsFutureMaterializationButStillSkeletonOnly()
	{
		var readyDryRun = new FindGroupMutationPostProjectedRowComparisonDryRunContract(
			FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor,
			Actions: [],
			AcceptedJavaRows: [],
			AcceptedCSharpRows: [],
			Fields:
			[
				new FindGroupMutationPostProjectedRowComparisonDryRunField(
					1,
					2,
					FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment,
					"postedSystemMessageId",
					FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus.RequiredEqualityInput,
					FindGroupMutationPostComparisonDifferenceKind.DirectPacketMismatch,
					"Field mismatch shape",
					"FindGroupService.addRecruitment",
					"test field")
			],
			OutputKinds:
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
			],
			HasExecutionBlockerReport: true,
			HasResultContract: true,
			HasJavaArtifactDirectoryReport: true,
			HasGuardedFixtureResultContract: true,
			ShouldCompareRows: true,
			ExecutionDecision: "future executor may compare",
			TraceName: "cm-find-group-direct-mutation-post-boundary",
			JavaSource: "FindGroupService.addRecruitment/addApplication",
			IsLive: false);

		var skeleton = FindGroupMutationPostProjectedRowComparisonResultSkeletonService.Create(readyDryRun);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonResultSkeletonStatus.ReadyForFutureResultMaterialization, skeleton.Status);
		Assert.True(skeleton.CanMaterializeRealResults);
		Assert.False(skeleton.IsLive);
		Assert.All(skeleton.Rows, row => Assert.Contains("Skeleton row only", row.Notes, StringComparison.Ordinal));
	}
}
