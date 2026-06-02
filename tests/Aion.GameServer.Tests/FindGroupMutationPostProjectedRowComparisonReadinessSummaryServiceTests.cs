using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonReadinessSummaryServiceTests
{
	[Fact]
	public void Create_DefaultSummaryBlocksBeforeExecutorReadiness()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonReadinessSummaryService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedDryRunNotReady, summary.Status);
		Assert.False(summary.IsLive);
		Assert.True(summary.HasDryRunContract);
		Assert.True(summary.HasExecutorSkeleton);
		Assert.True(summary.HasValueContract);
		Assert.True(summary.HasBlockedResultReport);
		Assert.False(summary.HasAllPairedInputs);
		Assert.False(summary.CanCompareRows);
		Assert.False(summary.CanProjectValues);
		Assert.False(summary.CanEmitResults);
		Assert.Equal(4, summary.Stages.Count);
		Assert.Contains("blocked before future executor readiness", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", summary.TraceName);
		Assert.Contains("addRecruitment/addApplication", summary.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultSummaryListsEachReadinessStage()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonReadinessSummaryService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonReadinessStage.DryRunContract,
				FindGroupMutationPostProjectedRowComparisonReadinessStage.ExecutorSkeleton,
				FindGroupMutationPostProjectedRowComparisonReadinessStage.ValueContract,
				FindGroupMutationPostProjectedRowComparisonReadinessStage.BlockedResultReport,
			],
			summary.Stages.Select(stage => stage.Stage));
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonReadinessStage.DryRunContract
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Blocked
			&& stage.HasExpectedShape
			&& stage.BlocksComparison
			&& stage.Evidence.Contains("shouldCompareRows=False", StringComparison.Ordinal));
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonReadinessStage.BlockedResultReport
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Blocked
			&& stage.Evidence.Contains("canEmitMatched=False", StringComparison.Ordinal)
			&& stage.Evidence.Contains("canEmitFieldMismatch=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingPairSummaryBlocksAtPairedInputs()
	{
		var dryRun = ReadyDryRun();
		var executor = MissingPairExecutor();
		var valueContract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create(executor);
		var blockedReport = FindGroupMutationPostProjectedRowComparisonBlockedResultReportService.Create(executor, valueContract);

		var summary = FindGroupMutationPostProjectedRowComparisonReadinessSummaryService.Create(
			dryRun,
			executor,
			valueContract,
			blockedReport);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedMissingPairedInputs, summary.Status);
		Assert.False(summary.HasAllPairedInputs);
		Assert.False(summary.CanCompareRows);
		Assert.Contains("not fully paired", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonReadinessStage.ExecutorSkeleton
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Blocked
			&& stage.Evidence.Contains("hasAllPairedInputs=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_PairedInputsStillBlockAtValueProjection()
	{
		var dryRun = ReadyDryRun();
		var executor = PairedExecutor();
		var valueContract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create(executor);
		var blockedReport = FindGroupMutationPostProjectedRowComparisonBlockedResultReportService.Create(executor, valueContract);

		var summary = FindGroupMutationPostProjectedRowComparisonReadinessSummaryService.Create(
			dryRun,
			executor,
			valueContract,
			blockedReport);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedValueProjectionDeferred, summary.Status);
		Assert.True(summary.HasAllPairedInputs);
		Assert.False(summary.CanCompareRows);
		Assert.False(summary.CanProjectValues);
		Assert.False(summary.CanEmitResults);
		Assert.Contains("value projection is still deferred", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonReadinessStage.ValueContract
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Deferred
			&& stage.BlocksComparison
			&& stage.Evidence.Contains("canProjectValues=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ResultEmissionUnavailableKeepsSummaryNonLiveAndBlocked()
	{
		var dryRun = ReadyDryRun();
		var executor = PairedExecutor();
		var valueContract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create(executor);
		var blockedReport = FindGroupMutationPostProjectedRowComparisonBlockedResultReportService.Create(executor, valueContract);

		var summary = FindGroupMutationPostProjectedRowComparisonReadinessSummaryService.Create(
			dryRun,
			executor,
			valueContract,
			blockedReport);

		Assert.False(summary.IsLive);
		Assert.False(summary.CanEmitResults);
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonReadinessStage.BlockedResultReport
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Blocked
			&& stage.BlocksComparison
			&& stage.HasExpectedShape
			&& stage.Notes.Contains("No projected-row comparison results emitted", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonDryRunContract ReadyDryRun() =>
		new(
			FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor,
			Actions:
			[
				Action(2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment),
				Action(6, FindGroupDirectPacketMutationPostTraceMutationKind.Application),
			],
			AcceptedJavaRows: [],
			AcceptedCSharpRows: [],
			PairedRowReadiness: [],
			Fields:
			[
				new FindGroupMutationPostProjectedRowComparisonDryRunField(
					1,
					2,
					FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment,
					"postedSystemMessageId",
					FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus.RequiredEqualityInput,
					FindGroupMutationPostComparisonDifferenceKind.DirectPacketMismatch,
					"shape",
					"FindGroupService.addRecruitment",
					"test")
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

	private static FindGroupMutationPostProjectedRowComparisonDryRunAction Action(
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind) =>
		new(
			action,
			mutationKind,
			action == 2 ? "FindGroupService.addRecruitment" : "FindGroupService.addApplication",
			"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
			action == 2 ? 1400392 : 1400393,
			action == 2 ? 0 : 4,
			"matched shape");

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeleton MissingPairExecutor() =>
		Executor(
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.BlockedMissingPairedRows,
			hasAllPairedInputs: false);

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeleton PairedExecutor() =>
		Executor(
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.ReadyForFutureValueComparisonButDeferred,
			hasAllPairedInputs: true);

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeleton Executor(
		FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus status,
		bool hasAllPairedInputs) =>
		new(
			status,
			[
				ExecutorRow(1, 2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment, hasAllPairedInputs),
				ExecutorRow(2, 6, FindGroupDirectPacketMutationPostTraceMutationKind.Application, hasAllPairedInputs),
			],
			HasDryRunContract: true,
			HasResultSkeleton: true,
			hasAllPairedInputs,
			ShouldAttemptExecutor: true,
			CanCompareValues: false,
			hasAllPairedInputs ? "deferred" : "missing pair",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow ExecutorRow(
		int order,
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind,
		bool hasPair) =>
		new(
			order,
			action,
			mutationKind,
			"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
			hasPair
				? FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedValueComparisonDeferred
				: FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedMissingCSharpRow,
			HasAcceptedJavaRow: true,
			HasAcceptedCSharpRow: hasPair,
			ComparesValues: false,
			"shape",
			$"action={action}",
			hasPair ? "paired" : "missing C#");
}
