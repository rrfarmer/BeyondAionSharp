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
				"PlayerEnterWorldService.LeaveWorldAsync can record disabled cleanup before question denial when an observer is supplied, but normal DI does not yet prove the same FindGroupRecruitmentPlanService singleton is shared by all callers.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialObserverOnly,
				RequiresSharedSingleton: true),
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.GroupJoin,
				"PlayerGroupService.addPlayerToGroup calls FindGroupService.getInstance().onJoinedTeam(invited) after group membership mutation.",
				"GameServerConnection can consume an injected PlayerGroupInviteRequestService with a shared FindGroupJoinedTeamLifecycleRecorder; production DI singleton registration and CM_FIND_GROUP wiring remain blocked.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialInjectedConnectionWiring,
				RequiresSharedSingleton: true),
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.AllianceJoin,
				"PlayerAllianceService.addPlayerToAlliance calls FindGroupService.getInstance().onJoinedTeam(invited) after alliance membership mutation.",
				"GameServerConnection can consume an injected PlayerAllianceInviteRequestService with a shared FindGroupJoinedTeamLifecycleRecorder; production DI singleton registration and CM_FIND_GROUP wiring remain blocked.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialInjectedConnectionWiring,
				RequiresSharedSingleton: true),
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.GroupDisbandRecruitmentRemoval,
				"PlayerGroupService.disband calls FindGroupService.getInstance().removeRecruitment(group) before removing the group.",
				"PlayerGroupRuntime leave/disband planning can expose a non-live FindGroupRecruitmentPlanService.RemoveRecruitment(teamId) plan when a find-group service is supplied, but normal live singleton wiring is not proven.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialNonLivePlanOnly,
				RequiresSharedSingleton: true),
			new FindGroupLifecycleSingletonCallSiteReadiness(
				FindGroupLifecycleSingletonCallSite.AllianceDisbandRecruitmentRemoval,
				"PlayerAllianceService.disband calls FindGroupService.getInstance().removeRecruitment(alliance) before alliance disband events.",
				"PlayerAllianceRuntime leave/disband planning can expose a non-live FindGroupRecruitmentPlanService.RemoveRecruitment(allianceId) plan when a find-group service is supplied, but normal live singleton wiring is not proven.",
				FindGroupLifecycleSingletonCallSiteStatus.PartialNonLivePlanOnly,
				RequiresSharedSingleton: true),
		};

		return new FindGroupLifecycleSingletonWiringReadinessReport(
			FindGroupLifecycleSingletonWiringReadinessStatus.BlockedPendingSingletonWiring,
			callSites,
			[
				"Register one shared FindGroupRecruitmentPlanService singleton before live CM_FIND_GROUP dispatch or lifecycle cleanup is enabled.",
				"Route GameServerConnection CM_FIND_GROUP planning, logout cleanup, group joined-team cleanup, alliance joined-team cleanup, and team disband recruitment removal through that same singleton.",
				"Complete production DI singleton registration and remove remaining observer-only/fallback-only lifecycle gaps before claiming Java singleton lifetime parity.",
				"Prove Java order for logout before question denial, joined-team cleanup after membership mutation, and disband recruitment removal before team removal.",
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
