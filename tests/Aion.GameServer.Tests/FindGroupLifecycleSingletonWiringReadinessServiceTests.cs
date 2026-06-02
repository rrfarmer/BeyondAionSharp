using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupLifecycleSingletonWiringReadinessServiceTests
{
	[Fact]
	public void CreateReport_KeepsLiveSingletonWiringBlocked()
	{
		var report = FindGroupLifecycleSingletonWiringReadinessService.CreateReport();

		Assert.Equal(FindGroupLifecycleSingletonWiringReadinessStatus.BlockedPendingSingletonWiring, report.Status);
		Assert.False(report.IsReadyForLiveSingletonWiring);
		Assert.Contains("FindGroupService.SingletonHolder", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains(report.Blockers, blocker => blocker.Contains("one shared FindGroupRecruitmentPlanService singleton", StringComparison.Ordinal));
		Assert.Contains(report.Blockers, blocker => blocker.Contains("Replace current observer-only constructors/new service fallbacks", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_EnumeratesJavaSingletonLifecycleCallSites()
	{
		var report = FindGroupLifecycleSingletonWiringReadinessService.CreateReport();

		Assert.Equal(
			[
				FindGroupLifecycleSingletonCallSite.CmFindGroupBoundary,
				FindGroupLifecycleSingletonCallSite.LogoutCleanup,
				FindGroupLifecycleSingletonCallSite.GroupJoin,
				FindGroupLifecycleSingletonCallSite.AllianceJoin,
				FindGroupLifecycleSingletonCallSite.GroupDisbandRecruitmentRemoval,
				FindGroupLifecycleSingletonCallSite.AllianceDisbandRecruitmentRemoval,
			],
			report.CallSites.Select(callSite => callSite.CallSite).ToArray());
		Assert.All(report.CallSites, callSite => Assert.True(callSite.RequiresSharedSingleton));
		Assert.Contains(report.CallSites, callSite => callSite.JavaSource.Contains("CM_FIND_GROUP.runImpl", StringComparison.Ordinal));
		Assert.Contains(report.CallSites, callSite => callSite.JavaSource.Contains("PlayerLeaveWorldService.leaveWorld", StringComparison.Ordinal));
		Assert.Contains(report.CallSites, callSite => callSite.JavaSource.Contains("PlayerGroupService.addPlayerToGroup", StringComparison.Ordinal));
		Assert.Contains(report.CallSites, callSite => callSite.JavaSource.Contains("PlayerAllianceService.addPlayerToAlliance", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_RecordsObserverOnlyAndNonLivePlanGaps()
	{
		var report = FindGroupLifecycleSingletonWiringReadinessService.CreateReport();

		Assert.Contains(
			report.CallSites,
			callSite => callSite.CallSite == FindGroupLifecycleSingletonCallSite.LogoutCleanup
				&& callSite.Status == FindGroupLifecycleSingletonCallSiteStatus.PartialObserverOnly
				&& callSite.CSharpEvidence.Contains("normal DI does not yet prove the same", StringComparison.Ordinal));
		Assert.Contains(
			report.CallSites,
			callSite => callSite.CallSite == FindGroupLifecycleSingletonCallSite.GroupJoin
				&& callSite.Status == FindGroupLifecycleSingletonCallSiteStatus.PartialObserverOnly
				&& callSite.CSharpEvidence.Contains("constructs the service without a recorder", StringComparison.Ordinal));
		Assert.Contains(
			report.CallSites,
			callSite => callSite.CallSite == FindGroupLifecycleSingletonCallSite.GroupDisbandRecruitmentRemoval
				&& callSite.Status == FindGroupLifecycleSingletonCallSiteStatus.PartialNonLivePlanOnly
				&& callSite.CSharpEvidence.Contains("RemoveRecruitment(teamId)", StringComparison.Ordinal));
		Assert.Contains(
			report.CallSites,
			callSite => callSite.CallSite == FindGroupLifecycleSingletonCallSite.AllianceDisbandRecruitmentRemoval
				&& callSite.Status == FindGroupLifecycleSingletonCallSiteStatus.PartialNonLivePlanOnly
				&& callSite.CSharpEvidence.Contains("RemoveRecruitment(allianceId)", StringComparison.Ordinal));
	}
}
