using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHousePayRent : GameServerPacket
{
	public const int PacketOpCode = 262;

	private readonly int _weeksPaid;

	public SmHousePayRent(int weeksPaid)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_PAY_RENT.
		_weeksPaid = weeksPaid;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_HOUSE_PAY_RENT.writeImpl.
		buffer.WriteC(0);
		buffer.WriteC(_weeksPaid);
	}
}
