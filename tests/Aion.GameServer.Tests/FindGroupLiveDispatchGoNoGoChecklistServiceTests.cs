using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupLiveDispatchGoNoGoChecklistServiceTests
{
	[Fact]
	public void CreateChecklist_KeepsLiveDispatchBlockedUntilEveryGateIsReady()
	{
		var checklist = FindGroupLiveDispatchGoNoGoChecklistService.CreateChecklist();

		Assert.Equal(FindGroupLiveDispatchGoNoGoStatus.Blocked, checklist.Status);
		Assert.False(checklist.IsReadyForLiveDispatch);
		Assert.True(checklist.HasAllRequiredLiveDispatchGates);
		Assert.Equal(FindGroupConnectionBoundaryReadinessStatus.BlockedPendingBoundaryWiring, checklist.BoundaryReadiness.Status);
		Assert.Contains("CM_FIND_GROUP.runImpl", checklist.JavaSource, StringComparison.Ordinal);
		Assert.Contains("Do not wire GameServerConnection.ProcessPacketAsync", checklist.LiveWiringDecision, StringComparison.Ordinal);
		Assert.Equal(
			[
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ConnectionBoundaryWiring,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.SharedSingletonLifecycle,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.WorldBroadcastDispatch,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ActionTwelveInviteDispatch,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.RuntimeComparison,
			],
			checklist.RequiredLiveDispatchGateKinds);
		Assert.Contains(
			checklist.Items,
			item => item.Kind == FindGroupLiveDispatchGoNoGoChecklistItemKind.ConnectionBoundaryWiring
				&& item.Status == FindGroupLiveDispatchGoNoGoChecklistItemStatus.Blocked
				&& item.Gate.Contains("ProcessPacketAsync", StringComparison.Ordinal)
				&& item.Evidence.Contains("live CmFindGroup dispatch deferred", StringComparison.Ordinal));
		Assert.Contains(
			checklist.Items,
			item => item.Kind == FindGroupLiveDispatchGoNoGoChecklistItemKind.RuntimeComparison
				&& item.Status == FindGroupLiveDispatchGoNoGoChecklistItemStatus.Blocked
				&& item.Evidence.Contains("FindGroupRuntimeComparisonPreflightContractService", StringComparison.Ordinal)
				&& item.NextRequiredEvidence.Contains("preflight contract", StringComparison.Ordinal));
		Assert.DoesNotContain(FindGroupLiveDispatchGoNoGoChecklistItemKind.ParsedOnlyNoRunActions, checklist.BlockingRequiredGateKinds);
	}

	[Fact]
	public void CreateChecklist_SeparatesEvidenceAvailableGatesFromReadyGates()
	{
		var checklist = FindGroupLiveDispatchGoNoGoChecklistService.CreateChecklist();

		Assert.Contains(
			checklist.Items,
			item => item.Kind == FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch
				&& item.Status == FindGroupLiveDispatchGoNoGoChecklistItemStatus.EvidenceAvailable
				&& item.Evidence.Contains("action 12 declined SM_MESSAGE payload", StringComparison.Ordinal)
				&& item.NextRequiredEvidence.Contains("live connection-registry tests", StringComparison.Ordinal));
		Assert.Contains(
			checklist.Items,
			item => item.Kind == FindGroupLiveDispatchGoNoGoChecklistItemKind.WorldBroadcastDispatch
				&& item.Status == FindGroupLiveDispatchGoNoGoChecklistItemStatus.EvidenceAvailable
				&& item.Evidence.Contains("race-filtered world-broadcast intents", StringComparison.Ordinal));
		Assert.Contains(
			checklist.Items,
			item => item.Kind == FindGroupLiveDispatchGoNoGoChecklistItemKind.ActionTwelveInviteDispatch
				&& item.Status == FindGroupLiveDispatchGoNoGoChecklistItemStatus.EvidenceAvailable
				&& item.Evidence.Contains("boundary-acceptance-before-group/alliance-invite traces", StringComparison.Ordinal)
				&& item.Evidence.Contains("missing inviter/invited failure results", StringComparison.Ordinal)
				&& item.Evidence.Contains("non-live ordered trace contract", StringComparison.Ordinal)
				&& item.NextRequiredEvidence.Contains("FindGroupActionTwelveInviteLiveBoundaryTraceContractService", StringComparison.Ordinal)
				&& item.NextRequiredEvidence.Contains("declined-whisper dispatch", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateChecklist_RequiresEveryLiveSideEffectGateBeforeProcessPacketAsyncWiring()
	{
		var checklist = FindGroupLiveDispatchGoNoGoChecklistService.CreateChecklist();

		Assert.Equal(
			[
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ConnectionBoundaryWiring,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.SharedSingletonLifecycle,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.WorldBroadcastDispatch,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ActionTwelveInviteDispatch,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.RuntimeComparison,
			],
			checklist.BlockingRequiredGateKinds);

		var blockedItems = checklist.Items
			.Where(item => checklist.BlockingRequiredGateKinds.Contains(item.Kind))
			.ToArray();

		Assert.All(
			blockedItems,
			item => Assert.NotEqual(FindGroupLiveDispatchGoNoGoChecklistItemStatus.Ready, item.Status));
		Assert.Contains(blockedItems, item => item.NextRequiredEvidence.Contains("shared FindGroupRecruitmentPlanService", StringComparison.Ordinal));
		Assert.Contains(blockedItems, item => item.NextRequiredEvidence.Contains("packet order", StringComparison.Ordinal));
		Assert.Contains(blockedItems, item => item.NextRequiredEvidence.Contains("race visibility filtering", StringComparison.Ordinal));
		Assert.Contains(blockedItems, item => item.NextRequiredEvidence.Contains("invite request mutation", StringComparison.Ordinal));
		Assert.Contains(blockedItems, item => item.NextRequiredEvidence.Contains("preflight contract", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateChecklist_MarksParsedOnlyActionsAsReadyNoOps()
	{
		var checklist = FindGroupLiveDispatchGoNoGoChecklistService.CreateChecklist();

		var parsedOnly = Assert.Single(
			checklist.Items,
			item => item.Kind == FindGroupLiveDispatchGoNoGoChecklistItemKind.ParsedOnlyNoRunActions);
		Assert.Equal(FindGroupLiveDispatchGoNoGoChecklistItemStatus.Ready, parsedOnly.Status);
		Assert.Contains("actions 20 and 25", parsedOnly.Gate, StringComparison.Ordinal);
		Assert.Contains("runImpl has no branch", parsedOnly.Evidence, StringComparison.Ordinal);
		Assert.Contains("parsed-only no-ops", parsedOnly.NextRequiredEvidence, StringComparison.Ordinal);
	}
}
