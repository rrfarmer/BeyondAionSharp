using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistServiceTests
{
	[Fact]
	public void Create_DefaultChecklistBlocksBeforeReadinessSummary()
	{
		var checklist = FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus.BlockedReadinessSummaryNotReady, checklist.Status);
		Assert.False(checklist.IsLive);
		Assert.True(checklist.HasValueReaderReadinessSummary);
		Assert.True(checklist.HasTypedReaderBlockers);
		Assert.True(checklist.HasMismatchContextBlockers);
		Assert.False(checklist.CanImplementTypedReaders);
		Assert.False(checklist.CanAttachMismatchContext);
		Assert.False(checklist.CanReadValues);
		Assert.False(checklist.CanCompareValues);
		Assert.False(checklist.CanEmitComparisonResult);
		Assert.Equal(2, checklist.Rows.Count);
		Assert.Contains("readiness summary metadata", checklist.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", checklist.TraceName);
		Assert.Contains("addRecruitment/addApplication", checklist.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_SeparatesTypedReaderAndMismatchContextBlockers()
	{
		var checklist = FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistService.Create();

		Assert.Contains(checklist.Rows, row =>
			row.Area == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessArea.TypedEqualityReaders
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus.BlockedReadinessSummaryNotReady
			&& row.BlockingFieldCount == 38
			&& !row.CanImplement
			&& !row.CanReadValues
			&& !row.CanAttachContext
			&& row.ExistingMetadataProvider.Contains("ValueReaderPreflightContractService", StringComparison.Ordinal)
			&& row.Evidence.Contains("readerKinds=6", StringComparison.Ordinal)
			&& row.Notes.Contains("separate from runtime-only mismatch context", StringComparison.Ordinal));
		Assert.Contains(checklist.Rows, row =>
			row.Area == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessArea.MismatchContextAttachment
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus.BlockedReadinessSummaryNotReady
			&& row.BlockingFieldCount == 4
			&& !row.CanAttachContext
			&& row.ExistingMetadataProvider.Contains("MismatchContextPreflightContractService", StringComparison.Ordinal)
			&& row.Evidence.Contains("contextFields=traceSource/serverEpochSeconds", StringComparison.Ordinal)
			&& row.Evidence.Contains("MissingJavaRow/MissingCSharpRow/FieldMismatch", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyMetadataStillBlocksImplementation()
	{
		var design = ReadyDesignContract();
		var typedPreflight = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);
		var mismatchContextPreflight = FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create(typedPreflight);
		var skeleton = ValueReaderSkeleton(design, hasJava: true, hasCSharp: true);
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(skeleton);
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create(
			design,
			typedPreflight,
			mismatchContextPreflight,
			skeleton,
			report);

		var checklist = FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistService.Create(
			summary,
			typedPreflight,
			mismatchContextPreflight);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistStatus.BlockedImplementationDeferred, checklist.Status);
		Assert.Contains("typed readers and mismatch-context attachment remain intentionally unimplemented", checklist.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(checklist.Rows, row =>
			row.Area == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessArea.TypedEqualityReaders
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus.BlockedTypedReadersNotImplemented
			&& row.Evidence.Contains("preflightStatus=BlockedTypedReadersDeferred", StringComparison.Ordinal));
		Assert.Contains(checklist.Rows, row =>
			row.Area == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessArea.MismatchContextAttachment
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessRowStatus.BlockedContextAttachmentDeferred
			&& row.Evidence.Contains("contextStatus=BlockedContextAttachmentDeferred", StringComparison.Ordinal)
			&& row.Notes.Contains("must never enable Matched output", StringComparison.Ordinal));
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

	private static FindGroupMutationPostProjectedRowComparisonValueReaderSkeleton ValueReaderSkeleton(
		FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract design,
		bool hasJava,
		bool hasCSharp)
	{
		var dryRun = new FindGroupMutationPostProjectedRowComparisonDryRunContract(
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

		return FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create(design, dryRun);
	}

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
