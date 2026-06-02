using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonDryRunContractServiceTests
{
	[Fact]
	public void Create_DefaultDryRunBlocksAndDoesNotCompareRows()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonDryRunStatus.BlockedByExecutionReport, contract.Status);
		Assert.False(contract.IsLive);
		Assert.True(contract.HasExecutionBlockerReport);
		Assert.True(contract.HasResultContract);
		Assert.True(contract.HasJavaArtifactDirectoryReport);
		Assert.True(contract.HasGuardedFixtureResultContract);
		Assert.False(contract.ShouldCompareRows);
		Assert.Empty(contract.AcceptedJavaRows);
		Assert.Empty(contract.AcceptedCSharpRows);
		Assert.Equal([2, 6], contract.PairedRowReadiness.Select(row => row.Action));
		Assert.All(contract.PairedRowReadiness, row =>
		{
			Assert.False(row.HasAcceptedJavaRow);
			Assert.False(row.HasAcceptedCSharpRow);
			Assert.False(row.IsReadyForFutureComparisonInput);
			Assert.Contains("does not compare Java/C# values", row.Notes, StringComparison.Ordinal);
		});
		Assert.Contains("Comparison not executed", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", contract.TraceName);
		Assert.Contains("addRecruitment/addApplication", contract.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_CoversActionTwoAndSixWithJavaPacketExpectations()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create();

		Assert.Equal([2, 6], contract.Actions.Select(action => action.Action));
		Assert.Contains(contract.Actions, action =>
			action.Action == 2
			&& action.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
			&& action.ExpectedPostedSystemMessageId == 1400392
			&& action.ExpectedRefreshedListAction == 0
			&& action.JavaMethod.Contains("addRecruitment", StringComparison.Ordinal));
		Assert.Contains(contract.Actions, action =>
			action.Action == 6
			&& action.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
			&& action.ExpectedPostedSystemMessageId == 1400393
			&& action.ExpectedRefreshedListAction == 4
			&& action.JavaMethod.Contains("addApplication", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DefinesPlannedOutputKindsWithoutProducingResults()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			],
			contract.OutputKinds);
		Assert.All(contract.Actions, action =>
			Assert.Contains("Emit Matched only when all required equality fields match", action.PlannedMatchOutput, StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MapsRequiredFieldsToFieldMismatchOutputShape()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create();

		Assert.Equal(42, contract.Fields.Count);
		Assert.Equal(Enumerable.Range(1, contract.Fields.Count), contract.Fields.Select(field => field.Order));
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "postedSystemMessageId"
			&& field.Status == FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus.RequiredEqualityInput
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.DirectPacketMismatch
			&& field.PlannedOutputShape.Contains("javaValue", StringComparison.Ordinal)
			&& field.PlannedOutputShape.Contains("csharpValue", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.MutationStateMismatch
			&& field.JavaSource.Contains("values().stream().filter", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_KeepsRuntimeOnlyFieldsAsContextNotEqualityInputs()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create();

		Assert.Contains(contract.Fields, field =>
			field.FieldName == "traceSource"
			&& field.Status == FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus.IgnoredRuntimeContext
			&& field.DifferenceKind == FindGroupMutationPostComparisonDifferenceKind.RuntimeOnlyIgnored
			&& field.PlannedOutputShape.Contains("IgnoredRuntimeContext", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "serverEpochSeconds"
			&& field.Status == FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus.IgnoredRuntimeContext);
	}

	[Fact]
	public void Create_ReadyBlockerReportAllowsFutureExecutorButStillDryRunOnly()
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

		var contract = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create(
			readyReport,
			javaArtifacts: ShapeValidJavaArtifacts(),
			guardedFixtureResultContract: guardedFixture);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor, contract.Status);
		Assert.True(contract.ShouldCompareRows);
		Assert.Equal(2, contract.AcceptedJavaRows.Count);
		Assert.Equal(2, contract.AcceptedCSharpRows.Count);
		Assert.Equal(2, contract.PairedRowReadiness.Count);
		Assert.All(contract.PairedRowReadiness, row =>
		{
			Assert.True(row.HasAcceptedJavaRow);
			Assert.True(row.HasAcceptedCSharpRow);
			Assert.True(row.IsReadyForFutureComparisonInput);
			Assert.Contains("hasJavaRow=True", row.Evidence, StringComparison.Ordinal);
			Assert.Contains("hasCSharpRow=True", row.Evidence, StringComparison.Ordinal);
			Assert.Contains("still does not compare Java/C# values", row.Notes, StringComparison.Ordinal);
		});
		Assert.Equal([2, 6], contract.AcceptedCSharpRows.Select(row => row.Action));
		Assert.Contains("future executor may compare", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.False(contract.IsLive);
	}

	[Fact]
	public void Create_ProjectsShapeValidJavaRowsAsFutureExecutorInputs()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create(
			javaArtifacts: ShapeValidJavaArtifacts());

		Assert.Equal(2, contract.AcceptedJavaRows.Count);
		Assert.Equal([2, 6], contract.AcceptedJavaRows.Select(row => row.Action));
		Assert.All(contract.AcceptedJavaRows, row =>
		{
			Assert.True(row.IsShapeValidJavaArtifact);
			Assert.Contains("action/mutationKind/activePlayerObjectId/mutatedEntryObjectId", row.RequiredRowIdentity, StringComparison.Ordinal);
			Assert.Contains("Shape-valid Java artifact row", row.PlannedInputSource, StringComparison.Ordinal);
			Assert.Contains("status=ShapeValid", row.Evidence, StringComparison.Ordinal);
		});
		Assert.Contains(contract.AcceptedJavaRows, row =>
			row.Action == 2
			&& row.MutationKind == "Recruitment"
			&& row.Evidence.Contains("posted=1400392", StringComparison.Ordinal)
			&& row.Evidence.Contains("refreshed=0", StringComparison.Ordinal));
		Assert.Contains(contract.AcceptedJavaRows, row =>
			row.Action == 6
			&& row.MutationKind == "Application"
			&& row.Evidence.Contains("posted=1400393", StringComparison.Ordinal)
			&& row.Evidence.Contains("refreshed=4", StringComparison.Ordinal));
		Assert.All(contract.PairedRowReadiness, row =>
		{
			Assert.True(row.HasAcceptedJavaRow);
			Assert.False(row.HasAcceptedCSharpRow);
			Assert.False(row.IsReadyForFutureComparisonInput);
			Assert.Contains("hasCSharpRow=False", row.Evidence, StringComparison.Ordinal);
		});
		Assert.False(contract.ShouldCompareRows);
	}

	[Fact]
	public void Create_ProjectsAcceptedGuardedCSharpRowsAsFutureExecutorInputs()
	{
		var guardedFixture = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				LiveCSharpRow(2),
				FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(6),
			]);

		var contract = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create(
			guardedFixtureResultContract: guardedFixture);

		var accepted = Assert.Single(contract.AcceptedCSharpRows);
		Assert.Equal(1, accepted.Order);
		Assert.Equal(2, accepted.Action);
		Assert.Equal(FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment, accepted.MutationKind);
		Assert.Equal(FindGroupMutationPostGuardedFixtureCandidateRowStatus.AcceptedLiveBoundaryRow, accepted.GuardedStatus);
		Assert.True(accepted.IsAcceptedLiveBoundaryEvidence);
		Assert.Contains("action/mutationKind/activePlayerObjectId/mutatedEntryObjectId", accepted.RequiredRowIdentity, StringComparison.Ordinal);
		Assert.Contains("boundary=True", accepted.Evidence, StringComparison.Ordinal);
		Assert.Contains("Accepted C# row", accepted.PlannedInputSource, StringComparison.Ordinal);
		Assert.Contains(contract.PairedRowReadiness, row =>
			row.Action == 2
			&& !row.HasAcceptedJavaRow
			&& row.HasAcceptedCSharpRow
			&& !row.IsReadyForFutureComparisonInput);
		Assert.Contains(contract.PairedRowReadiness, row =>
			row.Action == 6
			&& !row.HasAcceptedJavaRow
			&& !row.HasAcceptedCSharpRow
			&& !row.IsReadyForFutureComparisonInput);
		Assert.False(contract.ShouldCompareRows);
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
