using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonExecutorSkeletonServiceTests
{
	[Fact]
	public void Create_DefaultSkeletonBlocksAndDoesNotCompareValues()
	{
		var skeleton = FindGroupMutationPostProjectedRowComparisonExecutorSkeletonService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.BlockedDryRunNotReady, skeleton.Status);
		Assert.False(skeleton.IsLive);
		Assert.True(skeleton.HasDryRunContract);
		Assert.True(skeleton.HasResultSkeleton);
		Assert.False(skeleton.HasAllPairedInputs);
		Assert.False(skeleton.ShouldAttemptExecutor);
		Assert.False(skeleton.CanCompareValues);
		Assert.Equal([2, 6], skeleton.Rows.Select(row => row.Action));
		Assert.All(skeleton.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingJavaRow, row.Status);
			Assert.False(row.HasAcceptedJavaRow);
			Assert.False(row.HasAcceptedCSharpRow);
			Assert.False(row.ComparesValues);
			Assert.Contains("csharpRowReference", row.PlannedResultShape, StringComparison.Ordinal);
		});
		Assert.Contains("Comparison executor not invoked", skeleton.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", skeleton.TraceName);
		Assert.Contains("addRecruitment/addApplication", skeleton.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_JavaOnlyRowsEmitMissingCSharpPlannedRows()
	{
		var dryRun = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create(
			javaArtifacts: ShapeValidJavaArtifacts());

		var skeleton = FindGroupMutationPostProjectedRowComparisonExecutorSkeletonService.Create(dryRun);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.BlockedDryRunNotReady, skeleton.Status);
		Assert.False(skeleton.ShouldAttemptExecutor);
		Assert.False(skeleton.HasAllPairedInputs);
		Assert.All(skeleton.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingCSharpRow, row.Status);
			Assert.True(row.HasAcceptedJavaRow);
			Assert.False(row.HasAcceptedCSharpRow);
			Assert.False(row.ComparesValues);
			Assert.Contains("javaRowReference", row.PlannedResultShape, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void Create_PairedRowsDeferValueComparisonInsteadOfMaterializingResults()
	{
		var readyReport = new FindGroupMutationPostComparisonExecutionBlockerReport(
			FindGroupMutationPostComparisonExecutionBlockerReportStatus.ReadyForExecutor,
			[],
			HasJavaRows: true,
			HasLiveCSharpRows: true,
			HasProjectionMetadata: true,
			HasReadinessAggregate: true,
			HasResultContract: true,
			ShouldExecuteComparison: true,
			"Envelope gates are ready; a future executor may compare projected rows, but this report did not execute comparison.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
		var guardedFixture = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				LiveCSharpRow(2),
				LiveCSharpRow(6),
			]);
		var dryRun = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create(
			readyReport,
			javaArtifacts: ShapeValidJavaArtifacts(),
			guardedFixtureResultContract: guardedFixture);

		var skeleton = FindGroupMutationPostProjectedRowComparisonExecutorSkeletonService.Create(dryRun);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.ReadyForFutureValueComparisonButDeferred, skeleton.Status);
		Assert.True(skeleton.HasAllPairedInputs);
		Assert.True(skeleton.ShouldAttemptExecutor);
		Assert.False(skeleton.CanCompareValues);
		Assert.Contains("defers Java/C# value comparison", skeleton.ExecutionDecision, StringComparison.Ordinal);
		Assert.All(skeleton.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedValueComparisonDeferred, row.Status);
			Assert.True(row.HasAcceptedJavaRow);
			Assert.True(row.HasAcceptedCSharpRow);
			Assert.False(row.ComparesValues);
			Assert.Contains("javaValue", row.PlannedResultShape, StringComparison.Ordinal);
			Assert.Contains("does not compare field values", row.Notes, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void Create_ReadyDryRunWithMissingPairBlocksBeforeValueComparison()
	{
		var readyReport = new FindGroupMutationPostComparisonExecutionBlockerReport(
			FindGroupMutationPostComparisonExecutionBlockerReportStatus.ReadyForExecutor,
			[],
			HasJavaRows: true,
			HasLiveCSharpRows: true,
			HasProjectionMetadata: true,
			HasReadinessAggregate: true,
			HasResultContract: true,
			ShouldExecuteComparison: true,
			"Envelope gates are ready; a future executor may compare projected rows, but this report did not execute comparison.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
		var guardedFixture = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				LiveCSharpRow(2),
			]);
		var dryRun = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create(
			readyReport,
			javaArtifacts: ShapeValidJavaArtifacts(),
			guardedFixtureResultContract: guardedFixture);

		var skeleton = FindGroupMutationPostProjectedRowComparisonExecutorSkeletonService.Create(dryRun);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.BlockedMissingPairedRows, skeleton.Status);
		Assert.True(skeleton.ShouldAttemptExecutor);
		Assert.False(skeleton.HasAllPairedInputs);
		Assert.False(skeleton.CanCompareValues);
		Assert.Contains(skeleton.Rows, row =>
			row.Action == 2
			&& row.Status == FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedValueComparisonDeferred);
		Assert.Contains(skeleton.Rows, row =>
			row.Action == 6
			&& row.Status == FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingCSharpRow);
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryReport ShapeValidJavaArtifacts() =>
		new(
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid,
			FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot,
			[
				ShapeValidFile(2),
				ShapeValidFile(6),
			],
			HasGeneratedJavaArtifacts: true,
			HasAllExpectedFiles: true,
			HasOnlyShapeValidArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid only");

	private static FindGroupMutationPostJavaTraceArtifactDirectoryFileRow ShapeValidFile(int action) =>
		new(
			action,
			FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(action),
			FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
			new FindGroupMutationPostJavaTraceArtifactValidationReport(
				[],
				IsValid: true,
				new FindGroupMutationPostJavaTraceArtifactMetadata(
					SchemaVersion: 1,
					TraceName: "cm-find-group-direct-mutation-post-boundary",
					[
						new FindGroupMutationPostJavaTraceArtifactValidationTraceRow(
							SchemaVersion: 1,
							TraceName: "cm-find-group-direct-mutation-post-boundary",
							TraceSource: "Java",
							action,
							MutationKind: action == 2 ? "Recruitment" : "Application",
							PostedSystemMessageId: action == 2 ? 1400392 : 1400393,
							RefreshedListAction: action == 2 ? 0 : 4)
					])),
			"shape-valid only");

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport LiveCSharpRow(int action) =>
		FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(action) with
		{
			BoundaryAccepted = true,
			ActivePlayerObjectId = action == 2 ? 1001 : 1002,
			ActivePlayerRace = "ELYOS",
			ServerEpochSeconds = 123,
			MutatedEntryObjectId = action == 2 ? 2001 : 2002,
			StateMutationRecordedBeforeDirectPackets = true,
			PostedSystemMessageRecipientObjectId = action == 2 ? 1001 : 1002,
			RefreshedListRecipientObjectId = action == 2 ? 1001 : 1002,
			VisibleEntryObjectIdsAfterMutation = [action == 2 ? 2001 : 2002],
			ExecutorInvokedFromBoundary = true,
			RegistrySendsObservedInOrder = true,
		};
}
