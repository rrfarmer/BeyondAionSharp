using Aion.Commons.Network;
using Aion.GameServer.Configuration;

namespace Aion.GameServer.Network.ChatServer.ServerPackets;

public sealed class SmChatServerAuth : ChatServerPacket
{
	private readonly GameServerOptions _options;

	public SmChatServerAuth(GameServerOptions options)
	{
		_options = options;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: gameserver/network/chatserver/serverpackets/SM_CS_AUTH.writeImpl.
		buffer.WriteC(0x00);
		buffer.WriteC(_options.Network.GameServerId);
		buffer.WriteS(_options.Network.ChatPassword);
	}
}
