namespace Aion.GameServer.Services;

public static class FindGroupDirectPacketTriggerOrderingReadinessService
{
	public static FindGroupDirectPacketTriggerOrderingReadinessReport CreateReport()
	{
		return new FindGroupDirectPacketTriggerOrderingReadinessReport(
			FindGroupDirectPacketTriggerOrderingReadinessStatus.BlockedPendingLiveBoundaryOrderingEvidence,
			"game-server/src/com/aionemu/gameserver/network/aion/AionClientPacket.java run",
			"game-server/src/com/aionemu/gameserver/network/aion/clientpackets/CM_FIND_GROUP.java runImpl",
			"dotnetConversion/src/Aion.GameServer/Network/Aion/GameServerConnection.cs ProcessPacketAsync case CmFindGroup",
			[
				new FindGroupDirectPacketTriggerOrderingEvidence(
					FindGroupDirectPacketTriggerOrderingEvidenceKind.JavaTriggerBeforeRunImpl,
					"Java AionClientPacket.run validates the packet and invokes CM_FIND_GROUP.runImpl synchronously.",
					FindGroupDirectPacketTriggerOrderingEvidenceStatus.Reviewed),
				new FindGroupDirectPacketTriggerOrderingEvidence(
					FindGroupDirectPacketTriggerOrderingEvidenceKind.JavaSequentialSendPacketCalls,
					"Java FindGroupService direct PacketSendUtility.sendPacket calls occur in the branch order reached from CM_FIND_GROUP.runImpl.",
					FindGroupDirectPacketTriggerOrderingEvidenceStatus.Reviewed),
				new FindGroupDirectPacketTriggerOrderingEvidence(
					FindGroupDirectPacketTriggerOrderingEvidenceKind.CSharpOptInExecutorOrder,
					"C# FindGroupSideEffectDispatchExecutorService preserves direct-intent order when explicitly invoked by controlled evidence tests.",
					FindGroupDirectPacketTriggerOrderingEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketTriggerOrderingEvidence(
					FindGroupDirectPacketTriggerOrderingEvidenceKind.CSharpDisabledBoundaryActionZeroTrace,
					"C# focused tests record disabled CM_FIND_GROUP action 0 boundary acceptance before opt-in registry execution of the direct SmFindGroup packet to the active player.",
					FindGroupDirectPacketTriggerOrderingEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketTriggerOrderingEvidence(
					FindGroupDirectPacketTriggerOrderingEvidenceKind.CSharpTriggerBoundaryWiring,
					"C# GameServerConnection.ProcessPacketAsync still defers CmFindGroup and does not invoke the opt-in executor from the triggering client packet.",
					FindGroupDirectPacketTriggerOrderingEvidenceStatus.Blocked),
				new FindGroupDirectPacketTriggerOrderingEvidence(
					FindGroupDirectPacketTriggerOrderingEvidenceKind.LiveSocketOrderingComparison,
					"No live connection-registry or socket-level test proves CM_FIND_GROUP direct packets are emitted after the triggering client packet is accepted and before later boundary work.",
					FindGroupDirectPacketTriggerOrderingEvidenceStatus.Blocked),
			],
			[
				"Do not claim live direct-packet parity from opt-in executor ordering alone.",
				"Before enabling live CmFindGroup, add a connection-boundary test that observes the triggering packet and the resulting direct SendPacketAsync calls in one ordered trace.",
				"Keep action 20 and 25 parsed-only no-op behavior outside the direct-packet ordering requirement.",
			]);
	}
}

public enum FindGroupDirectPacketTriggerOrderingReadinessStatus
{
	BlockedPendingLiveBoundaryOrderingEvidence,
	Ready,
}

public enum FindGroupDirectPacketTriggerOrderingEvidenceKind
{
	JavaTriggerBeforeRunImpl,
	JavaSequentialSendPacketCalls,
	CSharpOptInExecutorOrder,
	CSharpDisabledBoundaryActionZeroTrace,
	CSharpTriggerBoundaryWiring,
	LiveSocketOrderingComparison,
}

public enum FindGroupDirectPacketTriggerOrderingEvidenceStatus
{
	Reviewed,
	EvidenceAvailable,
	Blocked,
	Ready,
}

public sealed record FindGroupDirectPacketTriggerOrderingReadinessReport(
	FindGroupDirectPacketTriggerOrderingReadinessStatus Status,
	string JavaClientPacketRunSource,
	string JavaFindGroupRunImplSource,
	string CSharpBoundarySource,
	IReadOnlyList<FindGroupDirectPacketTriggerOrderingEvidence> Evidence,
	IReadOnlyList<string> NextRequiredEvidence)
{
	public bool IsReadyForLiveDirectPacketOrdering =>
		Status == FindGroupDirectPacketTriggerOrderingReadinessStatus.Ready
		&& Evidence.All(evidence => evidence.Status == FindGroupDirectPacketTriggerOrderingEvidenceStatus.Ready);
}

public sealed record FindGroupDirectPacketTriggerOrderingEvidence(
	FindGroupDirectPacketTriggerOrderingEvidenceKind Kind,
	string Detail,
	FindGroupDirectPacketTriggerOrderingEvidenceStatus Status);
