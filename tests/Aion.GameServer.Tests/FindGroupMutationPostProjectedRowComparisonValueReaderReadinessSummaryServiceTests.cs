using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryServiceTests
{
	[Fact]
	public void Create_DefaultSummaryBlocksBeforeDesignReadiness()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedDesignNotReady, summary.Status);
		Assert.False(summary.IsLive);
		Assert.True(summary.HasDesignContract);
		Assert.True(summary.HasValueReaderSkeleton);
		Assert.True(summary.HasBlockedResultReport);
		Assert.True(summary.HasRequiredFieldMappings);
		Assert.False(summary.HasAllPairedRows);
		Assert.False(summary.CanReadValues);
		Assert.False(summary.CanCompareValues);
		Assert.False(summary.CanEmitComparisonResult);
		Assert.True(summary.HasPreflightContract);
		Assert.Equal(4, summary.Stages.Count);
		Assert.Contains("design/runtime-evidence readiness", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", summary.TraceName);
		Assert.Contains("addRecruitment/addApplication", summary.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultSummaryListsEachValueReaderStage()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.DesignContract,
				FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.PreflightContract,
				FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.ReaderSkeleton,
				FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.BlockedResultReport,
			],
			summary.Stages.Select(stage => stage.Stage));
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.DesignContract
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Blocked
			&& stage.HasExpectedShape
			&& stage.BlocksValueReading
			&& stage.Evidence.Contains("canReadJavaValues=False", StringComparison.Ordinal)
			&& stage.Evidence.Contains("canReadCSharpValues=False", StringComparison.Ordinal));
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.PreflightContract
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Blocked
			&& stage.HasExpectedShape
			&& stage.BlocksValueReading
			&& stage.Evidence.Contains("readerKinds=6", StringComparison.Ordinal)
			&& stage.Evidence.Contains("hasSchemaV1TypeMap=True", StringComparison.Ordinal)
			&& stage.Evidence.Contains("canReadJavaValues=False", StringComparison.Ordinal)
			&& stage.Evidence.Contains("canReadCSharpValues=False", StringComparison.Ordinal));
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.BlockedResultReport
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Blocked
			&& stage.HasExpectedShape
			&& stage.BlocksValueReading
			&& stage.Evidence.Contains("missingJavaRows=", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_JavaOnlyRowsBlockAtMissingAcceptedRows()
	{
		var design = ReadyDesignContract();
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);
		var skeleton = ValueReaderSkeleton(design, hasJava: true, hasCSharp: false);
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(skeleton);

		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create(design, preflight, skeleton, report);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedMissingAcceptedRows, summary.Status);
		Assert.False(summary.HasAllPairedRows);
		Assert.False(summary.CanReadValues);
		Assert.Contains("not fully paired", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.ReaderSkeleton
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.ReadyForFutureInput
			&& stage.BlocksValueReading
			&& stage.Evidence.Contains("hasAllPairedRows=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_PairedRowsStillBlockAtReaderImplementation()
	{
		var design = ReadyDesignContract();
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);
		var skeleton = ValueReaderSkeleton(design, hasJava: true, hasCSharp: true);
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(skeleton);

		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create(design, preflight, skeleton, report);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedReaderImplementationDeferred, summary.Status);
		Assert.True(summary.HasAllPairedRows);
		Assert.False(summary.CanReadValues);
		Assert.False(summary.CanCompareValues);
		Assert.False(summary.CanEmitComparisonResult);
		Assert.Contains("reader implementation is intentionally deferred", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.PreflightContract
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Deferred
			&& stage.BlocksValueReading
			&& stage.Evidence.Contains("status=BlockedTypedReadersDeferred", StringComparison.Ordinal));
		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.ReaderSkeleton
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Deferred
			&& !stage.BlocksValueReading
			&& stage.Evidence.Contains("canReadValues=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_BlockedReportStageCarriesReaderBlockerCountsWithoutReading()
	{
		var design = ReadyDesignContract();
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);
		var skeleton = ValueReaderSkeleton(design, hasJava: true, hasCSharp: true);
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(skeleton);

		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create(design, preflight, skeleton, report);

		Assert.Contains(summary.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStage.BlockedResultReport
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessStageStatus.Blocked
			&& stage.BlocksValueReading
			&& stage.Evidence.Contains("totalAttempts=42", StringComparison.Ordinal)
			&& stage.Evidence.Contains("deferredReaderImplementation=", StringComparison.Ordinal)
			&& stage.Notes.Contains("field value reading is intentionally deferred", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton ValueReaderSkeleton(
		FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract design,
		bool hasJava,
		bool hasCSharp)
	{
		var dryRun = DryRun(hasJava, hasCSharp);
		return FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create(design, dryRun);
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

	private static FindGroupMutationPostProjectedRowComparisonDryRunContract DryRun(bool hasJava, bool hasCSharp) =>
		new(
			FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor,
			Actions: [],
			AcceptedJavaRows: hasJava ? [JavaRow(1, 2), JavaRow(2, 6)] : [],
			AcceptedCSharpRows: hasCSharp ? [CSharpRow(1, 2), CSharpRow(2, 6)] : [],
			PairedRowReadiness:
			[
				PairedRow(1, 2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment, hasJava, hasCSharp),
				PairedRow(2, 6, FindGroupDirectPacketMutationPostTraceMutationKind.Application, hasJava, hasCSharp),
			],
			Fields: [],
			OutputKinds: [],
			HasExecutionBlockerReport: true,
			HasResultContract: true,
			HasJavaArtifactDirectoryReport: hasJava,
			HasGuardedFixtureResultContract: hasCSharp,
			ShouldCompareRows: true,
			"future executor may compare",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonDryRunAcceptedJavaRowReference JavaRow(int order, int action) =>
		new(
			order,
			action,
			action == 2 ? "Recruitment" : "Application",
			"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
			IsShapeValidJavaArtifact: true,
			$"java action={action}",
			"test");

	private static FindGroupMutationPostProjectedRowComparisonDryRunAcceptedCSharpRowReference CSharpRow(int order, int action) =>
		new(
			order,
			action,
			action == 2 ? FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment : FindGroupDirectPacketMutationPostTraceMutationKind.Application,
			FindGroupMutationPostGuardedFixtureCandidateRowStatus.AcceptedLiveBoundaryRow,
			"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
			IsAcceptedLiveBoundaryEvidence: true,
			$"csharp action={action}",
			"test");

	private static FindGroupMutationPostProjectedRowComparisonDryRunPairedRowReadiness PairedRow(
		int order,
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind,
		bool hasJava,
		bool hasCSharp) =>
		new(
			order,
			action,
			mutationKind,
			"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
			hasJava,
			hasCSharp,
			hasJava && hasCSharp,
			$"action={action}; hasJavaRow={hasJava}; hasCSharpRow={hasCSharp}",
			"test");
}
