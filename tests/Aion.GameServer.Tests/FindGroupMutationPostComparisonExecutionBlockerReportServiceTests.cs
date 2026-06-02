using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostComparisonExecutionBlockerReportServiceTests
{
	[Fact]
	public void Create_DefaultReportBlocksOnMissingJavaRowsAndDoesNotExecute()
	{
		var report = FindGroupMutationPostComparisonExecutionBlockerReportService.Create();

		Assert.Equal(FindGroupMutationPostComparisonExecutionBlockerReportStatus.BlockedMissingJavaRows, report.Status);
		Assert.False(report.IsLive);
		Assert.False(report.HasJavaRows);
		Assert.False(report.HasLiveCSharpRows);
		Assert.True(report.HasProjectionMetadata);
		Assert.True(report.HasReadinessAggregate);
		Assert.True(report.HasResultContract);
		Assert.False(report.ShouldExecuteComparison);
		Assert.Contains("Comparison not executed", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
	}

	[Fact]
	public void Create_MapsEnvelopeGateBlockersToExecutionReasons()
	{
		var envelope = new FindGroupMutationPostComparisonInputEnvelope(
			FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingReadiness,
			[
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.JavaRows, FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByShapeValidJavaRows),
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.CSharpRows, FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByLiveCSharpRows),
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.ReadinessAggregate, FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingReadiness),
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.ResultContract, FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingReadiness),
			],
			JavaRows: [],
			CSharpRows: [],
			HasActionTwoJavaRow: true,
			HasActionSixJavaRow: true,
			HasActionTwoLiveCSharpRow: true,
			HasActionSixLiveCSharpRow: true,
			HasProjectionMetadata: true,
			HasReadinessAggregate: true,
			HasResultContract: true,
			ReadyForComparisonExecution: false,
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

		var report = FindGroupMutationPostComparisonExecutionBlockerReportService.Create(envelope);

		Assert.Equal(FindGroupMutationPostComparisonExecutionBlockerReportStatus.BlockedMissingReadiness, report.Status);
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.ReadinessAggregate
			&& row.Reason == FindGroupMutationPostComparisonExecutionBlockerReason.MissingReadinessAggregate
			&& row.BlocksExecution);
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.ResultContract
			&& row.Reason == FindGroupMutationPostComparisonExecutionBlockerReason.MissingResultContract
			&& row.BlocksExecution);
	}

	[Fact]
	public void Create_MissingLiveCSharpRowsBlocksAfterJavaRowsArePresent()
	{
		var envelope = new FindGroupMutationPostComparisonInputEnvelope(
			FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingLiveCSharpRows,
			[
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.JavaRows, FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByShapeValidJavaRows),
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.CSharpRows, FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingLiveCSharpRows),
			],
			JavaRows: [],
			CSharpRows: [],
			HasActionTwoJavaRow: true,
			HasActionSixJavaRow: true,
			HasActionTwoLiveCSharpRow: false,
			HasActionSixLiveCSharpRow: false,
			HasProjectionMetadata: true,
			HasReadinessAggregate: true,
			HasResultContract: true,
			ReadyForComparisonExecution: false,
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

		var report = FindGroupMutationPostComparisonExecutionBlockerReportService.Create(envelope);

		Assert.Equal(FindGroupMutationPostComparisonExecutionBlockerReportStatus.BlockedMissingLiveCSharpRows, report.Status);
		Assert.True(report.HasJavaRows);
		Assert.False(report.HasLiveCSharpRows);
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.CSharpRows
			&& row.Reason == FindGroupMutationPostComparisonExecutionBlockerReason.MissingLiveCSharpRows);
	}

	[Fact]
	public void Create_ReadyEnvelopeAllowsFutureExecutorButStillDoesNotCompareRows()
	{
		var envelope = new FindGroupMutationPostComparisonInputEnvelope(
			FindGroupMutationPostComparisonInputEnvelopeStatus.ReadyForComparisonExecution,
			[
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.JavaRows, FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByShapeValidJavaRows),
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.CSharpRows, FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByLiveCSharpRows),
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.ProjectionMetadata, FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByNonLiveMetadata),
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.ReadinessAggregate, FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByNonLiveMetadata),
				Gate(FindGroupMutationPostComparisonInputEnvelopeGate.ResultContract, FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByReadyContract),
			],
			JavaRows: [],
			CSharpRows: [],
			HasActionTwoJavaRow: true,
			HasActionSixJavaRow: true,
			HasActionTwoLiveCSharpRow: true,
			HasActionSixLiveCSharpRow: true,
			HasProjectionMetadata: true,
			HasReadinessAggregate: true,
			HasResultContract: true,
			ReadyForComparisonExecution: true,
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

		var report = FindGroupMutationPostComparisonExecutionBlockerReportService.Create(envelope);

		Assert.Equal(FindGroupMutationPostComparisonExecutionBlockerReportStatus.ReadyForExecutor, report.Status);
		Assert.True(report.ShouldExecuteComparison);
		Assert.Contains("future executor may compare", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.DoesNotContain(report.Rows, row => row.BlocksExecution);
		Assert.All(report.Rows, row => Assert.Equal(FindGroupMutationPostComparisonExecutionBlockerReason.ReadyNoBlocker, row.Reason));
	}

	private static FindGroupMutationPostComparisonInputEnvelopeGateRow Gate(
		FindGroupMutationPostComparisonInputEnvelopeGate gate,
		FindGroupMutationPostComparisonInputEnvelopeGateStatus status) =>
		new(
			0,
			gate,
			status,
			status is FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingJavaRows
				or FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingLiveCSharpRows
				or FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingReadiness,
			$"status={status}",
			"FindGroupService.addRecruitment/addApplication",
			"FindGroupMutationPostComparisonInputEnvelope",
			"test gate");
}
