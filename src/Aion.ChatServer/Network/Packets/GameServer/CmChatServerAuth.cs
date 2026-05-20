using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.GameServer;

public sealed class CmChatServerAuth : GsClientPacket
{
	public CmChatServerAuth(byte opCode)
		: base(opCode)
	{
	}

	public byte GameServerId { get; private set; }

	public string Password { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		GameServerId = buffer.ReadC();
		Password = buffer.ReadS();
	}
}
