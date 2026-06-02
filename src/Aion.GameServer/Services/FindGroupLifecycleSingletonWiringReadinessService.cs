namespace Aion.GameServer.Services;

public static class FindGroupLifecycleSingletonWiringReadinessService
{
	public static FindGroupLifecycleSingletonWiringReadinessReport CreateReport()
	{
		// Java parity: FindGroupService is a SingletonHolder-backed singleton. The same instance is
		// used by CM_FIND_GROUP, PlayerLeaveWorldService, PlayerGroupService, and PlayerAllianceService.
		var callSites = new[]
		{
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.CmFindGroupBoundary,
				"CM_FIND_GROUP.runImpl dispatches every represented action through FindGroupService.getInstance().",
				"GameServerConnection case CmFindGroup remains deferred; non-live adapter evidence exists but is not wired to a shared singleton.",
				FindGroupLifecycleSingletonCallSiteStatus.BlockedMissingLiveBoundaryWiring,
				RequiresSharedSingleton: true),
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.LogoutCleanup,
				"PlayerLeaveWorldService.leaveWorld calls FindGroupService.getInstance().onLogout(player) before ResponseRequester.denyAll().",
				"PlayerEnterWorldService.LeaveWorldAsync uses the injected shared FindGroupRecruitmentPlanService before question denial without requiring an observer; live CM_FIND_GROUP wiring remains blocked.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialProductionSingletonGraph,
				RequiresSharedSingleton: true),
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.GroupJoin,
				"PlayerGroupService.addPlayerToGroup calls FindGroupService.getInstance().onJoinedTeam(invited) after group membership mutation.",
				"Production DI registers one shared FindGroupRecruitmentPlanService, FindGroupJoinedTeamLifecycleRecorder, PlayerGroupRuntime, and PlayerGroupInviteRequestService; CM_FIND_GROUP wiring remains blocked.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialProductionSingletonGraph,
				RequiresSharedSingleton: true),
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.AllianceJoin,
				"PlayerAllianceService.addPlayerToAlliance calls FindGroupService.getInstance().onJoinedTeam(invited) after alliance membership mutation.",
				"Production DI registers one shared FindGroupRecruitmentPlanService, FindGroupJoinedTeamLifecycleRecorder, PlayerAllianceRuntime, and PlayerAllianceInviteRequestService; CM_FIND_GROUP wiring remains blocked.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialProductionSingletonGraph,
				RequiresSharedSingleton: true),
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.GroupDisbandRecruitmentRemoval,
				"PlayerGroupService.disband calls FindGroupService.getInstance().removeRecruitment(group) before removing the group.",
				"Production DI registers PlayerGroupRuntime with the shared FindGroupRecruitmentPlanService, so disband planning can remove team-keyed recruitment from the shared state; live packet fanout remains disabled.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialProductionSingletonGraph,
				RequiresSharedSingleton: true),
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.AllianceDisbandRecruitmentRemoval,
				"PlayerAllianceService.disband calls FindGroupService.getInstance().removeRecruitment(alliance) before alliance disband events.",
				"Production DI registers PlayerAllianceRuntime with the shared FindGroupRecruitmentPlanService, so disband planning can remove alliance-keyed recruitment from the shared state; live packet fanout remains disabled.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialProductionSingletonGraph,
				RequiresSharedSingleton: true),
		};

		return new FindGroupLifecycleSingletonWiringReadinessReport(
			FindGroupLifecycleSingletonWiringReadinessStatus.BlockedPendingSingletonWiring,
			callSites,
			[
				"Route GameServerConnection CM_FIND_GROUP planning through the shared FindGroupRecruitmentPlanService before live dispatch is enabled.",
				"Remove remaining fallback-only boundary gaps before claiming Java singleton lifetime parity.",
				"Prove live packet order, race fanout, and connection-registry behavior before enabling CM_FIND_GROUP dispatch.",
			],
			"Java sources reviewed: FindGroupService.SingletonHolder, CM_FIND_GROUP.runImpl, PlayerLeaveWorldService.leaveWorld, PlayerGroupService.addPlayerToGroup/disband, PlayerAllianceService.addPlayerToAlliance/disband.");
	}
}

public enum FindGroupLifecycleSingletonWiringReadinessStatus
{
	BlockedPendingSingletonWiring,
	Ready,
}

public enum FindGroupLifecycleSingletonCallSite
{
	CmFindGroupBoundary,
	LogoutCleanup,
	GroupJoin,
	AllianceJoin,
	GroupDisbandRecruitmentRemoval,
	AllianceDisbandRecruitmentRemoval,
}

public enum FindGroupLifecycleSingletonCallSiteStatus
{
	PartialObserverOnly,
	PartialNonLivePlanOnly,
	PartialInjectedConnectionWiring,
	PartialProductionSingletonGraph,
	BlockedMissingLiveBoundaryWiring,
	BlockedMissingLifecycleHook,
	Ready,
}

public sealed record FindGroupLifecycleSingletonWiringReadinessReport(
	FindGroupLifecycleSingletonWiringReadinessStatus Status,
	IReadOnlyList<FindGroupLifecycleSingletonCallSiteReadiness> CallSites,
	IReadOnlyList<string> Blockers,
	string JavaSource)
{
	public bool IsReadyForLiveSingletonWiring =>
		Status == FindGroupLifecycleSingletonWiringReadinessStatus.Ready
		&& CallSites.All(callSite => callSite.Status == FindGroupLifecycleSingletonCallSiteStatus.Ready);
}

public sealed record FindGroupLifecycleSingletonCallSiteReadiness(
	FindGroupLifecycleSingletonCallSite CallSite,
	string JavaSource,
	string CSharpEvidence,
	FindGroupLifecycleSingletonCallSiteStatus Status,
	bool RequiresSharedSingleton);
