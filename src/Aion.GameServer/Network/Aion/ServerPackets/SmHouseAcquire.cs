using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseAcquire : GameServerPacket
{
	public const int PacketOpCode = 275;

	private readonly int _playerObjectId;
	private readonly int _addressId;
	private readonly bool _acquire;

	public SmHouseAcquire(int playerObjectId, int addressId, bool acquire)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_ACQUIRE.
		_playerObjectId = playerObjectId;
		_addressId = addressId;
		_acquire = acquire;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_HOUSE_ACQUIRE.writeImpl.
		buffer.WriteD(_playerObjectId);
		buffer.WriteD(_addressId);
		buffer.WriteD(_acquire ? 1 : 0);
	}
}
