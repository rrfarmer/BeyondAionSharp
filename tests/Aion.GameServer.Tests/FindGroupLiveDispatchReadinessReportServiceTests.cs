using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupLiveDispatchReadinessReportServiceTests
{
	[Fact]
	public void CreateReport_EnumeratesJavaRunImplActionsAndKeepsLiveDispatchBlocked()
	{
		var report = FindGroupLiveDispatchReadinessReportService.CreateReport();

		Assert.Equal(FindGroupLiveDispatchReadinessStatus.BlockedPendingLiveDispatchReview, report.Status);
		Assert.False(report.IsReadyForLiveDispatch);
		Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 15, 17], report.JavaRunImplActions);
		Assert.Equal([20, 25], report.ParsedButNoRunImplActions);
		Assert.Equal(18, report.Actions.Count);
		Assert.All(
			report.Actions.Where(action => action.HasJavaRunImplBranch),
			action =>
			{
				Assert.Equal(FindGroupClientActionDispatchReadiness.DeferredUntilRuntimeFactsAreAvailable, action.Readiness);
				Assert.Contains(FindGroupClientActionRuntimeRequirement.ActivePlayer, action.Requirements);
				Assert.Contains(FindGroupClientActionRuntimeRequirement.FindGroupStateStore, action.Requirements);
			});
		Assert.Contains(report.GlobalBlockers, blocker => blocker.Contains("GameServerConnection still defers CmFindGroup", StringComparison.Ordinal));
		Assert.Contains(report.GlobalBlockers, blocker => blocker.Contains("real-client runtime comparison", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("onLogout cleanup", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("onJoinedTeam plans", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(20)]
	[InlineData(25)]
	public void CreateReport_PreservesParsedButNoRunImplActionsAsNonDispatching(int action)
	{
		var report = FindGroupLiveDispatchReadinessReportService.CreateReport();

		var actionReport = Assert.Single(report.Actions, entry => entry.Action == action);
		Assert.False(actionReport.HasJavaRunImplBranch);
		Assert.Equal(FindGroupClientActionDispatchReadiness.ParsedButNoJavaRunImpl, actionReport.Readiness);
		Assert.Empty(actionReport.Requirements);
		Assert.Equal("CM_FIND_GROUP.readImpl parses this action, but runImpl has no branch.", actionReport.JavaSource);
	}

	[Fact]
	public void CreateReport_ActionTwelveKeepsInviteSideEffectGateExplicit()
	{
		var report = FindGroupLiveDispatchReadinessReportService.CreateReport();

		var actionTwelve = Assert.Single(report.Actions, entry => entry.Action == 12);
		Assert.True(actionTwelve.HasJavaRunImplBranch);
		Assert.Contains(FindGroupClientActionRuntimeRequirement.WorldPlayerLookup, actionTwelve.Requirements);
		Assert.Contains(FindGroupClientActionRuntimeRequirement.GroupAllianceInviteDispatch, actionTwelve.Requirements);
		Assert.Contains(report.GlobalBlockers, blocker => blocker.Contains("declined action 12 direct packet dispatch", StringComparison.Ordinal));
		Assert.Contains(report.GlobalBlockers, blocker => blocker.Contains("Action 12 group/alliance invite request dispatch", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupInstanceApplicationDirectDispatchPlanService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupInstanceApplicationInviteDispatchPlanService", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateReport_RecordsLifecycleObserverEvidenceWithoutMarkingLiveDispatchReady()
	{
		var report = FindGroupLiveDispatchReadinessReportService.CreateReport();

		Assert.False(report.IsReadyForLiveDispatch);
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("LeaveWorldAsync", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("PlayerGroupInviteRequestService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("current-team size reaches minMembers", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupSideEffectDispatchAuditService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupSideEffectDispatchExecutorService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupConnectionBoundarySideEffectCompositionEvidenceService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("action 1 and action 5 world-broadcast intents", StringComparison.Ordinal));
		Assert.Contains(report.GlobalBlockers, blocker => blocker.Contains("not wired as a live singleton", StringComparison.Ordinal));
	}
}
