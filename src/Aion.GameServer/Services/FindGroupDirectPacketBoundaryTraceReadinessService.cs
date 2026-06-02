namespace Aion.GameServer.Services;

public static class FindGroupDirectPacketBoundaryTraceReadinessService
{
	public static FindGroupDirectPacketBoundaryTraceReadinessReport CreateReport()
	{
		return new FindGroupDirectPacketBoundaryTraceReadinessReport(
			FindGroupDirectPacketBoundaryTraceReadinessStatus.BlockedPendingLiveProcessPacketTrace,
			"game-server/src/com/aionemu/gameserver/network/aion/clientpackets/CM_FIND_GROUP.java",
			"dotnetConversion/src/Aion.GameServer/Network/Aion/GameServerConnection.cs",
			[
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionZeroDirectSend,
					"Java CM_FIND_GROUP action 0 synchronously calls FindGroupService.showRecruitments(player), which sends SM_FIND_GROUP action 0 directly to the triggering player.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionFourDirectSend,
					"Java CM_FIND_GROUP action 4 synchronously calls FindGroupService.showApplications(player), which sends SM_FIND_GROUP action 4 directly to the triggering player.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionZeroComposition,
					"C# GameServerConnection.CreateDisabledFindGroupBoundaryPlan can compose action 0 as a direct SmFindGroup intent for the active player without invoking live socket sends.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionFourComposition,
					"C# GameServerConnection.CreateDisabledFindGroupBoundaryPlan can compose action 4 as a direct SmFindGroup intent for the active player without invoking live socket sends.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpOptInRegistryExecutionTrace,
					"C# focused tests can record disabled CM_FIND_GROUP action 0/4 acceptance before an opt-in registry direct send of SmFindGroup to the same player.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.LiveProcessPacketAsyncTrace,
					"C# GameServerConnection.ProcessPacketAsync still defers CmFindGroup, so no live boundary trace proves the direct packet is emitted from the triggering client-packet path.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.Blocked),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.LiveSocketComparison,
					"No encrypted socket or real-client comparison has observed action 0 direct-packet ordering.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.Blocked),
			],
			[
				"Do not claim live direct-packet parity from the disabled helper plus opt-in executor trace.",
				"Before enabling live CmFindGroup direct actions, add a ProcessPacketAsync boundary trace or runtime/socket comparison for actions 0 and 4.",
				"Keep the disabled boundary helper available as the reviewed composition surface for future live wiring.",
			]);
	}
}

public enum FindGroupDirectPacketBoundaryTraceReadinessStatus
{
	BlockedPendingLiveProcessPacketTrace,
	Ready,
}

public enum FindGroupDirectPacketBoundaryTraceEvidenceKind
{
	JavaActionZeroDirectSend,
	JavaActionFourDirectSend,
	CSharpDisabledBoundaryActionZeroComposition,
	CSharpDisabledBoundaryActionFourComposition,
	CSharpOptInRegistryExecutionTrace,
	LiveProcessPacketAsyncTrace,
	LiveSocketComparison,
}

public enum FindGroupDirectPacketBoundaryTraceEvidenceStatus
{
	Reviewed,
	EvidenceAvailable,
	Blocked,
	Ready,
}

public sealed record FindGroupDirectPacketBoundaryTraceReadinessReport(
	FindGroupDirectPacketBoundaryTraceReadinessStatus Status,
	string JavaFindGroupSource,
	string CSharpBoundarySource,
	IReadOnlyList<FindGroupDirectPacketBoundaryTraceEvidence> Evidence,
	IReadOnlyList<string> NextRequiredEvidence)
{
	public bool IsReadyForLiveDirectPacketBoundary =>
		Status == FindGroupDirectPacketBoundaryTraceReadinessStatus.Ready
		&& Evidence.All(evidence => evidence.Status == FindGroupDirectPacketBoundaryTraceEvidenceStatus.Ready);
}

public sealed record FindGroupDirectPacketBoundaryTraceEvidence(
	FindGroupDirectPacketBoundaryTraceEvidenceKind Kind,
	string Detail,
	FindGroupDirectPacketBoundaryTraceEvidenceStatus Status);
