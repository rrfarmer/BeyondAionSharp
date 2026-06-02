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
					FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionEightDirectSend,
					"Java CM_FIND_GROUP action 8 synchronously calls FindGroupService.registerInstanceGroup(player, instanceMaskId, message, minMembers), which stores the instance group and sends SM_FIND_GROUP action 14 directly to the triggering player.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionNineDirectSend,
					"Java CM_FIND_GROUP action 9 synchronously calls FindGroupService.removeInstanceGroup(player), which removes by active player object id and always sends the SM_FIND_GROUP action 10 updated show-list packet.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionTenDirectSend,
					"Java CM_FIND_GROUP action 10 synchronously calls FindGroupService.showInstanceGroups(player, false), which may send SM_FIND_GROUP action 26 directly to the triggering player before the SM_FIND_GROUP action 10 show-list packet.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionElevenDirectSend,
					"Java CM_FIND_GROUP action 11 synchronously calls FindGroupService.sendInstanceApplication(player, playerOrTeamId), which resolves the recruiter and sends SM_FIND_GROUP applicant details directly to that recruiter.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionThirteenDirectSend,
					"Java CM_FIND_GROUP action 13 synchronously calls FindGroupService.showInstanceGroups(player, true), which skips the action 26 mask-list branch and sends the SM_FIND_GROUP action 10 show-list packet directly to the triggering player.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.Reviewed),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.JavaActionFifteenDirectSend,
					"Java CM_FIND_GROUP action 15 synchronously calls FindGroupService.showInstanceGroupMembersInfo(player, playerOrTeamId), which sends SM_FIND_GROUP action 16 directly to the triggering player when the instance group exists.",
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
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionEightComposition,
					"C# GameServerConnection.CreateDisabledFindGroupBoundaryPlan can compose action 8 as a direct SmFindGroup action 14 intent for the active player without invoking live socket sends.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionNineComposition,
					"C# GameServerConnection.CreateDisabledFindGroupBoundaryPlan can compose action 9 removed and missing outcomes as direct SmFindGroup action 10 updated show-list intents for the active player without invoking live socket sends.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionTenComposition,
					"C# GameServerConnection.CreateDisabledFindGroupBoundaryPlan can compose action 10 as direct SmFindGroup action 10 show-list intent, plus action 26 mask-list intent first when form-anywhere is enabled, without invoking live socket sends.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionElevenComposition,
					"C# GameServerConnection.CreateDisabledFindGroupBoundaryPlan can compose action 11 as a direct SmFindGroup applicant intent for the resolved recruiter without invoking live socket sends.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionThirteenComposition,
					"C# GameServerConnection.CreateDisabledFindGroupBoundaryPlan can compose action 13 as a direct SmFindGroup action 10 show-list intent without an action 26 mask-list intent, even when form-anywhere is enabled, without invoking live socket sends.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpDisabledBoundaryActionFifteenComposition,
					"C# GameServerConnection.CreateDisabledFindGroupBoundaryPlan can compose action 15 as a direct SmFindGroup action 16 member-info intent for the active player without invoking live socket sends.",
					FindGroupDirectPacketBoundaryTraceEvidenceStatus.EvidenceAvailable),
				new FindGroupDirectPacketBoundaryTraceEvidence(
					FindGroupDirectPacketBoundaryTraceEvidenceKind.CSharpOptInRegistryExecutionTrace,
					"C# focused tests can record disabled CM_FIND_GROUP action 0/4/8/9/10/11/13/15 acceptance before opt-in registry direct sends of SmFindGroup to the Java-selected recipient.",
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
				"Before enabling live CmFindGroup direct actions, add a ProcessPacketAsync boundary trace or runtime/socket comparison for actions 0, 4, 8, 9, 10, 11, 13, and 15.",
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
	JavaActionEightDirectSend,
	JavaActionNineDirectSend,
	JavaActionTenDirectSend,
	JavaActionElevenDirectSend,
	JavaActionThirteenDirectSend,
	JavaActionFifteenDirectSend,
	CSharpDisabledBoundaryActionZeroComposition,
	CSharpDisabledBoundaryActionFourComposition,
	CSharpDisabledBoundaryActionEightComposition,
	CSharpDisabledBoundaryActionNineComposition,
	CSharpDisabledBoundaryActionTenComposition,
	CSharpDisabledBoundaryActionElevenComposition,
	CSharpDisabledBoundaryActionThirteenComposition,
	CSharpDisabledBoundaryActionFifteenComposition,
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
