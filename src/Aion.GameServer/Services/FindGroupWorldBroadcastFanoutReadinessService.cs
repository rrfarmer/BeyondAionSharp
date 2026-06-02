namespace Aion.GameServer.Services;

public static class FindGroupWorldBroadcastFanoutReadinessService
{
	public static FindGroupWorldBroadcastFanoutReadinessReport CreateReport()
	{
		return new FindGroupWorldBroadcastFanoutReadinessReport(
			FindGroupWorldBroadcastFanoutReadinessStatus.BlockedPendingLiveBoundaryFanoutEvidence,
			"game-server/src/com/aionemu/gameserver/utils/PacketSendUtility.java broadcastToWorld",
			"game-server/src/com/aionemu/gameserver/services/findgroup/FindGroupService.java removeRecruitment/removeApplication",
			"dotnetConversion/src/Aion.GameServer/Network/Aion/GameClientSocketServer.cs BroadcastToWorldAsync",
			"dotnetConversion/src/Aion.GameServer/Services/FindGroupSideEffectDispatchExecutorService.cs",
			[
				new FindGroupWorldBroadcastFanoutEvidence(
					FindGroupWorldBroadcastFanoutEvidenceKind.JavaWorldIteration,
					"Java PacketSendUtility.broadcastToWorld iterates World.forEachPlayer and calls sendPacket only for players accepted by the predicate.",
					FindGroupWorldBroadcastFanoutEvidenceStatus.Reviewed),
				new FindGroupWorldBroadcastFanoutEvidence(
					FindGroupWorldBroadcastFanoutEvidenceKind.JavaFindGroupRaceFilter,
					"Java FindGroupService.removeRecruitment filters by recruitment.getRace(); removeApplication filters by application.getPlayer().getRace().",
					FindGroupWorldBroadcastFanoutEvidenceStatus.Reviewed),
				new FindGroupWorldBroadcastFanoutEvidence(
					FindGroupWorldBroadcastFanoutEvidenceKind.CSharpRegistryRaceFilter,
					"C# GameClientSocketServer.BroadcastToWorldAsync iterates online connections, skips missing active players, and sends only when the filter accepts the player.",
					FindGroupWorldBroadcastFanoutEvidenceStatus.EvidenceAvailable),
				new FindGroupWorldBroadcastFanoutEvidence(
					FindGroupWorldBroadcastFanoutEvidenceKind.CSharpOptInExecutorOrder,
					"C# FindGroupSideEffectDispatchExecutorService applies the recorded race filter and records world-broadcast execution order when explicitly invoked.",
					FindGroupWorldBroadcastFanoutEvidenceStatus.EvidenceAvailable),
				new FindGroupWorldBroadcastFanoutEvidence(
					FindGroupWorldBroadcastFanoutEvidenceKind.CSharpLiveBoundaryWiring,
					"C# GameServerConnection.ProcessPacketAsync still defers CmFindGroup and does not invoke world-broadcast fanout from the triggering client packet.",
					FindGroupWorldBroadcastFanoutEvidenceStatus.Blocked),
				new FindGroupWorldBroadcastFanoutEvidence(
					FindGroupWorldBroadcastFanoutEvidenceKind.LiveRuntimeComparison,
					"No live connection-registry, socket, or real-client comparison proves FindGroup broadcast recipient filtering or ordering from CM_FIND_GROUP actions 1 and 5.",
					FindGroupWorldBroadcastFanoutEvidenceStatus.Blocked),
			],
			[
				"Do not claim live world-broadcast parity from opt-in executor race-filter evidence alone.",
				"Before enabling live CmFindGroup actions 1 and 5, add a live boundary trace proving same-race recipients, opposite-race exclusion, and broadcast ordering from the triggering client packet.",
				"Keep direct packet ordering and action 12 invite dispatch as separate gates.",
			]);
	}
}

public enum FindGroupWorldBroadcastFanoutReadinessStatus
{
	BlockedPendingLiveBoundaryFanoutEvidence,
	Ready,
}

public enum FindGroupWorldBroadcastFanoutEvidenceKind
{
	JavaWorldIteration,
	JavaFindGroupRaceFilter,
	CSharpRegistryRaceFilter,
	CSharpOptInExecutorOrder,
	CSharpLiveBoundaryWiring,
	LiveRuntimeComparison,
}

public enum FindGroupWorldBroadcastFanoutEvidenceStatus
{
	Reviewed,
	EvidenceAvailable,
	Blocked,
	Ready,
}

public sealed record FindGroupWorldBroadcastFanoutReadinessReport(
	FindGroupWorldBroadcastFanoutReadinessStatus Status,
	string JavaPacketSendUtilitySource,
	string JavaFindGroupSource,
	string CSharpRegistrySource,
	string CSharpExecutorSource,
	IReadOnlyList<FindGroupWorldBroadcastFanoutEvidence> Evidence,
	IReadOnlyList<string> NextRequiredEvidence)
{
	public bool IsReadyForLiveWorldBroadcastFanout =>
		Status == FindGroupWorldBroadcastFanoutReadinessStatus.Ready
		&& Evidence.All(evidence => evidence.Status == FindGroupWorldBroadcastFanoutEvidenceStatus.Ready);
}

public sealed record FindGroupWorldBroadcastFanoutEvidence(
	FindGroupWorldBroadcastFanoutEvidenceKind Kind,
	string Detail,
	FindGroupWorldBroadcastFanoutEvidenceStatus Status);
