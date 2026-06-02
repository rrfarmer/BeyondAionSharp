using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportServiceTests
{
	[Fact]
	public void Create_DefaultReportSummarizesMissingJavaRowsWithoutReadingValues()
	{
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedValueReaderSkeletonNotReady, report.Status);
		Assert.False(report.IsLive);
		Assert.True(report.HasValueReaderSkeleton);
		Assert.Equal(42, report.TotalAttempts);
		Assert.True(report.MissingJavaRowAttempts > 0);
		Assert.Equal(0, report.MissingCSharpRowAttempts);
		Assert.True(report.IgnoredRuntimeContextAttempts > 0);
		Assert.Equal(0, report.DeferredReaderImplementationAttempts);
		Assert.False(report.AttemptsAnyJavaRead);
		Assert.False(report.AttemptsAnyCSharpRead);
		Assert.False(report.CanReadValues);
		Assert.False(report.CanCompareValues);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Contains("addRecruitment/addApplication", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("skeleton is not ready", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultReportRowsExposeCountsAndBlockers()
	{
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.MissingJavaRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.MissingCSharpRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.IgnoredRuntimeContext,
				FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.DeferredReaderImplementation,
			],
			report.Rows.Select(row => row.Kind));
		Assert.All(report.Rows, row =>
		{
			Assert.False(row.CanReadValues);
			Assert.False(row.CanEmitComparisonResult);
			Assert.Contains("attemptCount=", row.Evidence, StringComparison.Ordinal);
		});
		Assert.Contains(report.Rows, row =>
			row.Kind == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.MissingJavaRows
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus.UnavailableSkeletonBlocked
			&& row.AttemptCount == report.MissingJavaRowAttempts
			&& row.Notes.Contains("no Java artifact values were parsed", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_JavaOnlyRowsSummarizeMissingCSharpRows()
	{
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(
			ValueReaderSkeleton(hasJava: true, hasCSharp: false));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedMissingAcceptedRows, report.Status);
		Assert.Equal(0, report.MissingJavaRowAttempts);
		Assert.True(report.MissingCSharpRowAttempts > 0);
		Assert.Equal(0, report.DeferredReaderImplementationAttempts);
		Assert.Contains("accepted Java/C# row references are incomplete", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(report.Rows, row =>
			row.Kind == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.MissingCSharpRows
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus.Blocked
			&& row.AttemptCount == report.MissingCSharpRowAttempts
			&& row.Notes.Contains("no C# trace-export values were read", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_PairedRowsSummarizeDeferredReaderImplementation()
	{
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(
			ValueReaderSkeleton(hasJava: true, hasCSharp: true));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportStatus.BlockedReaderImplementationDeferred, report.Status);
		Assert.Equal(0, report.MissingJavaRowAttempts);
		Assert.Equal(0, report.MissingCSharpRowAttempts);
		Assert.True(report.IgnoredRuntimeContextAttempts > 0);
		Assert.True(report.DeferredReaderImplementationAttempts > 0);
		Assert.False(report.AttemptsAnyJavaRead);
		Assert.False(report.AttemptsAnyCSharpRead);
		Assert.Contains("field value reading is intentionally deferred", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(report.Rows, row =>
			row.Kind == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.DeferredReaderImplementation
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus.Deferred
			&& row.AttemptCount == report.DeferredReaderImplementationAttempts
			&& row.Notes.Contains("Accepted rows exist", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_IgnoredRuntimeContextCountNeverEnablesResultEmission()
	{
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(
			ValueReaderSkeleton(hasJava: true, hasCSharp: true));

		Assert.Contains(report.Rows, row =>
			row.Kind == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowKind.IgnoredRuntimeContext
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultRowStatus.IgnoredRuntimeContextOnly
			&& row.AttemptCount == report.IgnoredRuntimeContextAttempts
			&& !row.CanReadValues
			&& !row.CanEmitComparisonResult
			&& row.Notes.Contains("real comparison result needs context", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton ValueReaderSkeleton(bool hasJava, bool hasCSharp)
	{
		var design = ReadyDesignContract();
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
