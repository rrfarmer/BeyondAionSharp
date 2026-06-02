namespace Aion.GameServer.Services;

public static class FindGroupLiveDispatchGoNoGoChecklistService
{
	public static FindGroupLiveDispatchGoNoGoChecklist CreateChecklist()
	{
		var aggregate = FindGroupConnectionBoundaryReadinessAggregateService.CreateReport();
		var items = new[]
		{
			new FindGroupLiveDispatchGoNoGoChecklistItem(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ConnectionBoundaryWiring,
				"GameServerConnection.ProcessPacketAsync case CmFindGroup",
				"Java CM_FIND_GROUP.runImpl dispatches through FindGroupService.getInstance(); C# still keeps live CmFindGroup dispatch deferred.",
				FindGroupLiveDispatchGoNoGoChecklistItemStatus.Blocked,
				"Wire the boundary only after the shared singleton, direct packet, broadcast, invite, and failure-result gates are ready."),
			new FindGroupLiveDispatchGoNoGoChecklistItem(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.SharedSingletonLifecycle,
				"FindGroupService singleton lifecycle",
				"Java uses one FindGroupService.SingletonHolder instance across CM_FIND_GROUP, logout, joined-team, and disband cleanup. C# has production singleton graph evidence for lifecycle callers, but CM_FIND_GROUP is not wired to execute that shared state.",
				aggregate.LifecycleSingletonReadiness.IsReadyForLiveSingletonWiring
					? FindGroupLiveDispatchGoNoGoChecklistItemStatus.Ready
					: FindGroupLiveDispatchGoNoGoChecklistItemStatus.Blocked,
				"Route live CM_FIND_GROUP execution through the shared FindGroupRecruitmentPlanService before claiming singleton lifetime parity."),
			new FindGroupLiveDispatchGoNoGoChecklistItem(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch,
				"PacketSendUtility.sendPacket-equivalent dispatch",
				"Disabled evidence exists for direct packet intents, action 11 application packets, action 12 declined SM_MESSAGE payload, and missing direct recipients.",
				FindGroupLiveDispatchGoNoGoChecklistItemStatus.EvidenceAvailable,
				"Add live connection-registry tests that prove packet order relative to the triggering client packet."),
			new FindGroupLiveDispatchGoNoGoChecklistItem(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.WorldBroadcastDispatch,
				"PacketSendUtility.broadcastToWorld race fanout",
				"Disabled evidence exists for race-filtered world-broadcast intents and direct-before-broadcast executor order.",
				FindGroupLiveDispatchGoNoGoChecklistItemStatus.EvidenceAvailable,
				"Add live connection-registry tests that prove race visibility filtering and broadcast order."),
			new FindGroupLiveDispatchGoNoGoChecklistItem(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ActionTwelveInviteDispatch,
				"Action 12 group/alliance invite dispatch",
				"Disabled evidence exists for accepted group/alliance invite requests, an action 12 boundary-acceptance-before-group-invite trace, missing applicant/state branches, and missing inviter/invited failure results.",
				FindGroupLiveDispatchGoNoGoChecklistItemStatus.EvidenceAvailable,
				"Prove live invite request mutation and packet/question ordering before enabling action 12 live dispatch."),
			new FindGroupLiveDispatchGoNoGoChecklistItem(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ParsedOnlyNoRunActions,
				"Parsed-only actions 20 and 25",
				"Java readImpl parses actions 20 and 25, but runImpl has no branch; disabled C# evidence preserves the no-side-effect result.",
				FindGroupLiveDispatchGoNoGoChecklistItemStatus.Ready,
				"Keep these actions as parsed-only no-ops in any live adapter."),
			new FindGroupLiveDispatchGoNoGoChecklistItem(
				FindGroupLiveDispatchGoNoGoChecklistItemKind.RuntimeComparison,
				"Runtime/socket comparison",
				"No encrypted socket or real-client comparison has verified live CM_FIND_GROUP packet order, visibility filtering, service concurrency, or client-observable behavior.",
				FindGroupLiveDispatchGoNoGoChecklistItemStatus.Blocked,
				"Add runtime or socket-level evidence before claiming live parity."),
		};

		return new FindGroupLiveDispatchGoNoGoChecklist(
			FindGroupLiveDispatchGoNoGoStatus.Blocked,
			aggregate,
			items,
			"Java sources reviewed: CM_FIND_GROUP.runImpl, FindGroupService.SingletonHolder, FindGroupService sendPacket/broadcast/invite call sites.");
	}
}

public enum FindGroupLiveDispatchGoNoGoStatus
{
	Blocked,
	Ready,
}

public enum FindGroupLiveDispatchGoNoGoChecklistItemKind
{
	ConnectionBoundaryWiring,
	SharedSingletonLifecycle,
	DirectPacketDispatch,
	WorldBroadcastDispatch,
	ActionTwelveInviteDispatch,
	ParsedOnlyNoRunActions,
	RuntimeComparison,
}

public enum FindGroupLiveDispatchGoNoGoChecklistItemStatus
{
	Blocked,
	EvidenceAvailable,
	Ready,
}

public sealed record FindGroupLiveDispatchGoNoGoChecklist(
	FindGroupLiveDispatchGoNoGoStatus Status,
	FindGroupConnectionBoundaryReadinessAggregate BoundaryReadiness,
	IReadOnlyList<FindGroupLiveDispatchGoNoGoChecklistItem> Items,
	string JavaSource)
{
	public bool IsReadyForLiveDispatch =>
		Status == FindGroupLiveDispatchGoNoGoStatus.Ready
		&& BoundaryReadiness.IsReadyForLiveDispatch
		&& Items.All(item => item.Status == FindGroupLiveDispatchGoNoGoChecklistItemStatus.Ready);
}

public sealed record FindGroupLiveDispatchGoNoGoChecklistItem(
	FindGroupLiveDispatchGoNoGoChecklistItemKind Kind,
	string Gate,
	string Evidence,
	FindGroupLiveDispatchGoNoGoChecklistItemStatus Status,
	string NextRequiredEvidence);
