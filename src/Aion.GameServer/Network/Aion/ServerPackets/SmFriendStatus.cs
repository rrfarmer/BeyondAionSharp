using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmFriendStatus : GameServerPacket
{
	public const int PacketOpCode = 227;
	private readonly byte _status;

	public SmFriendStatus(byte status)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_STATUS(int status).
		_status = status;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_STATUS.writeImpl.
		buffer.WriteC(_status);
	}
}
