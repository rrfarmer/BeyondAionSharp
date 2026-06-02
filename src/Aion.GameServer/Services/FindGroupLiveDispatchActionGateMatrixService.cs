namespace Aion.GameServer.Services;

public static class FindGroupLiveDispatchActionGateMatrixService
{
	public static FindGroupLiveDispatchActionGateMatrix CreateMatrix()
	{
		var entries = new[]
		{
			Executable(0, "showRecruitments", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(1, "removeRecruitment", [FindGroupLiveDispatchGoNoGoChecklistItemKind.WorldBroadcastDispatch]),
			Executable(2, "addRecruitment", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(3, "updateRecruitment", [FindGroupLiveDispatchGoNoGoChecklistItemKind.SharedSingletonLifecycle]),
			Executable(4, "showApplications", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(5, "removeApplication", [FindGroupLiveDispatchGoNoGoChecklistItemKind.WorldBroadcastDispatch]),
			Executable(6, "addApplication", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(7, "updateApplication", [FindGroupLiveDispatchGoNoGoChecklistItemKind.SharedSingletonLifecycle]),
			Executable(8, "registerInstanceGroup", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(9, "removeInstanceGroup", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(10, "showInstanceGroups(false)", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(11, "sendInstanceApplication", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(12, "sendInstanceApplicationResult", [FindGroupLiveDispatchGoNoGoChecklistItemKind.ActionTwelveInviteDispatch, FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(13, "showInstanceGroups(true)", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(15, "showInstanceGroupMembersInfo", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			Executable(17, "updateInstanceGroup", [FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch]),
			ParsedOnly(20, "clicked Enter button in Prepare for entry window"),
			ParsedOnly(25, "ban from instance group"),
		};

		return new FindGroupLiveDispatchActionGateMatrix(
			"game-server/src/com/aionemu/gameserver/network/aion/clientpackets/CM_FIND_GROUP.java readImpl/runImpl",
			"game-server/src/com/aionemu/gameserver/services/findgroup/FindGroupService.java",
			entries,
			"Actions 14 and 16 are server-packet action codes, not CM_FIND_GROUP client runImpl branches.");
	}

	private static FindGroupLiveDispatchActionGateMatrixEntry Executable(
		int action,
		string javaMethod,
		IReadOnlyList<FindGroupLiveDispatchGoNoGoChecklistItemKind> missingLiveGates)
	{
		return new FindGroupLiveDispatchActionGateMatrixEntry(
			action,
			javaMethod,
			FindGroupLiveDispatchActionRuntimeShape.ExecutableRunImplBranch,
			missingLiveGates,
			FindGroupLiveDispatchActionGateStatus.BlockedPendingLiveEvidence,
			"Java runImpl executes this action through FindGroupService.getInstance(); C# live ProcessPacketAsync dispatch remains deferred.");
	}

	private static FindGroupLiveDispatchActionGateMatrixEntry ParsedOnly(int action, string javaReadComment)
	{
		return new FindGroupLiveDispatchActionGateMatrixEntry(
			action,
			javaReadComment,
			FindGroupLiveDispatchActionRuntimeShape.ParsedOnlyNoRunBranch,
			[FindGroupLiveDispatchGoNoGoChecklistItemKind.ParsedOnlyNoRunActions],
			FindGroupLiveDispatchActionGateStatus.ReadyParsedOnlyNoOp,
			"Java readImpl parses this action, but runImpl has no branch; preserve as a no-side-effect live adapter result.");
	}
}

public enum FindGroupLiveDispatchActionRuntimeShape
{
	ExecutableRunImplBranch,
	ParsedOnlyNoRunBranch,
}

public enum FindGroupLiveDispatchActionGateStatus
{
	BlockedPendingLiveEvidence,
	ReadyParsedOnlyNoOp,
}

public sealed record FindGroupLiveDispatchActionGateMatrix(
	string JavaClientPacketSource,
	string JavaServiceSource,
	IReadOnlyList<FindGroupLiveDispatchActionGateMatrixEntry> Entries,
	string BoundaryNote)
{
	public bool IsReadyForLiveDispatch => Entries.All(entry => entry.Status == FindGroupLiveDispatchActionGateStatus.ReadyParsedOnlyNoOp);
}

public sealed record FindGroupLiveDispatchActionGateMatrixEntry(
	int Action,
	string JavaRunImplTarget,
	FindGroupLiveDispatchActionRuntimeShape RuntimeShape,
	IReadOnlyList<FindGroupLiveDispatchGoNoGoChecklistItemKind> MissingLiveGates,
	FindGroupLiveDispatchActionGateStatus Status,
	string Evidence);
