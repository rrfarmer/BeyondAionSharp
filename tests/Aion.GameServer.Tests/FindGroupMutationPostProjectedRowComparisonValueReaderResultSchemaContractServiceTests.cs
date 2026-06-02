using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractServiceTests
{
	[Fact]
	public void Create_DefaultSchemaBlocksBeforeRunbookReadiness()
	{
		var schema = FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus.BlockedRunbookNotReady, schema.Status);
		Assert.False(schema.IsLive);
		Assert.True(schema.HasImplementationRunbook);
		Assert.True(schema.HasResultContract);
		Assert.Equal(38, schema.EqualityFieldCount);
		Assert.Equal(4, schema.RuntimeContextFieldCount);
		Assert.False(schema.CanProjectValues);
		Assert.False(schema.CanAttachRuntimeContext);
		Assert.False(schema.CanEmitMatched);
		Assert.False(schema.CanEmitFieldMismatch);
		Assert.False(schema.CanEmitMissingJavaRow);
		Assert.False(schema.CanEmitMissingCSharpRow);
		Assert.False(schema.CanEmitIgnoredRuntimeContext);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", schema.TraceName);
		Assert.Contains("addRecruitment/addApplication", schema.JavaSource, StringComparison.Ordinal);
		Assert.Contains("implementation runbook metadata", schema.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefinesRowsForEveryFutureOutputKindWithoutEmitting()
	{
		var schema = FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			],
			schema.Rows.Select(row => row.OutputKind));
		Assert.All(schema.Rows, row => Assert.False(row.CanEmitResult));
		Assert.All(schema.Rows, row => Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedRunbookNotReady, row.Status));
	}

	[Fact]
	public void Create_MatchedAndFieldMismatchRowsRequireProjectedValues()
	{
		var schema = FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService.Create();

		Assert.Contains(schema.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.RequiresProjectedValues
			&& !row.RequiresMissingRowDecision
			&& !row.AllowsRuntimeContextAttachment
			&& row.SchemaFields.SequenceEqual(["action", "mutationKind", "rowIdentity", "matchedFields", "matchedFieldCount"])
			&& row.Notes.Contains("cannot include ignored runtime context", StringComparison.Ordinal));
		Assert.Contains(schema.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch
			&& row.RequiresProjectedValues
			&& !row.RequiresMissingRowDecision
			&& row.AllowsRuntimeContextAttachment
			&& row.SchemaFields.Contains("javaValue")
			&& row.SchemaFields.Contains("csharpValue")
			&& row.SchemaFields.Contains("runtimeContext")
			&& row.Evidence.Contains("DirectPacketMismatch", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingRowsRequireRowDecisionAndAllowContext()
	{
		var schema = FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService.Create();

		Assert.Contains(schema.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow
			&& !row.RequiresProjectedValues
			&& row.RequiresMissingRowDecision
			&& row.AllowsRuntimeContextAttachment
			&& row.SchemaFields.Contains("csharpRowReference")
			&& row.Notes.Contains("context must not create the decision", StringComparison.Ordinal));
		Assert.Contains(schema.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow
			&& !row.RequiresProjectedValues
			&& row.RequiresMissingRowDecision
			&& row.AllowsRuntimeContextAttachment
			&& row.SchemaFields.Contains("javaRowReference"));
	}

	[Fact]
	public void Create_IgnoredRuntimeContextIsNotStandaloneResult()
	{
		var schema = FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService.Create();

		Assert.Contains(schema.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& !row.RequiresProjectedValues
			&& !row.RequiresMissingRowDecision
			&& row.AllowsRuntimeContextAttachment
			&& row.SchemaFields.SequenceEqual(["traceSource", "serverEpochSeconds"])
			&& row.Evidence.Contains("traceSource/serverEpochSeconds", StringComparison.Ordinal)
			&& row.Notes.Contains("not a standalone comparison result", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyRunbookStillDefersResultEmission()
	{
		var runbook = ReadyRunbook();

		var schema = FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService.Create(runbook);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus.BlockedResultSchemaDeferred, schema.Status);
		Assert.Contains("projected values, missing-row decisions, context attachment, and result emission remain intentionally deferred", schema.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(schema.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedValueProjectionDeferred);
		Assert.Contains(schema.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedMissingRowDecisionDeferred);
		Assert.Contains(schema.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedContextAttachmentDeferred);
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
