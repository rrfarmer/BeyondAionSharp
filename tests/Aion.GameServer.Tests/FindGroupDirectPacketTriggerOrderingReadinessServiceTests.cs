using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupDirectPacketTriggerOrderingReadinessServiceTests
{
	[Fact]
	public void CreateReport_KeepsLiveDirectPacketOrderingBlocked()
	{
		var report = FindGroupDirectPacketTriggerOrderingReadinessService.CreateReport();

		Assert.Equal(
			FindGroupDirectPacketTriggerOrderingReadinessStatus.BlockedPendingLiveBoundaryOrderingEvidence,
			report.Status);
		Assert.False(report.IsReadyForLiveDirectPacketOrdering);
		Assert.Contains("AionClientPacket.java", report.JavaClientPacketRunSource, StringComparison.Ordinal);
		Assert.Contains("CM_FIND_GROUP.java", report.JavaFindGroupRunImplSource, StringComparison.Ordinal);
		Assert.Contains("ProcessPacketAsync", report.CSharpBoundarySource, StringComparison.Ordinal);
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketTriggerOrderingEvidenceKind.CSharpTriggerBoundaryWiring
				&& evidence.Status == FindGroupDirectPacketTriggerOrderingEvidenceStatus.Blocked
				&& evidence.Detail.Contains("still defers CmFindGroup", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_SeparatesJavaAndOptInExecutorEvidenceFromLiveBoundaryProof()
	{
		var report = FindGroupDirectPacketTriggerOrderingReadinessService.CreateReport();

		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketTriggerOrderingEvidenceKind.JavaTriggerBeforeRunImpl
				&& evidence.Status == FindGroupDirectPacketTriggerOrderingEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("invokes CM_FIND_GROUP.runImpl synchronously", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketTriggerOrderingEvidenceKind.JavaSequentialSendPacketCalls
				&& evidence.Status == FindGroupDirectPacketTriggerOrderingEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("branch order", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketTriggerOrderingEvidenceKind.CSharpOptInExecutorOrder
				&& evidence.Status == FindGroupDirectPacketTriggerOrderingEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("controlled evidence tests", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketTriggerOrderingEvidenceKind.CSharpDisabledBoundaryActionZeroFourEightElevenFifteenTrace
				&& evidence.Status == FindGroupDirectPacketTriggerOrderingEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("disabled CM_FIND_GROUP action 0/4/8/11/15 boundary acceptance", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketTriggerOrderingEvidenceKind.LiveSocketOrderingComparison
				&& evidence.Status == FindGroupDirectPacketTriggerOrderingEvidenceStatus.Blocked
				&& evidence.Detail.Contains("after the triggering client packet", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_RecordsNextRequiredEvidenceBeforeLiveDispatch()
	{
		var report = FindGroupDirectPacketTriggerOrderingReadinessService.CreateReport();

		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("opt-in executor ordering alone", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("one ordered trace", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("action 20 and 25 parsed-only no-op", StringComparison.Ordinal));
	}
}
