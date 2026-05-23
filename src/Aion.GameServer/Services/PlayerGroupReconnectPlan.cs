using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record PlayerGroupReconnectResult(
	bool Reconnected,
	PlayerGroupReconnectPacketPlan? PacketPlan)
{
	public static PlayerGroupReconnectResult NotFound()
	{
		return new PlayerGroupReconnectResult(false, null);
	}
}

public sealed record PlayerGroupReconnectPacketPlan(
	int TeamId,
	int ReconnectingPlayerObjectId,
	bool SendGroupInfoToReconnectingPlayer,
	IReadOnlyList<PlayerGroupMemberInfoIntent> MemberInfoIntents,
	PlayerGroupInfoPacketPlan? GroupInfoPlan)
{
	public SmGroupInfo? CreateGroupInfoPacket()
	{
		// Java parity: model/team/group/events/PlayerConnectedEvent sends SM_GROUP_INFO to the reconnecting player.
		return SendGroupInfoToReconnectingPlayer && GroupInfoPlan != null
			? new SmGroupInfo(GroupInfoPlan)
			: null;
	}
}

public sealed record PlayerGroupMemberInfoIntent(
	int RecipientObjectId,
	int SubjectObjectId,
	PlayerGroupEvent Event,
	PlayerGroupMemberInfoPacketPlan? PacketPlan = null);

public sealed record PlayerGroupMemberInfoPacketPlan(
	int GroupId,
	int MemberObjectId,
	PlayerGroupEvent RequestedEvent,
	PlayerGroupEvent EffectiveEvent,
	int Slot,
	bool IsOnline,
	bool WritesLifeStatsBlock,
	bool WritesPositionBlock,
	bool WritesCommonDataBlock,
	bool WritesName,
	bool WritesAbnormalEffects,
	bool WritesSlotTimers)
{
	public static PlayerGroupMemberInfoPacketPlan FromMember(
		int groupId,
		PlayerGroupMember member,
		PlayerGroupEvent requestedEvent,
		int slot = 0)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_MEMBER_INFO.writeImpl header and event branch selection.
		var effectiveEvent = requestedEvent == PlayerGroupEvent.Enter && !member.IsOnline
			? PlayerGroupEvent.EnterOffline
			: requestedEvent;
		var writesName = effectiveEvent is PlayerGroupEvent.EnterOffline
			or PlayerGroupEvent.Join
			or PlayerGroupEvent.Enter
			or PlayerGroupEvent.Update;
		var writesEffects = effectiveEvent is PlayerGroupEvent.Enter
			or PlayerGroupEvent.Update
			or PlayerGroupEvent.UpdateEffects;

		return new PlayerGroupMemberInfoPacketPlan(
			groupId,
			member.ObjectId,
			requestedEvent,
			effectiveEvent,
			slot,
			member.IsOnline,
			WritesLifeStatsBlock: true,
			WritesPositionBlock: true,
			WritesCommonDataBlock: true,
			writesName,
			writesEffects,
			WritesSlotTimers: writesEffects);
	}
}
