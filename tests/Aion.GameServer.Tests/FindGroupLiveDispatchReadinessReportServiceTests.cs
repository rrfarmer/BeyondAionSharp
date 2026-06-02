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
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("without requiring an observer", StringComparison.Ordinal));
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
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("leader solo recruitment re-add before full-team recruitment removal", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("ConcurrentDictionary-backed recruitment, application, and instance-group state stores", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupConcurrentMutationOrderingReadinessService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("deterministic shared-singleton interleaving evidence", StringComparison.Ordinal));
		Assert.Contains(report.GlobalBlockers, blocker => blocker.Contains("Concurrent mutation ordering remains blocked", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupSideEffectDispatchAuditService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupSideEffectDispatchExecutorService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("records execution order", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupDirectPacketBoundaryTraceReadinessService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("action 0 disabled-boundary acceptance", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupDirectPacketTriggerOrderingReadinessService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupWorldBroadcastFanoutReadinessService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("disabled action 1 boundary fanout trace evidence", StringComparison.Ordinal));
		Assert.Contains(report.GlobalBlockers, blocker => blocker.Contains("Direct packet trigger ordering remains blocked", StringComparison.Ordinal));
		Assert.Contains(report.GlobalBlockers, blocker => blocker.Contains("World-broadcast fanout remains blocked", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupConnectionBoundarySideEffectCompositionEvidenceService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("action 0/4 show-list direct packets", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("action 2/3/6/7 recruitment/application mutation evidence", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("action 8/9/17 instance-group mutation packets", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("action 10/13 instance-group show direct packets", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("action 11/12 instance-application direct/invite intents", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("action 1/5 world-broadcast intents", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupConnectionBoundaryDispatchAdapterService", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("CreateDisabledFindGroupBoundaryPlan", StringComparison.Ordinal));
		Assert.Contains(report.ObserverEvidence, evidence => evidence.Contains("FindGroupLifecycleSingletonWiringReadinessService", StringComparison.Ordinal));
		Assert.Contains(report.GlobalBlockers, blocker => blocker.Contains("CM_FIND_GROUP is not wired", StringComparison.Ordinal));
	}
}
