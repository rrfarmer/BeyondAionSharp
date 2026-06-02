using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateServiceTests
{
	[Fact]
	public void Create_DefaultGateBlocksBeforeLiveInputHandoffReadiness()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedLiveInputHandoffNotReady, gate.Status);
		Assert.False(gate.IsLive);
		Assert.True(gate.HasLiveInputHandoff);
		Assert.True(gate.HasRuntimeEvidenceChecklist);
		Assert.False(gate.HasRuntimeEvidence);
		Assert.False(gate.CanImplementComparator);
		Assert.False(gate.CanExecuteComparator);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.False(gate.CanEnableLiveDispatch);
		Assert.Contains("handoff is not ready", gate.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", gate.TraceName);
		Assert.Contains("addRecruitment/addApplication", gate.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultGateListsGoNoGoRows()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.LiveInputHandoff,
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.RuntimeEvidenceChecklist,
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.RuntimeEvidencePresence,
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.ValueProjection,
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.ResultEmission,
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.RuntimeComparison,
				FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.LiveDispatchApproval,
			],
			gate.Rows.Select(row => row.Gate));
		Assert.All(gate.Rows, row =>
		{
			Assert.False(row.IsSatisfied);
			Assert.True(row.BlocksComparatorImplementation);
		});
	}

	[Fact]
	public void Create_RuntimeReadyHandoffStillBlocksMissingRuntimeEvidence()
	{
		var handoff = FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create(ReadyForRuntimeInputSummary());
		var checklist = FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create(handoff);

		var gate = FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateService.Create(handoff, checklist);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedRuntimeEvidenceMissing, gate.Status);
		Assert.Contains("runtime evidence is missing", gate.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.LiveInputHandoff
			&& row.IsSatisfied
			&& row.Status == FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedMissingRuntimeEvidence);
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.RuntimeEvidencePresence
			&& !row.IsSatisfied
			&& row.CurrentEvidence.Contains("hasAnyRuntimeEvidence=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_GateKeepsComparatorAndVerifiedParityDisabled()
	{
		var handoff = FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create(ReadyForRuntimeInputSummary());
		var gate = FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateService.Create(handoff);

		Assert.False(gate.CanImplementComparator);
		Assert.False(gate.CanExecuteComparator);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.ValueProjection
			&& row.Status == FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedImplementationDeferred
			&& row.RequiredEvidence.Contains("Projected Java/C# equality values", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.ResultEmission
			&& row.RequiredEvidence.Contains("Matched", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("FieldMismatch", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_LiveDispatchApprovalRemainsDisabledAndNamesBroadTrigger()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateService.Create(
			FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create(ReadyForRuntimeInputSummary()));

		Assert.False(gate.CanEnableLiveDispatch);
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonExecutionReadinessGate.LiveDispatchApproval
			&& row.Status == FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateRowStatus.BlockedLiveDispatchDisabled
			&& row.RequiredEvidence.Contains("broad-validation trigger", StringComparison.Ordinal)
			&& row.Notes.Contains("GameServerConnection.ProcessPacketAsync", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonReadinessSummary ReadyForRuntimeInputSummary() =>
		new(
			FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedValueProjectionDeferred,
			[
				new FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
					1,
					FindGroupMutationPostProjectedRowComparisonReadinessStage.DryRunContract,
					FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.ReadyForFutureInput,
					HasExpectedShape: true,
					BlocksComparison: false,
					"status=ReadyForFutureExecutor",
					"ready"),
			],
			HasDryRunContract: true,
			HasExecutorSkeleton: true,
			HasValueContract: true,
			HasBlockedResultReport: true,
			HasAllPairedInputs: true,
			CanCompareRows: false,
			CanProjectValues: false,
			CanEmitResults: false,
			"Projected-row comparison remains blocked because value projection is still deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
}
