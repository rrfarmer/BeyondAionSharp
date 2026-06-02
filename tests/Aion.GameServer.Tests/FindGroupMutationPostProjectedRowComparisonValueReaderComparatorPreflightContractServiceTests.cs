using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractServiceTests
{
	[Fact]
	public void Create_DefaultPreflightBlocksBeforeResultSchemaReadiness()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedResultSchemaNotReady, preflight.Status);
		Assert.False(preflight.IsLive);
		Assert.True(preflight.HasImplementationRunbook);
		Assert.True(preflight.HasResultSchema);
		Assert.Equal(38, preflight.EqualityFieldCount);
		Assert.Equal(4, preflight.RuntimeContextFieldCount);
		Assert.False(preflight.CanExecuteComparator);
		Assert.False(preflight.CanProjectValues);
		Assert.False(preflight.CanCompareValues);
		Assert.False(preflight.CanAttachRuntimeContext);
		Assert.False(preflight.CanEmitResults);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", preflight.TraceName);
		Assert.Contains("addRecruitment/addApplication", preflight.JavaSource, StringComparison.Ordinal);
		Assert.Contains("result-schema metadata", preflight.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_OrdersFutureComparatorStagesWithoutExecuting()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.RowIdentityPairing,
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.TypedReaderExecution,
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.EqualityValueComparison,
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.ResultSelection,
				FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.MismatchContextAttachment,
			],
			preflight.Stages.Select(stage => stage.Stage));
		Assert.All(preflight.Stages, stage =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedResultSchemaNotReady, stage.Status);
			Assert.False(stage.CanExecute);
			Assert.False(stage.CanProjectValues);
			Assert.False(stage.CanCompareValues);
			Assert.False(stage.CanEmitResults);
		});
	}

	[Fact]
	public void Create_ValueComparisonStageRequiresProjectedValuesAndMatchedOrMismatchOutput()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService.Create();

		Assert.Contains(preflight.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.EqualityValueComparison
			&& stage.RequiresAcceptedJavaRows
			&& stage.RequiresAcceptedCSharpRows
			&& stage.RequiresProjectedValues
			&& stage.RequiresResultSchema
			&& stage.OutputKinds.SequenceEqual(
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
			])
			&& stage.Notes.Contains("Matched only when every equality value matches", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ResultSelectionAndContextAttachmentStaySeparated()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService.Create();

		Assert.Contains(preflight.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.ResultSelection
			&& stage.OutputKinds.Contains(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow)
			&& stage.OutputKinds.Contains(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow)
			&& stage.OutputKinds.Contains(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch)
			&& stage.RequiredProducer.Contains("one deterministic outcome", StringComparison.Ordinal));
		Assert.Contains(preflight.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.MismatchContextAttachment
			&& !stage.RequiresAcceptedJavaRows
			&& !stage.RequiresAcceptedCSharpRows
			&& !stage.RequiresProjectedValues
			&& stage.OutputKinds.SequenceEqual([FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext])
			&& stage.Notes.Contains("must never affect equality", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyResultSchemaStillDefersEveryComparatorStage()
	{
		var resultSchema = ReadyResultSchema();

		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService.Create(resultSchema: resultSchema);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedComparatorImplementationDeferred, preflight.Status);
		Assert.Contains("row pairing, reader execution, value comparison, result selection, context attachment, and result emission remain intentionally deferred", preflight.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(preflight.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.RowIdentityPairing
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedRuntimeRowsMissing);
		Assert.Contains(preflight.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.TypedReaderExecution
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedReaderExecutionDeferred);
		Assert.Contains(preflight.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.EqualityValueComparison
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedComparisonDeferred);
		Assert.Contains(preflight.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.ResultSelection
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedResultSelectionDeferred);
		Assert.Contains(preflight.Stages, stage =>
			stage.Stage == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.MismatchContextAttachment
			&& stage.Status == FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedContextAttachmentDeferred);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContract ReadyResultSchema()
	{
		var runbook = ReadyRunbook();
		return FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService.Create(runbook);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContract ReadyRunbook()
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

		return FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService.Create(
			checklist,
			preflight,
			mismatchContextPreflight);
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
