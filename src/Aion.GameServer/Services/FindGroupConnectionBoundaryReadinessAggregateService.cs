namespace Aion.GameServer.Services;

public static class FindGroupConnectionBoundaryReadinessAggregateService
{
	public static FindGroupConnectionBoundaryReadinessAggregate CreateReport()
	{
		var liveDispatchReadiness = FindGroupLiveDispatchReadinessReportService.CreateReport();

		return new FindGroupConnectionBoundaryReadinessAggregate(
			FindGroupConnectionBoundaryReadinessStatus.BlockedPendingBoundaryWiring,
			"GameServerConnection.ProcessPacketAsync case CmFindGroup",
			"network/aion/clientpackets/CM_FIND_GROUP.runImpl -> services/findgroup/FindGroupService",
			liveDispatchReadiness,
			[
				new FindGroupConnectionBoundaryComponentReadiness(
					"Connection boundary",
					"GameServerConnection still defers CmFindGroup and does not invoke FindGroupService-equivalent actions.",
					FindGroupConnectionBoundaryComponentStatus.Blocked,
					"dotnetConversion/src/Aion.GameServer/Network/Aion/GameServerConnection.cs case CmFindGroup",
					"network/aion/clientpackets/CM_FIND_GROUP.runImpl"),
				new FindGroupConnectionBoundaryComponentReadiness(
					"Client action planner",
					"FindGroupConnectionClientActionCompositionPlanService can compose disabled action plans from active-player, team, world, auto-group, and config facts.",
					FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable,
					"FindGroupConnectionClientActionCompositionPlanService",
					"CM_FIND_GROUP.runImpl action switch"),
				new FindGroupConnectionBoundaryComponentReadiness(
					"Instance application direct packet executor",
					"FindGroupInstanceApplicationDirectDispatchPlanService can compose direct SM_FIND_GROUP applicant packet and declined SM_MESSAGE whisper dispatch evidence without live connection-registry sends.",
					FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable,
					"FindGroupInstanceApplicationDirectDispatchPlanService",
					"FindGroupService.sendInstanceApplication/sendInstanceApplicationResult declined branch"),
				new FindGroupConnectionBoundaryComponentReadiness(
					"Action 12 invite executor",
					"FindGroupInstanceApplicationInviteDispatchPlanService can compose group/alliance invite request-service results without live packet dispatch.",
					FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable,
					"FindGroupInstanceApplicationInviteDispatchPlanService",
					"FindGroupService.sendInstanceApplicationResult"),
				new FindGroupConnectionBoundaryComponentReadiness(
					"Side-effect dispatch audit and executor",
					"FindGroupSideEffectDispatchAuditService can audit direct sendPacket and broadcastToWorld intents; FindGroupSideEffectDispatchExecutorService can execute them through IGameClientConnectionRegistry only when explicitly invoked; FindGroupConnectionBoundarySideEffectCompositionEvidenceService can compose parsed CmFindGroup plans, including action 1 world-broadcast intents, with opt-in executor results.",
					FindGroupConnectionBoundaryComponentStatus.EvidenceAvailable,
					"FindGroupSideEffectDispatchAuditService; FindGroupSideEffectDispatchExecutorService; FindGroupConnectionBoundarySideEffectCompositionEvidenceService",
					"FindGroupService PacketSendUtility.sendPacket/broadcastToWorld call sites"),
				new FindGroupConnectionBoundaryComponentReadiness(
					"Lifecycle observers",
					"Logout cleanup and joined-team observer evidence exists, but the C# port has not wired FindGroupService as a live singleton across all callers.",
					FindGroupConnectionBoundaryComponentStatus.PartialEvidence,
					"PlayerEnterWorldService; PlayerGroupInviteRequestService; PlayerAllianceInviteRequestService; FindGroupJoinedTeamLifecycleRecorder",
					"FindGroupService.onLogout/onJoinedTeam"),
			],
			[
				"Do not enable live CmFindGroup dispatch until direct packet sends, world race-filter fanout, action 11 instance application dispatch, declined action 12 whisper dispatch, action 12 invite dispatch, and lifecycle singleton wiring are all reviewed together.",
				"Any future live executor must prove packet order, race visibility filters, connection-registry behavior, and service concurrency against Java.",
				"Actions 20 and 25 remain parsed-only because Java readImpl parses them but runImpl has no branch.",
			]);
	}
}

public enum FindGroupConnectionBoundaryReadinessStatus
{
	BlockedPendingBoundaryWiring,
	Ready,
}

public enum FindGroupConnectionBoundaryComponentStatus
{
	Blocked,
	PartialEvidence,
	EvidenceAvailable,
	Ready,
}

public sealed record FindGroupConnectionBoundaryReadinessAggregate(
	FindGroupConnectionBoundaryReadinessStatus Status,
	string CSharpBoundary,
	string JavaBoundary,
	FindGroupLiveDispatchReadinessReport LiveDispatchReadiness,
	IReadOnlyList<FindGroupConnectionBoundaryComponentReadiness> Components,
	IReadOnlyList<string> NextLiveDispatchRequirements)
{
	public bool IsReadyForLiveDispatch =>
		Status == FindGroupConnectionBoundaryReadinessStatus.Ready
		&& LiveDispatchReadiness.IsReadyForLiveDispatch
		&& Components.All(component => component.Status == FindGroupConnectionBoundaryComponentStatus.Ready);
}

public sealed record FindGroupConnectionBoundaryComponentReadiness(
	string Name,
	string Evidence,
	FindGroupConnectionBoundaryComponentStatus Status,
	string CSharpSource,
	string JavaSource);
