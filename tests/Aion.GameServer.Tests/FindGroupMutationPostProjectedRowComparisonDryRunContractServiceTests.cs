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
		Assert.False(contract.ShouldCompareRows);
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

		var contract = FindGroupMutationPostProjectedRowComparisonDryRunContractService.Create(readyReport);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor, contract.Status);
		Assert.True(contract.ShouldCompareRows);
		Assert.Contains("future executor may compare", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.False(contract.IsLive);
	}
}
