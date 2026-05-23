using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmGroupMemberInfo : GameServerPacket
{
	public const int PacketOpCode = 91;
	private readonly PlayerGroupMemberInfoPacketPlan _plan;

	public SmGroupMemberInfo(PlayerGroupMemberInfoPacketPlan plan)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_MEMBER_INFO(PlayerGroup, Player, GroupEvent, int).
		_plan = plan;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_MEMBER_INFO.writeImpl fixed prefix.
		var prefix = _plan.PrefixSnapshot;
		buffer.WriteD(_plan.GroupId);
		buffer.WriteD(_plan.MemberObjectId);
		buffer.WriteD(prefix.MaxHp ?? 0);
		buffer.WriteD(prefix.CurrentHp);
		buffer.WriteD(prefix.MaxMp ?? 0);
		buffer.WriteD(prefix.CurrentMp);
		buffer.WriteD(prefix.MaxFp ?? 0);
		buffer.WriteD(prefix.CurrentFp);
		buffer.WriteD(prefix.Unknown3Point5);
		buffer.WriteD(prefix.MapId);
		buffer.WriteD(prefix.MapInstanceId);
		buffer.WriteF(prefix.X);
		buffer.WriteF(prefix.Y);
		buffer.WriteF(prefix.Z);
		buffer.WriteC(prefix.ClassId);
		buffer.WriteC(prefix.GenderId);
		buffer.WriteC(prefix.Level);
		buffer.WriteC(prefix.EventId);
		buffer.WriteC(prefix.AlwaysOne);
		buffer.WriteC(prefix.FlyState);
		buffer.WriteC(prefix.MentorFlag);

		if (_plan.EffectiveEvent is PlayerGroupEvent.Movement
			or PlayerGroupEvent.Disconnected
			or PlayerGroupEvent.Leave)
		{
			return;
		}

		throw new NotSupportedException(
			$"SM_GROUP_MEMBER_INFO branch {_plan.EffectiveEvent} is not ported yet; name/effect/slot-timer payloads are pending.");
	}
}
