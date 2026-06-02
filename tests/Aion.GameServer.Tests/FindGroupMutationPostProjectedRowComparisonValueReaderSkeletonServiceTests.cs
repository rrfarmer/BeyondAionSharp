using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonServiceTests
{
	[Fact]
	public void Create_DefaultSkeletonBlocksBeforeDesignReadinessAndDoesNotRead()
	{
		var skeleton = FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedDesignNotReady, skeleton.Status);
		Assert.False(skeleton.IsLive);
		Assert.True(skeleton.HasDesignContract);
		Assert.True(skeleton.HasDryRunContract);
		Assert.False(skeleton.HasAcceptedJavaRows);
		Assert.False(skeleton.HasAcceptedCSharpRows);
		Assert.False(skeleton.HasAllPairedRows);
		Assert.False(skeleton.CanReadValues);
		Assert.False(skeleton.CanCompareValues);
		Assert.Equal(42, skeleton.Attempts.Count);
		Assert.All(skeleton.Attempts, attempt =>
		{
			Assert.False(attempt.AttemptsJavaRead);
			Assert.False(attempt.AttemptsCSharpRead);
			Assert.False(attempt.CanReadValue);
		});
	}

	[Fact]
	public void Create_DefaultSkeletonReportsMissingJavaRowsForRequiredFields()
	{
		var skeleton = FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create();

		Assert.Contains(skeleton.Attempts, attempt =>
			attempt.Action == 2
			&& attempt.FieldName == "postedSystemMessageId"
			&& attempt.Status == FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedMissingJavaRow
			&& attempt.JavaJsonPath == "$.traces[*].postedSystemMessageId"
			&& attempt.CSharpAccessor == "FindGroupDirectPacketMutationPostBoundaryTraceExport.PostedSystemMessageId"
			&& attempt.Notes.Contains("accepted Java runtime row", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_IgnoresRuntimeContextAttemptsWithoutReadingValues()
	{
		var skeleton = FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create();

		Assert.Contains(skeleton.Attempts, attempt =>
			attempt.FieldName == "traceSource"
			&& attempt.Status == FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.IgnoredRuntimeContextOnly
			&& !attempt.AttemptsJavaRead
			&& !attempt.AttemptsCSharpRead);
		Assert.Contains(skeleton.Attempts, attempt =>
			attempt.FieldName == "serverEpochSeconds"
			&& attempt.Status == FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.IgnoredRuntimeContextOnly);
	}

	[Fact]
	public void Create_JavaOnlyRowsBlockMissingCSharpRows()
	{
		var design = ReadyDesignContract();
		var dryRun = DryRun(hasJava: true, hasCSharp: false);

		var skeleton = FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create(design, dryRun);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedMissingAcceptedRows, skeleton.Status);
		Assert.True(skeleton.HasAcceptedJavaRows);
		Assert.False(skeleton.HasAcceptedCSharpRows);
		Assert.Contains(skeleton.Attempts, attempt =>
			attempt.Action == 6
			&& attempt.FieldName == "refreshedListAction"
			&& attempt.HasAcceptedJavaRow
			&& !attempt.HasAcceptedCSharpRow
			&& attempt.Status == FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedMissingCSharpRow);
	}

	[Fact]
	public void Create_PairedRowsStillDeferReaderImplementation()
	{
		var design = ReadyDesignContract();
		var dryRun = DryRun(hasJava: true, hasCSharp: true);

		var skeleton = FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create(design, dryRun);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonStatus.BlockedReaderImplementationDeferred, skeleton.Status);
		Assert.True(skeleton.HasAcceptedJavaRows);
		Assert.True(skeleton.HasAcceptedCSharpRows);
		Assert.True(skeleton.HasAllPairedRows);
		Assert.False(skeleton.CanReadValues);
		Assert.Contains("deferred", skeleton.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(skeleton.Attempts, attempt =>
			attempt.Action == 2
			&& attempt.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& attempt.Status == FindGroupMutationPostProjectedRowComparisonValueReadAttemptStatus.BlockedReaderImplementationDeferred
			&& attempt.HasAcceptedJavaRow
			&& attempt.HasAcceptedCSharpRow
			&& attempt.Notes.Contains("does not read or compare", StringComparison.Ordinal));
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
