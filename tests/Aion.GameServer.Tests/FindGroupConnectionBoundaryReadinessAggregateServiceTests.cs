using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupConnectionBoundaryReadinessAggregateServiceTests
{
	[Fact]
	public void CreateReport_KeepsCmFindGroupBoundaryBlockedAndNonLive()
	{
		var report = FindGroupConnectionBoundaryReadinessAggregateService.CreateReport();

		Assert.Equal(FindGroupConnectionBoundaryReadinessStatus.BlockedPendingBoundaryWiring, report.Status);
		Assert.False(report.IsReadyForLiveDispatch);
		Assert.Contains("CmFindGroup", report.CSharpBoundary, StringComparison.Ordinal);
		Assert.Contains("CM_FIND_GROUP.runImpl", report.JavaBoundary, StringComparison.Ordinal);
		Assert.Equal(FindGroupLiveDispatchReadinessStatus.BlockedPendingLiveDispatchReview, report.LiveDispatchReadiness.Status);
		Assert.Equal(FindGroupLifecycleSingletonWiringReadinessStatus.BlockedPendingSingletonWiring, report.LifecycleSingletonReadiness.Status);
		Assert.Contains(
			report.Components,
			component => component.Name == "Connection boundary"
				&& component.Status == FindGroupConnectionBoundaryComponentStatus.Blocked
				&& component.Evidence.Contains("still defers CmFindGroup", StringComparison.Ordinal));
		Assert.Contains(
			report.Components,
			component => component.Name == "Connection adapter consumer"
				&& component.Status == FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable
				&& component.Evidence.Contains("without live sends", StringComparison.Ordinal)
				&& component.CSharpSource.Contains("CreateDisabledFindGroupBoundaryPlan", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_AggregatesDisabledPlannerExecutorAuditAndLifecycleEvidence()
	{
		var report = FindGroupConnectionBoundaryReadinessAggregateService.CreateReport();

		Assert.Contains(
			report.Components,
			component => component.Name == "Client action planner"
				&& component.Status == FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable
				&& component.CSharpSource.Contains("FindGroupConnectionClientActionCompositionPlanService", StringComparison.Ordinal));
		Assert.Contains(
			report.Components,
			component => component.Name == "Action 12 invite executor"
				&& component.Status == FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable
				&& component.JavaSource.Contains("sendInstanceApplicationResult", StringComparison.Ordinal));
		Assert.Contains(
			report.Components,
			component => component.Name == "Instance application direct packet executor"
				&& component.Status == FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable
				&& component.JavaSource.Contains("sendInstanceApplication", StringComparison.Ordinal));
		Assert.Contains(
			report.Components,
			component => component.Name == "Side-effect dispatch audit and executor"
				&& component.Status == FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable
				&& component.Evidence.Contains("sendPacket", StringComparison.Ordinal)
				&& component.Evidence.Contains("broadcastToWorld", StringComparison.Ordinal)
				&& component.Evidence.Contains("direct-before-broadcast execution order", StringComparison.Ordinal)
				&& component.Evidence.Contains("parsed CmFindGroup plans", StringComparison.Ordinal)
				&& component.CSharpSource.Contains("FindGroupSideEffectDispatchExecutorService", StringComparison.Ordinal));
		Assert.Contains(
			report.Components,
			component => component.Name == "Non-live dispatch adapter"
				&& component.Status == FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable
				&& component.Evidence.Contains("missing-runtime status", StringComparison.Ordinal)
				&& component.CSharpSource.Contains("FindGroupConnectionBoundaryDispatchAdapterService", StringComparison.Ordinal));
		Assert.Contains(
			report.Components,
			component => component.Name == "Lifecycle observers"
				&& component.Status == FindGroupConnectionBoundaryComponentStatus.PartialEvidence
				&& component.Evidence.Contains("ConcurrentDictionary state stores matching Java ConcurrentHashMap", StringComparison.Ordinal)
				&& component.CSharpSource.Contains("FindGroupRecruitmentPlanService", StringComparison.Ordinal)
				&& component.JavaSource.Contains("onLogout/onJoinedTeam", StringComparison.Ordinal));
		Assert.Contains(
			report.Components,
			component => component.Name == "Lifecycle singleton wiring"
				&& component.Status == FindGroupConnectionBoundaryComponentStatus.PartialEvidence
				&& component.Evidence.Contains("group/alliance disband recruitment removal", StringComparison.Ordinal)
				&& component.CSharpSource.Contains("FindGroupLifecycleSingletonWiringReadinessService", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_CarriesLiveDispatchBlockersAndNextRequirements()
	{
		var report = FindGroupConnectionBoundaryReadinessAggregateService.CreateReport();

		Assert.Contains(report.LiveDispatchReadiness.GlobalBlockers, blocker => blocker.Contains("Direct PacketSendUtility.sendPacket", StringComparison.Ordinal));
		Assert.Contains(report.LiveDispatchReadiness.GlobalBlockers, blocker => blocker.Contains("broadcastToWorld", StringComparison.Ordinal));
		Assert.Contains(report.LiveDispatchReadiness.GlobalBlockers, blocker => blocker.Contains("declined action 12 direct packet dispatch", StringComparison.Ordinal));
		Assert.Contains(report.LiveDispatchReadiness.GlobalBlockers, blocker => blocker.Contains("Action 12 group/alliance invite request dispatch", StringComparison.Ordinal));
		Assert.Contains(report.NextLiveDispatchRequirements, requirement => requirement.Contains("Do not enable live CmFindGroup dispatch", StringComparison.Ordinal));
		Assert.Contains(report.NextLiveDispatchRequirements, requirement => requirement.Contains("packet order", StringComparison.Ordinal));
		Assert.Contains(report.NextLiveDispatchRequirements, requirement => requirement.Contains("Actions 20 and 25", StringComparison.Ordinal));
	}
}
