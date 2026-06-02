using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractServiceTests
{
	[Fact]
	public void Create_DefaultRunbookBlocksBeforeReadinessSummary()
	{
		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReadinessSummaryNotReady, runbook.Status);
		Assert.False(runbook.IsLive);
		Assert.True(runbook.HasValueReaderPreflight);
		Assert.True(runbook.HasImplementationReadinessChecklist);
		Assert.True(runbook.HasMismatchContextPreflight);
		Assert.Equal(38, runbook.TotalEqualityFieldCount);
		Assert.Equal(4, runbook.TotalContextFieldCount);
		Assert.False(runbook.CanImplementReaders);
		Assert.False(runbook.CanReadValues);
		Assert.False(runbook.CanCompareValues);
		Assert.False(runbook.CanAttachContext);
		Assert.False(runbook.CanEmitComparisonResult);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", runbook.TraceName);
		Assert.Contains("addRecruitment/addApplication", runbook.JavaSource, StringComparison.Ordinal);
		Assert.Contains("readiness-summary metadata", runbook.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_OrdersReaderImplementationWithoutEnablingReads()
	{
		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.TypedScalarEqualityReaders,
				FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.OrderedListEqualityReaders,
				FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.EnumAndStringEqualityReaders,
				FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.MismatchContextAttachment,
			],
			runbook.Steps.Select(step => step.Step));
		Assert.All(runbook.Steps, step =>
		{
			Assert.False(step.CanImplement);
			Assert.False(step.CanReadValues);
			Assert.False(step.CanCompareValues);
			Assert.False(step.CanAttachContext);
		});
	}

	[Fact]
	public void Create_GroupsSchemaV1ReaderKindsByImplementationPhase()
	{
		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService.Create();

		Assert.Contains(runbook.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.TypedScalarEqualityReaders
			&& step.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus.BlockedReadinessSummaryNotReady
			&& step.EqualityFieldCount == 26
			&& step.ContextFieldCount == 0
			&& step.RequiresJavaReader
			&& step.RequiresCSharpReader
			&& !step.PreservesCollectionOrder
			&& step.ReaderKinds.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar)
			&& step.ReaderKinds.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.BooleanScalar)
			&& step.Evidence.Contains("equalityFields=26", StringComparison.Ordinal));
		Assert.Contains(runbook.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.OrderedListEqualityReaders
			&& step.EqualityFieldCount == 2
			&& step.PreservesCollectionOrder
			&& step.ReaderKinds.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.OrderedInt32List)
			&& step.ImplementationOrder.Contains("preserve Java materialized visible-entry ordering exactly", StringComparison.Ordinal));
		Assert.Contains(runbook.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.EnumAndStringEqualityReaders
			&& step.EqualityFieldCount == 10
			&& step.ReaderKinds.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.StringScalar)
			&& step.ReaderKinds.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.EnumStringScalar)
			&& step.Notes.Contains("case and enum-name spelling must match Java JSON", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_AttachesMismatchContextLastOnlyAfterRealResults()
	{
		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService.Create();

		Assert.Contains(runbook.Steps, step =>
			step.Order == 4
			&& step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.MismatchContextAttachment
			&& step.ContextFieldCount == 4
			&& step.EqualityFieldCount == 0
			&& !step.RequiresJavaReader
			&& !step.RequiresCSharpReader
			&& step.ReaderKinds.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext)
			&& step.Evidence.Contains("traceSource/serverEpochSeconds", StringComparison.Ordinal)
			&& step.Evidence.Contains("MissingJavaRow/MissingCSharpRow/FieldMismatch", StringComparison.Ordinal)
			&& step.ImplementationOrder.Contains("only after real MissingJavaRow, MissingCSharpRow, or FieldMismatch result rows exist", StringComparison.Ordinal)
			&& step.Notes.Contains("must not enable Matched output", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyMetadataStillDefersRunbookExecution()
	{
		var design = ReadyDesignContract();
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);
		var mismatchContextPreflight = FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create(preflight);
		var skeleton = ValueReaderSkeleton(design);
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(skeleton);
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create(
			design,
			preflight,
			mismatchContextPreflight,
			skeleton,
			report);
		var checklist = FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistService.Create(
			summary,
			preflight,
			mismatchContextPreflight);

		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService.Create(
			checklist,
			preflight,
			mismatchContextPreflight);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStatus.BlockedReaderImplementationDeferred, runbook.Status);
		Assert.Contains("reader implementation, value reads, comparison, context attachment, and result emission remain intentionally deferred", runbook.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(runbook.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.TypedScalarEqualityReaders
			&& step.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus.BlockedReaderImplementationDeferred
			&& step.Evidence.Contains("preflightStatus=BlockedTypedReadersDeferred", StringComparison.Ordinal));
		Assert.Contains(runbook.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStep.MismatchContextAttachment
			&& step.Status == FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookStepStatus.BlockedContextAttachmentDeferred
			&& step.Evidence.Contains("contextStatus=BlockedContextAttachmentDeferred", StringComparison.Ordinal));
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
		FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract design)
	{
		var dryRun = new FindGroupMutationPostProjectedRowComparisonDryRunContract(
			FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor,
			Actions: [],
			AcceptedJavaRows: [JavaRow(1, 2), JavaRow(2, 6)],
			AcceptedCSharpRows: [CSharpRow(1, 2), CSharpRow(2, 6)],
			PairedRowReadiness:
			[
				PairedRow(1, 2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment),
				PairedRow(2, 6, FindGroupDirectPacketMutationPostTraceMutationKind.Application),
			],
			Fields: [],
			OutputKinds: [],
			HasExecutionBlockerReport: true,
			HasResultContract: true,
			HasJavaArtifactDirectoryReport: true,
			HasGuardedFixtureResultContract: true,
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
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind) =>
		new(
			order,
			action,
			mutationKind,
			"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
			HasAcceptedJavaRow: true,
			HasAcceptedCSharpRow: true,
			IsReadyForFutureComparisonInput: true,
			$"action={action}; hasJavaRow=True; hasCSharpRow=True",
			"test");
}
