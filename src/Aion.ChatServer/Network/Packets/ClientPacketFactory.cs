using Aion.Commons.Network;
using Aion.ChatServer.Network.Packets.Client;

namespace Aion.ChatServer.Network.Packets;

public enum ChatClientConnectionState
{
	Connected,
	Authed,
	Disconnected,
}

public static class ClientPacketFactory
{
	public const byte CmPlayerAuth = 0x05;
	public const byte CmChannelCreate = 0x0B;
	public const byte CmChannelJoin = 0x0D;
	public const byte CmChannelRequest = 0x10;
	public const byte CmChannelLeave = 0x12;
	public const byte CmChannelMessage = 0x18;
	public const byte CmPlayerInfo = 0x2C;
	public const byte CmChatIni = 0x30;
	public const byte CmPing = 0xFF;

	public static AbstractClientPacket? Create(PacketBuffer payload, ChatClientConnectionState state)
	{
		var opCode = payload.ReadC();
		AbstractClientPacket? packet = null;

		packet = state switch
		{
			ChatClientConnectionState.Connected => opCode switch
			{
				CmChatIni => new CmChatIni(opCode),
				CmPlayerAuth => new CmPlayerAuth(opCode),
				_ => null
			},
			ChatClientConnectionState.Authed => opCode switch
			{
				CmChannelCreate => new CmChannelCreate(opCode),
				CmChannelJoin => new CmChannelJoin(opCode),
				CmChannelRequest => new CmChannelRequest(opCode),
				CmChannelLeave => new CmChannelLeave(opCode),
				CmChannelMessage => new CmChannelMessage(opCode),
				CmPlayerInfo => new CmPlayerInfo(opCode),
				CmPing => new CmPing(opCode),
				_ => null
			},
			_ => null
		};

		if (packet == null)
			return null;

		try
		{
			packet.Read(payload);
			return packet;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
