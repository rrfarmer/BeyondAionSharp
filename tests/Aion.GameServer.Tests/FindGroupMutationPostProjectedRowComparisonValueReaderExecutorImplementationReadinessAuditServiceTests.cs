using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditServiceTests
{
	[Fact]
	public void Create_DefaultAuditBlocksUntilEvidenceSummaryIsReady()
	{
		var audit = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedEvidenceSummaryNotReady, audit.Status);
		Assert.False(audit.IsLive);
		Assert.True(audit.HasImplementationPlan);
		Assert.True(audit.HasEvidenceSummary);
		Assert.False(audit.HasAnyRuntimeEvidence);
		Assert.False(audit.CanWriteExecutableExecutor);
		Assert.False(audit.CanExecuteExecutor);
		Assert.False(audit.CanReadValues);
		Assert.False(audit.CanCompareValues);
		Assert.False(audit.CanEmitResults);
		Assert.False(audit.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", audit.TraceName);
		Assert.Contains("addRecruitment/addApplication", audit.JavaSource, StringComparison.Ordinal);
		Assert.Contains("evidence summary metadata is ready", audit.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultAuditListsEveryImplementationPlanStepAsBlocked()
	{
		var audit = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditService.Create();

		Assert.Equal(Enum.GetValues<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep>(), audit.Rows.Select(row => row.Step));
		Assert.All(audit.Rows, row =>
		{
			Assert.True(row.HasImplementationPlanStep);
			Assert.True(row.HasEvidenceSummary);
			Assert.True(row.BlocksExecutableCode);
			Assert.False(row.CanWriteExecutableCode);
			Assert.False(row.CanExecute);
		});
		Assert.Contains(audit.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment
			&& !row.RequiresRuntimeEvidence
			&& row.RequiredEvidence.Contains("parent MissingJavaRow, MissingCSharpRow, or FieldMismatch", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeMissingAuditNamesRuntimeEvidenceBlockers()
	{
		var audit = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditService.Create(
			Summary(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedRuntimeEvidenceMissing, audit.Status);
		Assert.Contains("runtime evidence required by the executor summary is missing", audit.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(audit.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.RowIdentityPairing
			&& row.RequiresRuntimeEvidence
			&& row.RequiredEvidence.Contains("Runtime-backed Java rows and accepted live C# boundary rows", StringComparison.Ordinal));
		Assert.Contains(audit.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.JavaTypedValueRead
			&& row.RequiredEvidence.Contains("Runtime-backed Java artifact rows", StringComparison.Ordinal));
		Assert.Contains(audit.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.CSharpTypedValueRead
			&& row.RequiredEvidence.Contains("Accepted live C# trace-export rows", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyShapedEvidenceStillDisallowsExecutableComparatorCode()
	{
		var audit = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditService.Create(
			Summary(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedResultEmissionDeferred));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedExecutableImplementationNotAllowed, audit.Status);
		Assert.False(audit.CanWriteExecutableExecutor);
		Assert.False(audit.CanExecuteExecutor);
		Assert.False(audit.CanCompareValues);
		Assert.False(audit.CanEmitResults);
		Assert.False(audit.CanClaimVerifiedParity);
		Assert.Contains("executable reader/comparator code remains disallowed", audit.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(audit.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison
			&& row.CurrentEvidence.Contains("evidenceSummaryStatus=BlockedResultEmissionDeferred", StringComparison.Ordinal)
			&& row.Notes.Contains("ordered-list handling", StringComparison.Ordinal));
		Assert.Contains(audit.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultEmission
			&& row.RequiredEvidence.Contains("runtime comparison evidence", StringComparison.Ordinal)
			&& row.Notes.Contains("Emission remains blocked", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract Summary(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus status) =>
		new(
			status,
			[
				SummaryRow(1, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.BlockedOutputPreview),
				SummaryRow(2, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeEvidenceIntake),
				SummaryRow(3, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.MaterializationPreflight),
				SummaryRow(4, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.ResultEmissionGate),
				SummaryRow(5, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeComparison),
			],
			HasBlockedOutputPreview: true,
			HasRuntimeEvidenceIntake: true,
			HasMaterializationPreflight: true,
			HasResultEmissionGate: true,
			HasAnyRuntimeEvidence: status != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			"Evidence summary remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRow SummaryRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement requirement) =>
		new(
			order,
			requirement,
			requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeComparison
				? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedRuntimeComparisonMissing
				: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedRuntimeEvidenceMissing,
			HasProvider: true,
			HasRuntimeEvidence: false,
			BlocksExecutorImplementation: true,
			BlocksVerifiedParity: true,
			"test provider",
			"test required evidence",
			"test current evidence",
			"test notes");
}
