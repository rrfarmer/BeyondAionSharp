using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPrivateStoreName : GameServerPacket
{
	public const int PacketOpCode = 145;
	private readonly int _playerObjectId;
	private readonly string _storeMessage;

	public SmPrivateStoreName(int playerObjectId, string storeMessage) : base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PRIVATE_STORE_NAME.writeImpl.
		_playerObjectId = playerObjectId;
		_storeMessage = storeMessage;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_playerObjectId);
		buffer.WriteS(_storeMessage);
	}
}
