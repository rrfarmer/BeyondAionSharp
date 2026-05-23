using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAllianceMemberInfo : GameServerPacket
{
	public const int PacketOpCode = 246;

	private readonly PlayerAllianceMemberInfoPacketPlan _plan;

	public SmAllianceMemberInfo(PlayerAllianceMemberInfoPacketPlan plan)
		: base(PacketOpCode)
	{
		_plan = plan;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ALLIANCE_MEMBER_INFO.writeImpl.
		var prefix = _plan.PrefixSnapshot;
		buffer.WriteD(_plan.AllianceId);
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
		buffer.WriteC(prefix.AllianceUnknown);

		if (_plan.EffectiveEvent is PlayerAllianceEvent.Leave
			or PlayerAllianceEvent.Banned
			or PlayerAllianceEvent.Movement
			or PlayerAllianceEvent.Disconnected)
			return;

		throw new NotSupportedException($"Alliance member info event {_plan.EffectiveEvent} is not ported yet.");
	}
}
