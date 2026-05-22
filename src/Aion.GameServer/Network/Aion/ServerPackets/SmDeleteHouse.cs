using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDeleteHouse : GameServerPacket
{
	public const int PacketOpCode = 272;

	private readonly int _addressId;

	public SmDeleteHouse(int addressId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_DELETE_HOUSE.
		_addressId = addressId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_addressId);
	}
}
