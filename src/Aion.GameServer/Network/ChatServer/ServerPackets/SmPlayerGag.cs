using Aion.Commons.Network;

namespace Aion.GameServer.Network.ChatServer.ServerPackets;

/// <summary>Java parity: network/chatserver/serverpackets/SM_CS_PLAYER_GAG(int playerId, long gagTime) (ViAl).</summary>
public sealed class SmPlayerGag : ChatServerPacket
{
	private readonly int _playerId;
	private readonly long _gagTime;

	public SmPlayerGag(int playerId, long gagTime)
	{
		_playerId = playerId;
		_gagTime = gagTime;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: network/chatserver/serverpackets/SM_CS_PLAYER_GAG.writeImpl (opcode 0x03).
		buffer.WriteC(0x03);
		buffer.WriteD(_playerId);
		buffer.WriteQ(_gagTime);
	}
}
