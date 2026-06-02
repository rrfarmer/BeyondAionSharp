using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateServiceTests
{
	[Fact]
	public void Create_DefaultGateBlocksBeforeComparatorPreflightReadiness()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedComparatorPreflightNotReady, gate.Status);
		Assert.False(gate.IsLive);
		Assert.True(gate.HasLiveInputHandoff);
		Assert.True(gate.HasRuntimeEvidenceChecklist);
		Assert.True(gate.HasComparatorPreflight);
		Assert.False(gate.HasRuntimeEvidence);
		Assert.False(gate.CanImplementExecutor);
		Assert.False(gate.CanExecuteExecutor);
		Assert.False(gate.CanProjectValues);
		Assert.False(gate.CanCompareValues);
		Assert.False(gate.CanEmitResults);
		Assert.False(gate.CanEnableLiveDispatch);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", gate.TraceName);
		Assert.Contains("addRecruitment/addApplication", gate.JavaSource, StringComparison.Ordinal);
		Assert.Contains("comparator preflight metadata is not ready", gate.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultGateListsExecutorReadinessRowsWithoutExecution()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.LiveInputHandoff,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.RuntimeEvidenceChecklist,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.ComparatorPreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.ExecutorImplementation,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.LiveDispatchGuard,
			],
			gate.Rows.Select(row => row.Gate));
		Assert.All(gate.Rows, row =>
		{
			Assert.True(row.BlocksExecutorImplementation);
			Assert.False(row.CanImplementExecutor);
			Assert.False(row.CanExecuteExecutor);
			Assert.False(row.CanEnableLiveDispatch);
		});
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.ComparatorPreflight
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedComparatorPreflightNotReady);
	}

	[Fact]
	public void Create_ReadyComparatorStillBlocksWhenRuntimeEvidenceIsMissing()
	{
		var comparatorPreflight = ReadyComparatorPreflight();
		var liveInputHandoff = RuntimeArtifactReadyHandoff();
		var runtimeEvidenceChecklist = FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create(liveInputHandoff);

		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateService.Create(
			liveInputHandoff,
			runtimeEvidenceChecklist,
			comparatorPreflight);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedRuntimeEvidenceMissing, gate.Status);
		Assert.Contains("runtime evidence and live-input handoff evidence are missing", gate.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.ComparatorPreflight
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedExecutorImplementationDeferred
			&& row.Evidence.Contains("status=BlockedComparatorImplementationDeferred", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.RuntimeEvidenceChecklist
			&& !row.HasRuntimeEvidence
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedRuntimeEvidenceMissing);
	}

	[Fact]
	public void Create_RuntimeEvidenceFlagsStillDeferExecutorImplementation()
	{
		var comparatorPreflight = ReadyComparatorPreflight();
		var liveInputHandoff = RuntimeArtifactReadyHandoff();
		var runtimeEvidenceChecklist = RuntimeEvidencePresentChecklist();

		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateService.Create(
			liveInputHandoff,
			runtimeEvidenceChecklist,
			comparatorPreflight);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedExecutorImplementationDeferred, gate.Status);
		Assert.True(gate.HasRuntimeEvidence);
		Assert.False(gate.CanImplementExecutor);
		Assert.False(gate.CanExecuteExecutor);
		Assert.False(gate.CanEmitResults);
		Assert.Contains("implementation remains intentionally deferred", gate.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.ExecutorImplementation
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedExecutorImplementationDeferred
			&& row.RequiredNextEvidence.Contains("row pairing, typed reader reads, equality comparison", StringComparison.Ordinal)
			&& row.Notes.Contains("must not materialize", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_LiveDispatchGuardRemainsDisabled()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateService.Create(
			RuntimeArtifactReadyHandoff(),
			RuntimeEvidencePresentChecklist(),
			ReadyComparatorPreflight());

		Assert.False(gate.CanEnableLiveDispatch);
		Assert.Contains(gate.Rows, row =>
			row.Gate == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.LiveDispatchGuard
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedLiveDispatchDisabled
			&& row.RequiredNextEvidence.Contains("broad-validation trigger", StringComparison.Ordinal)
			&& row.Notes.Contains("GameServerConnection.ProcessPacketAsync", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract ReadyComparatorPreflight() =>
		new(
			FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedComparatorImplementationDeferred,
			[
				new FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageRow(
					1,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.RowIdentityPairing,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedRuntimeRowsMissing,
					EqualityFieldCount: 38,
					RuntimeContextFieldCount: 4,
					OutputKinds: [],
					RequiresAcceptedJavaRows: true,
					RequiresAcceptedCSharpRows: true,
					RequiresProjectedValues: false,
					RequiresResultSchema: true,
					CanExecute: false,
					CanProjectValues: false,
					CanCompareValues: false,
					CanEmitResults: false,
					"ready comparator stage",
					"evidence",
					"notes"),
			],
			EqualityFieldCount: 38,
			RuntimeContextFieldCount: 4,
			HasImplementationRunbook: true,
			HasResultSchema: true,
			CanExecuteComparator: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanAttachRuntimeContext: false,
			CanEmitResults: false,
			"Comparator implementation remains deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract RuntimeArtifactReadyHandoff() =>
		new(
			FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedMissingRuntimeArtifacts,
			[
				new FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow(
					1,
					FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.LiveDispatchGuard,
					FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedLiveDispatchDisabled,
					IsRuntimeEvidence: false,
					BlocksLiveComparison: true,
					"live dispatch guard",
					"canEnableLiveDispatch=false",
					"live dispatch remains disabled"),
			],
			HasReadinessSummary: true,
			HasNonLiveMetadata: true,
			HasRequiredRuntimeEvidence: false,
			CanStartLiveComparison: false,
			CanEnableLiveDispatch: false,
			"Runtime artifacts missing.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist RuntimeEvidencePresentChecklist() =>
		new(
			FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedRuntimeEvidenceMissing,
			[
				new FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistRow(
					1,
					FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact,
					FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.FutureRuntimeEvidenceRequired,
					HasExistingProvider: true,
					HasRuntimeEvidence: true,
					BlocksVerifiedParity: true,
					"test runtime evidence provider",
					"executor implementation still deferred",
					"hasAnyRuntimeEvidence=True",
					"test row"),
			],
			HasLiveInputHandoff: true,
			HasExistingNonLiveProviders: true,
			HasAnyRuntimeEvidence: true,
			CanStartProjectedComparison: false,
			CanClaimVerifiedParity: false,
			"Runtime evidence flag present, but executor implementation remains deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
}
