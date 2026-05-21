using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMarkFriendList : GameServerPacket
{
	public const int PacketOpCode = 279;
	private readonly int _playerObjectId;

	public SmMarkFriendList(int playerObjectId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MARK_FRIENDLIST.
		_playerObjectId = playerObjectId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MARK_FRIENDLIST.writeImpl.
		buffer.WriteD(_playerObjectId);
		buffer.WriteC(1);
		buffer.WriteH(0);
	}
}
