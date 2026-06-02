using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupDirectPacketBoundaryTraceReadinessServiceTests
{
	[Fact]
	public void CreateReport_KeepsLiveBoundaryTraceBlocked()
	{
		var report = FindGroupDirectPacketBoundaryTraceReadinessService.CreateReport();

		Assert.Equal(
			FindGroupDirectPacketBoundaryTraceReadinessStatus.BlockedPendingLiveProcessPacketTrace,
			report.Status);
		Assert.False(report.IsReadyForLiveDirectPacketBoundary);
		Assert.Contains("CM_FIND_GROUP.java", report.JavaFindGroupSource, StringComparison.Ordinal);
		Assert.Contains("GameServerConnection.cs", report.CSharpBoundarySource, StringComparison.Ordinal);
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.LiveProcessPacketAsyncTrace
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.Blocked
				&& evidence.Detail.Contains("still defers CmFindGroup", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_RecordsDisabledBoundaryActionZeroAndOptInTraceEvidence()
	{
		var report = FindGroupDirectPacketBoundaryTraceReadinessService.CreateReport();

		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionZeroDirectSend
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("showRecruitments(player)", StringComparison.Ordinal)
				&& evidence.Detail.Contains("triggering player", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionFourDirectSend
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("showApplications(player)", StringComparison.Ordinal)
				&& evidence.Detail.Contains("triggering player", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionEightDirectSend
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("registerInstanceGroup(player", StringComparison.Ordinal)
				&& evidence.Detail.Contains("SM_FIND_GROUP action 14", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionElevenDirectSend
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("sendInstanceApplication(player, playerOrTeamId)", StringComparison.Ordinal)
				&& evidence.Detail.Contains("directly to that recruiter", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionFifteenDirectSend
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed
				&& evidence.Detail.Contains("showInstanceGroupMembersInfo(player, playerOrTeamId)", StringComparison.Ordinal)
				&& evidence.Detail.Contains("SM_FIND_GROUP action 16", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionZeroComposition
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("CreateDisabledFindGroupBoundaryPlan", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionFourComposition
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("action 4", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionEightComposition
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("action 8", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionElevenComposition
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("action 11", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionFifteenComposition
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("action 15", StringComparison.Ordinal));
		Assert.Contains(
			report.Evidence,
			evidence => evidence.Kind == FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpOptInRegistryExecutionTrace
				&& evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable
				&& evidence.Detail.Contains("disabled CM_FIND_GROUP action 0/4/8/11/15 acceptance", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_RecordsNextRequiredLiveEvidence()
	{
		var report = FindGroupDirectPacketBoundaryTraceReadinessService.CreateReport();

		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("disabled helper plus opt-in executor trace", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("ProcessPacketAsync boundary trace", StringComparison.Ordinal));
		Assert.Contains(
			report.NextRequiredEvidence,
			item => item.Contains("reviewed composition surface", StringComparison.Ordinal));
	}
}
