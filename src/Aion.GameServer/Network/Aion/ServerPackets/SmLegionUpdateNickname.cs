using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionUpdateNickname : GameServerPacket
{
	public const int PacketOpCode = 11; // Java parity: ServerPacketsOpcodes addPacketOpcode(11, SM_LEGION_UPDATE_NICKNAME.class).

	private readonly int _playerObjectId;
	private readonly string _nickname;

	public SmLegionUpdateNickname(int playerObjectId, string nickname)
		: base(PacketOpCode)
	{
		_playerObjectId = playerObjectId;
		_nickname = nickname;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_NICKNAME.writeImpl.
		buffer.WriteD(_playerObjectId);
		buffer.WriteS(_nickname);
	}
}
