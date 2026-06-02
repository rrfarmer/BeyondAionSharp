using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractServiceTests
{
	[Fact]
	public void Create_DefaultHandoffBlocksUntilImplementationAuditIsReady()
	{
		var handoff = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedExecutorConsistencyAuditNotReady, handoff.Status);
		Assert.False(handoff.IsLive);
		Assert.True(handoff.HasExecutorConsistencyAudit);
		Assert.True(handoff.HasImplementationReadinessAudit);
		Assert.False(handoff.HasAnyRuntimeEvidence);
		Assert.False(handoff.CanStartExecutableImplementation);
		Assert.False(handoff.CanStartRuntimeComparison);
		Assert.False(handoff.CanReadValues);
		Assert.False(handoff.CanCompareValues);
		Assert.False(handoff.CanMaterializeOutputs);
		Assert.False(handoff.CanEmitResults);
		Assert.False(handoff.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", handoff.TraceName);
		Assert.Contains("addRecruitment/addApplication", handoff.JavaSource, StringComparison.Ordinal);
		Assert.Contains("executor consistency audit metadata is internally consistent", handoff.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultHandoffListsEveryEvidenceRequirementInOrder()
	{
		var handoff = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractService.Create();

		Assert.Equal(Enum.GetValues<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement>(), handoff.Rows.Select(row => row.Requirement));
		Assert.All(handoff.Rows, row =>
		{
			Assert.True(row.HasImplementationReadinessAudit);
			Assert.True(row.HasExecutorConsistencyAudit);
			Assert.False(row.HasRuntimeEvidence);
			Assert.True(row.RequiredBeforeExecutableImplementation);
			Assert.True(row.RequiredBeforeVerifiedParity);
			Assert.False(row.CanStartExecutableImplementation);
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedExecutorConsistencyAuditNotReady, row.Status);
		});
	}

	[Fact]
	public void Create_ConsistentAuditStillBlocksUntilImplementationAuditIsReady()
	{
		var handoff = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractService.Create(
			ConsistencyAudit(),
			Audit(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedEvidenceSummaryNotReady));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedImplementationAuditNotReady, handoff.Status);
		Assert.Contains("implementation readiness audit metadata is ready", handoff.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(handoff.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutorConsistencyAudit
			&& row.HasExecutorConsistencyAudit
			&& row.CurrentEvidence.Contains("consistencyAuditStatus=ConsistentBlockedReadiness", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeMissingHandoffNamesJavaCSharpValueMaterializationAndEmissionEvidence()
	{
		var handoff = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractService.Create(
			ConsistencyAudit(),
			Audit(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedRuntimeEvidenceMissing));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedRuntimeEvidenceMissing, handoff.Status);
		Assert.Contains("Java artifact rows, C# boundary rows, value projection, materialization, result emission, and runtime comparison evidence are missing", handoff.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(handoff.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows
			&& row.RequiredBeforeRuntimeComparison
			&& row.RequiredEvidence.Contains("CM_FIND_GROUP.readImpl/runImpl", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("FindGroupService.addRecruitment/addApplication", StringComparison.Ordinal));
		Assert.Contains(handoff.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.CSharpBoundaryRows
			&& row.RequiredBeforeRuntimeComparison
			&& row.RequiredEvidence.Contains("Accepted live C# CM_FIND_GROUP boundary rows", StringComparison.Ordinal));
		Assert.Contains(handoff.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ValueProjection
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedValueProjectionMissing
			&& row.RequiredEvidence.Contains("ordered visibleEntryObjectIdsAfterMutation", StringComparison.Ordinal));
		Assert.Contains(handoff.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.Materialization
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedMaterializationMissing
			&& row.RequiredEvidence.Contains("MissingJavaRow", StringComparison.Ordinal));
		Assert.Contains(handoff.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ResultEmission
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedResultEmissionMissing
			&& row.RequiredEvidence.Contains("ignored runtime context", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyShapedAuditStillDefersExecutableImplementationAndParity()
	{
		var handoff = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractService.Create(
			ConsistencyAudit(),
			Audit(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus.BlockedExecutableImplementationNotAllowed));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedExecutableImplementationDeferred, handoff.Status);
		Assert.False(handoff.CanStartExecutableImplementation);
		Assert.False(handoff.CanStartRuntimeComparison);
		Assert.False(handoff.CanReadValues);
		Assert.False(handoff.CanCompareValues);
		Assert.False(handoff.CanMaterializeOutputs);
		Assert.False(handoff.CanEmitResults);
		Assert.False(handoff.CanClaimVerifiedParity);
		Assert.Contains("executable implementation remains deferred", handoff.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(handoff.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RuntimeComparison
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedRuntimeComparisonMissing
			&& row.Notes.Contains("final objective evidence gate", StringComparison.Ordinal));
		Assert.Contains(handoff.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedExecutableImplementationDeferred
			&& row.CurrentEvidence.Contains("canWriteExecutableExecutor=False", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAudit ConsistencyAudit() =>
		new(
			FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.ConsistentBlockedReadiness,
			[
				ConsistencyRow(1, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.MaterializationBlocker),
				ConsistencyRow(2, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ResultEmissionGate),
				ConsistencyRow(3, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ResultEmissionBlocker),
				ConsistencyRow(4, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.EvidenceSummary),
				ConsistencyRow(5, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ExecutorEvidenceBridge),
				ConsistencyRow(6, FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.RuntimeComparisonAndLiveDispatch),
			],
			HasMaterializationBlockerReport: true,
			HasResultEmissionGate: true,
			HasResultEmissionBlockerReport: true,
			HasEvidenceSummary: true,
			HasExecutorEvidenceBridge: true,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanWriteExecutableExecutor: false,
			CanRunRuntimeComparison: false,
			CanEnableLiveDispatch: false,
			CanClaimVerifiedParity: false,
			"Executor consistency audit remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedValueExecutorConsistencyAuditRow ConsistencyRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement requirement) =>
		new(
			order,
			requirement,
			requirement == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.RuntimeComparisonAndLiveDispatch
				? FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedRuntimeComparisonMissing
				: FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.ConsistentBlocked,
			HasProvider: true,
			BlocksMaterialization: true,
			BlocksResultEmission: true,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksLiveDispatch: true,
			BlocksVerifiedParity: true,
			"test provider",
			"test required evidence",
			"test current evidence",
			"test notes");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAudit Audit(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditStatus status) =>
		new(
			status,
			[
				AuditRow(1, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.RowIdentityPairing),
				AuditRow(2, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.JavaTypedValueRead),
				AuditRow(3, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.CSharpTypedValueRead),
				AuditRow(4, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison),
				AuditRow(5, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection),
				AuditRow(6, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment),
				AuditRow(7, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultEmission),
			],
			HasImplementationPlan: true,
			HasEvidenceSummary: true,
			HasAnyRuntimeEvidence: false,
			CanWriteExecutableExecutor: false,
			CanExecuteExecutor: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			"Implementation readiness audit remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditRow AuditRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep step) =>
		new(
			order,
			step,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedRuntimeRowsMissing,
			HasImplementationPlanStep: true,
			HasEvidenceSummary: true,
			RequiresRuntimeEvidence: step is not FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment,
			BlocksExecutableCode: true,
			CanWriteExecutableCode: false,
			CanExecute: false,
			"test required evidence",
			"test current evidence",
			"test notes");
}
