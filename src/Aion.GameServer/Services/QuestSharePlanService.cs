using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: network/aion/clientpackets/CM_QUEST_SHARE.runImpl decision logic.
/// Pure planner: given the sharer, the quest template/state and the resolved current-team online members,
/// it produces the ordered list of packets to fan out. The GameServerConnection handler resolves the inputs
/// (template table, team members) from runtime state and executes the plan.
/// </summary>
public static class QuestSharePlanService
{
	// Java: GroupConfig.GROUP_MAX_DISTANCE (gameserver.playergroup.maxdistance, default 100).
	public const float GroupMaxDistance = 100f;

	// Java raw SM_SYSTEM_MESSAGE ids used directly in CM_QUEST_SHARE.runImpl.
	public const int MsgCannotShare = 1100001;       // This quest cannot be shared.
	public const int MsgNoGroupMembers = 1100000;    // There are no group members to share the quest with.
	public const int MsgNoAllianceMembers = 1100005; // There are no Alliance members to share the quest with.
	public const int MsgSharedWith = 1100002;        // You shared the quest with %0.
	public const int MsgFailedToShare = 1100003;     // You failed to share the quest with %0.

	public static QuestSharePlan Plan(
		Player sharer,
		int questId,
		NearbyQuestTemplateSummary? template,
		PlayerQuestState? questState,
		IReadOnlyList<Player> currentTeamOnlineMembers,
		Func<Player, bool> canStartConditions,
		float groupMaxDistance = GroupMaxDistance)
	{
		var instructions = new List<QuestShareInstruction>();

		// Java: questTemplate == null || questTemplate.isCannotShare() -> 1100001, return.
		if (template == null || template.CannotShare)
		{
			instructions.Add(new QuestShareInstruction(sharer.ObjectId, new SmSystemMessage(MsgCannotShare)));
			return new QuestSharePlan(instructions);
		}

		// Java: questState == null || status == COMPLETE -> return (no message).
		if (questState == null || questState.IsComplete)
			return new QuestSharePlan(instructions);

		// Java: currentGroup.filterMembers(allExcept(player) AND ONLINE AND isInRange(member, player, GROUP_MAX_DISTANCE)).
		// currentTeamOnlineMembers is the whole current group OR whole alliance, already online; exclude self + range-gate.
		var shareList = currentTeamOnlineMembers
			.Where(member => member.ObjectId != sharer.ObjectId
				&& IsInGroupRange(sharer, member, groupMaxDistance))
			.ToList();

		// Java: membersToShareWith.isEmpty() -> 1100005 if target == ALLIANCE else 1100000, return.
		if (shareList.Count == 0)
		{
			var emptyMessageId = string.Equals(template.Target, "ALLIANCE", StringComparison.Ordinal)
				? MsgNoAllianceMembers
				: MsgNoGroupMembers;
			instructions.Add(new QuestShareInstruction(sharer.ObjectId, new SmSystemMessage(emptyMessageId)));
			return new QuestSharePlan(instructions);
		}

		// Java: per member, checkStartConditions(member, questId, false) -> 1100003 fail, else SM_QUEST_ACTION.SHARE + 1100002.
		foreach (var member in shareList)
		{
			if (!canStartConditions(member))
			{
				instructions.Add(new QuestShareInstruction(sharer.ObjectId, new SmSystemMessage(MsgFailedToShare, member.Name)));
				continue;
			}

			// Java: SM_QUEST_ACTION(questId, player.getObjectId(), member.isInAlliance()).
			var shareInAlliance = member.TeamMembership == PlayerTeamMembership.Alliance;
			instructions.Add(new QuestShareInstruction(member.ObjectId, SmQuestAction.Share(questId, sharer.ObjectId, shareInAlliance)));
			instructions.Add(new QuestShareInstruction(sharer.ObjectId, new SmSystemMessage(MsgSharedWith, member.Name)));
		}

		return new QuestSharePlan(instructions);
	}

	private static bool IsInGroupRange(Player sharer, Player member, float range)
	{
		// Java parity: PositionUtil.isInRange(member, player, range) (centerToCenter=true) requires same world+instance.
		return sharer.Position.WorldId == member.Position.WorldId
			&& sharer.Position.InstanceId == member.Position.InstanceId
			&& PositionUtilService.IsInRange(
				sharer.Position.X, sharer.Position.Y, sharer.Position.Z,
				member.Position.X, member.Position.Y, member.Position.Z,
				range);
	}
}

public sealed record QuestShareInstruction(int RecipientObjectId, GameServerPacket Packet);

public sealed record QuestSharePlan(IReadOnlyList<QuestShareInstruction> Instructions);
