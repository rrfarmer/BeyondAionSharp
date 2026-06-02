using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostValueProjectionHandoffGateServiceTests
{
	[Fact]
	public void Create_DefaultGateBlocksBeforeRowPairing()
	{
		var gate = FindGroupMutationPostValueProjectionHandoffGateService.Create();

		Assert.Equal(FindGroupMutationPostValueProjectionHandoffGateStatus.BlockedRowPairingNotReady, gate.Status);
		Assert.False(gate.IsLive);
		Assert.True(gate.HasRowPairingReadiness);
		Assert.True(gate.HasValueContract);
		Assert.True(gate.HasValueReaderReadiness);
		Assert.False(gate.HasAllActionMutationPairs);
		Assert.False(gate.HasRuntimeRowValues);
		Assert.False(gate.CanStartValueProjection);
		Assert.False(gate.CanReadValues);
		Assert.False(gate.CanCompareValues);
		Assert.False(gate.CanEmitResults);
		Assert.False(gate.CanRunRuntimeComparison);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.Equal(Enum.GetValues<FindGroupMutationPostValueProjectionHandoffGateStage>(), gate.Rows.Select(row => row.Stage));
		Assert.Contains("row pairing readiness", gate.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", gate.TraceName);
		Assert.Contains("addRecruitment/addApplication", gate.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ReadyRowPairsStillBlockWhenValueContractLacksPairedInputs()
	{
		var gate = FindGroupMutationPostValueProjectionHandoffGateService.Create(ReadyPairingReport());

		Assert.Equal(FindGroupMutationPostValueProjectionHandoffGateStatus.BlockedValueContractNotReady, gate.Status);
		Assert.True(gate.HasAllActionMutationPairs);
		Assert.False(gate.CanStartValueProjection);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostValueProjectionHandoffGateStage.RowPairingReadiness
			&& row.Status == FindGroupMutationPostValueProjectionHandoffGateStageStatus.ReadyForRuntimeInput
			&& !row.BlocksValueProjection);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostValueProjectionHandoffGateStage.ValueContract
			&& row.Status == FindGroupMutationPostValueProjectionHandoffGateStageStatus.Blocked
			&& row.Evidence.Contains("hasAllPairedInputs=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyPairingAndValueContractBlockUntilValueReaderImplementationDeferred()
	{
		var valueContract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create(PairedExecutorSkeleton());

		var gate = FindGroupMutationPostValueProjectionHandoffGateService.Create(ReadyPairingReport(), valueContract);

		Assert.Equal(FindGroupMutationPostValueProjectionHandoffGateStatus.BlockedValueReaderNotReady, gate.Status);
		Assert.True(gate.HasAllValueSourceMappings);
		Assert.False(gate.CanStartValueProjection);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostValueProjectionHandoffGateStage.ValueContract
			&& row.Status == FindGroupMutationPostValueProjectionHandoffGateStageStatus.Deferred
			&& row.Evidence.Contains("status=ReadyForFutureValueProjectionButDeferred", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostValueProjectionHandoffGateStage.ValueReaderReadiness
			&& row.Status == FindGroupMutationPostValueProjectionHandoffGateStageStatus.Blocked);
	}

	[Fact]
	public void Create_ReadyMetadataStillBlocksOnRuntimeValues()
	{
		var valueContract = FindGroupMutationPostProjectedRowComparisonValueContractService.Create(PairedExecutorSkeleton());
		var valueReaderReadiness = PairedValueReaderReadiness();

		var gate = FindGroupMutationPostValueProjectionHandoffGateService.Create(ReadyPairingReport(), valueContract, valueReaderReadiness);

		Assert.Equal(FindGroupMutationPostValueProjectionHandoffGateStatus.ReadyForRuntimeValuesProjectionBlocked, gate.Status);
		Assert.True(gate.HasAllActionMutationPairs);
		Assert.True(gate.HasAllValueSourceMappings);
		Assert.False(gate.HasRuntimeRowValues);
		Assert.False(gate.CanStartValueProjection);
		Assert.False(gate.CanReadValues);
		Assert.False(gate.CanCompareValues);
		Assert.False(gate.CanEmitResults);
		Assert.Contains("runtime row values", gate.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostValueProjectionHandoffGateStage.RuntimeValueEvidence
			&& row.Status == FindGroupMutationPostValueProjectionHandoffGateStageStatus.Blocked
			&& row.BlocksValueProjection
			&& row.Notes.Contains("does not read values", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostJavaCSharpRowPairingReadinessReport ReadyPairingReport() =>
		new(
			FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.ReadyForValueProjectionRuntimeComparisonBlocked,
			[
				PairRow(1, 2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment),
				PairRow(2, 6, FindGroupDirectPacketMutationPostTraceMutationKind.Application),
			],
			ArtifactRoot: "test-root",
			HasShapeValidJavaArtifacts: true,
			HasAcceptedCSharpBoundaryRows: true,
			HasActionTwoPair: true,
			HasActionSixPair: true,
			HasAllActionMutationPairs: true,
			CanFeedValueProjection: true,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			"ready for value projection metadata only",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostJavaCSharpRowPairingReadinessRow PairRow(
		int order,
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind) =>
		new(
			order,
			action,
			mutationKind,
			action == 2
				? "FindGroupService.addRecruitment(player, message, groupType)"
				: "FindGroupService.addApplication(player, message, groupType, classId, level)",
			$"action-{action}.json",
			FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
			HasShapeValidJavaArtifact: true,
			HasAcceptedCSharpBoundaryRow: true,
			HasActionMutationPairingIdentity: true,
			CanFeedValueProjection: true,
			FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.ReadyForValueProjection,
			$"action={action}",
			"test pair");

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeleton PairedExecutorSkeleton() =>
		new(
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonStatus.ReadyForFutureValueComparisonButDeferred,
			[
				ExecutorRow(1, 2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment),
				ExecutorRow(2, 6, FindGroupDirectPacketMutationPostTraceMutationKind.Application),
			],
			HasDryRunContract: true,
			HasResultSkeleton: true,
			HasAllPairedInputs: true,
			ShouldAttemptExecutor: true,
			CanCompareValues: false,
			"deferred",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRow ExecutorRow(
		int order,
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind) =>
		new(
			order,
			action,
			mutationKind,
			"action/mutationKind/activePlayerObjectId/mutatedEntryObjectId",
			FindGroupMutationPostProjectedRowComparisonExecutorSkeletonRowStatus.BlockedValueComparisonDeferred,
			HasAcceptedJavaRow: true,
			HasAcceptedCSharpRow: true,
			ComparesValues: false,
			"action, mutationKind, fieldName, differenceKind, javaValue, csharpValue, javaSource",
			$"action={action}",
			"paired");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary PairedValueReaderReadiness()
	{
		var design = ReadyDesignContract();
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);
		var mismatchContextPreflight = FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create(preflight);
		var dryRun = PairedDryRun();
		var skeleton = FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create(design, dryRun);
		var report = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(skeleton);
		return FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create(design, preflight, mismatchContextPreflight, skeleton, report);
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

	private static FindGroupMutationPostProjectedRowComparisonDryRunContract PairedDryRun() =>
		new(
			FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor,
			Actions: [],
			AcceptedJavaRows: [],
			AcceptedCSharpRows: [],
			PairedRowReadiness:
			[
				DryRunPair(1, 2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment),
				DryRunPair(2, 6, FindGroupDirectPacketMutationPostTraceMutationKind.Application),
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

	private static FindGroupMutationPostProjectedRowComparisonDryRunPairedRowReadiness DryRunPair(
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
			$"action={action}",
			"paired");
}
