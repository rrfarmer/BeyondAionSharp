using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonBlockedResultReportServiceTests
{
	[Fact]
	public void Create_DefaultReportBlocksBeforeAnyResultEmission()
	{
		var report = FindGroupMutationPostProjectedRowComparisonBlockedResultReportService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedExecutorSkeletonNotReady, report.Status);
		Assert.False(report.IsLive);
		Assert.True(report.HasExecutorSkeleton);
		Assert.True(report.HasValueContract);
		Assert.False(report.HasAllPairedInputs);
		Assert.False(report.CanEmitMatched);
		Assert.False(report.CanEmitMissingJavaRow);
		Assert.False(report.CanEmitMissingCSharpRow);
		Assert.False(report.CanEmitFieldMismatch);
		Assert.False(report.CanEmitIgnoredRuntimeContext);
		Assert.Equal(5, report.Rows.Count);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Contains("addRecruitment/addApplication", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("executor skeleton is not ready", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.All(report.Rows, row =>
		{
			Assert.False(row.CanEmitResult);
			Assert.NotEmpty(row.RequiredInput);
			Assert.NotEmpty(row.Blocker);
		});
	}

	[Fact]
	public void Create_ListsMatchedAndFieldMismatchAsUnavailable()
	{
		var report = FindGroupMutationPostProjectedRowComparisonBlockedResultReportService.Create();

		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableExecutorSkeletonBlocked
			&& !row.CanEmitResult
			&& row.RequiredInput.Contains("no mismatches", StringComparison.Ordinal)
			&& row.Evidence.Contains("canEmitMatched=False", StringComparison.Ordinal)
			&& row.Notes.Contains("found equal", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch
			&& row.Status == FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableExecutorSkeletonBlocked
			&& !row.CanEmitResult
			&& row.RequiredInput.Contains("first differing field", StringComparison.Ordinal)
			&& row.Evidence.Contains("canEmitFieldMismatch=False", StringComparison.Ordinal)
			&& row.Notes.Contains("difference is selected", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingValueSourcesBlocksMissingRowAndComparisonOutputs()
	{
		var report = FindGroupMutationPostProjectedRowComparisonBlockedResultReportService.Create(
			valueContract: MissingValueSourcesContract());

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedMissingValueSources, report.Status);
		Assert.False(report.HasAllPairedInputs);
		Assert.Contains("value sources are incomplete", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow
			&& row.Status == FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableMissingValueSources
			&& row.Blocker.Contains("value sources are missing", StringComparison.Ordinal)
			&& row.Evidence.Contains("hasAllPairedInputs=False", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableMissingValueSources);
	}

	[Fact]
	public void Create_PairedInputsStillDeferMatchedAndFieldMismatchOutputs()
	{
		var report = FindGroupMutationPostProjectedRowComparisonBlockedResultReportService.Create(
			valueContract: PairedDeferredValueContract());

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonBlockedResultReportStatus.BlockedValueComparisonUnavailable, report.Status);
		Assert.True(report.HasAllPairedInputs);
		Assert.Contains("value projection and comparison are still deferred", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.All(report.Rows, row => Assert.False(row.CanEmitResult));
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableValueProjectionDeferred
			&& row.Blocker.Contains("future executor must compare Java/C# values", StringComparison.Ordinal)
			&& row.Evidence.Contains("equalityFields=17", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch
			&& row.Status == FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableValueProjectionDeferred
			&& row.Evidence.Contains("canEmitFieldMismatch=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_IgnoredRuntimeContextIsUnavailableUntilRealMismatch()
	{
		var report = FindGroupMutationPostProjectedRowComparisonBlockedResultReportService.Create();

		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& row.Status == FindGroupMutationPostProjectedRowComparisonBlockedResultRowStatus.UnavailableRuntimeContextOnly
			&& !row.CanEmitResult
			&& row.Blocker.Contains("Runtime context rows are unavailable", StringComparison.Ordinal)
			&& row.Evidence.Contains("traceSource/serverEpochSeconds", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueContract MissingValueSourcesContract() =>
		ValueContract(
			FindGroupMutationPostProjectedRowComparisonValueContractStatus.BlockedMissingValueSources,
			hasAllPairedInputs: false);

	private static FindGroupMutationPostProjectedRowComparisonValueContract PairedDeferredValueContract() =>
		ValueContract(
			FindGroupMutationPostProjectedRowComparisonValueContractStatus.ReadyForFutureValueProjectionButDeferred,
			hasAllPairedInputs: true);

	private static FindGroupMutationPostProjectedRowComparisonValueContract ValueContract(
		FindGroupMutationPostProjectedRowComparisonValueContractStatus status,
		bool hasAllPairedInputs)
	{
		var baseContract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create();
		return new FindGroupMutationPostProjectedRowComparisonValueContract(
			status,
			baseContract.Fields,
			baseContract.EqualityProjectionFields,
			baseContract.IgnoredRuntimeFields,
			HasExecutorSkeleton: true,
			HasResultContract: true,
			hasAllPairedInputs,
			CanProjectValues: false,
			CanEmitMatched: false,
			CanEmitFieldMismatch: false,
			"test value contract",
			baseContract.TraceName,
			baseContract.JavaSource,
			IsLive: false);
	}
}
