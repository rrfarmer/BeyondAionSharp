using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupWorldBroadcastFanoutReadinessServiceTests
{
	[Fact]
	public void CreateReport_KeepsLiveWorldBroadcastFanoutBlocked()
	{
		var report = FindGroupWorldBroadcastFanoutReadinessService.CreateReport();

		Assert.Equal(
			FindGroupWorldBroadcastFanoutReadinessStatus.BlockedPendingLiveBoundaryFanoutEvidence,
			report.Status);
		Assert.False(report.IsReadyForLiveWorldBroadcastFanout);
		Assert.Contains("PacketSendUtility.java", report.JavaPacketSendUtilitySource, StringComparison.Ordinal);
		Assert.Contains("FindGroupService.java", report.JavaFindGroupSource, StringComparison.Ordinal);
		Assert.Contains("BroadcastToWorldAsync", report.CSharpRegistrySource, StringComparison.Ordinal);
		Assert.Contains("FindGroupSideEffectDispatchExecutorService", report.CSharpExecutorSource, StringComparison.Ordinal);
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupWorldBroadcastFanoutEvidenceKind.CSharpLiveBoundaryWiring
				&& evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.Blocked
				&& evidence.Detail.Contains("still defers CmFindGroup", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_SeparatesJavaRaceFilterAndCSharpExecutorEvidenceFromLiveProof()
	{
		var report = FindGroupWorldBroadcastFanoutReadinessService.CreateReport();

		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupWorldBroadcastFanoutEvidenceKind.JavaWorldIteration
				&& evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("World.forEachPlayer", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupWorldBroadcastFanoutEvidenceKind.JavaFindGroupRaceFilter
				&& evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("recruitment.getRace", StringComparison.Ordinal)
				&& evidence.Detail.Contains("application.getPlayer().getRace", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupWorldBroadcastFanoutEvidenceKind.CSharpRegistryRaceFilter
				&& evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("filter accepts the player", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupWorldBroadcastFanoutEvidenceKind.CSharpOptInExecutorOrder
				&& evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("explicitly invoked", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupWorldBroadcastFanoutEvidenceKind.CSharpDisabledBoundaryActionOneFanoutTrace
				&& evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("disabled CM_FIND_GROUP action 1 removed-branch boundary acceptance", StringComparison.Ordinal)
				&& evidence.Detail.Contains("opposite-race exclusion", StringComparison.Ordinal)
				&& evidence.Detail.Contains("missing-branch no-send status evidence", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupWorldBroadcastFanoutEvidenceKind.CSharpDisabledBoundaryActionFiveFanoutTrace
				&& evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("disabled CM_FIND_GROUP action 5 removed-branch boundary acceptance", StringComparison.Ordinal)
				&& evidence.Detail.Contains("same-race recipients", StringComparison.Ordinal)
				&& evidence.Detail.Contains("missing-branch no-send status evidence", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupWorldBroadcastFanoutEvidenceKind.CSharpLiveBoundaryTraceContract
				&& evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("FindGroupWorldBroadcastLiveBoundaryTraceContractService", StringComparison.Ordinal)
				&& evidence.Detail.Contains("without wiring ProcessPacketAsync", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_RecordsNextRequiredEvidenceForActionsOneAndFive()
	{
		var report = FindGroupWorldBroadcastFanoutReadinessService.CreateReport();

		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupWorldBroadcastFanoutEvidenceKind.LiveRuntimeComparison
				&& evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.Blocked
				&& evidence.Detail.Contains("actions 1 and 5", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("opt-in executor race-filter evidence alone", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("same-race recipients", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("missing-branch no-send outcomes", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("action 12 invite dispatch as separate gates", StringComparison.Ordinal));
	}
}
