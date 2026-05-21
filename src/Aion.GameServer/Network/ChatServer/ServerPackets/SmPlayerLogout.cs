using Aion.Commons.Network;

namespace Aion.GameServer.Network.ChatServer.ServerPackets;

public sealed class SmPlayerLogout : ChatServerPacket
{
	private readonly int _playerId;

	public SmPlayerLogout(int playerId)
	{
		// Java parity: network/chatserver/serverpackets/SM_CS_PLAYER_LOGOUT(Player).
		_playerId = playerId;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: network/chatserver/serverpackets/SM_CS_PLAYER_LOGOUT.writeImpl.
		buffer.WriteC(0x02);
		buffer.WriteD(_playerId);
	}
}
