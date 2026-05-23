using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLeaveGroupMember : GameServerPacket
{
	public const int PacketOpCode = 247;

	public SmLeaveGroupMember()
		: base(PacketOpCode)
	{
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEAVE_GROUP_MEMBER.writeImpl.
		buffer.WriteD(0x00);
		buffer.WriteC(0x00);
		buffer.WriteD(0x3F);
		buffer.WriteD(0x00);
		buffer.WriteH(0x00);
	}
}
