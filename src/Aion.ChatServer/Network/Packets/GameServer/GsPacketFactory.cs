using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.GameServer;

public enum GameServerConnectionState
{
	Connected,
	Authed,
	Disconnected,
}

public static class GsPacketFactory
{
	public const byte CmChatServerAuth = 0x00;
	public const byte CmPlayerAuth = 0x01;
	public const byte CmPlayerLogout = 0x02;
	public const byte CmPlayerGag = 0x03;

	public const byte SmGameServerAuthResponse = 0x00;
	public const byte SmPlayerAuthResponse = 0x01;

	public static GsClientPacket? Create(PacketBuffer payload, GameServerConnectionState state)
	{
		var opCode = payload.ReadC();
		GsClientPacket? packet = null;

		packet = state switch
		{
			GameServerConnectionState.Connected => opCode == CmChatServerAuth ? new CmChatServerAuth(opCode) : null,
			GameServerConnectionState.Authed => opCode switch
			{
				CmPlayerAuth => new CmPlayerAuth(opCode),
				CmPlayerLogout => new CmPlayerLogout(opCode),
				CmPlayerGag => new CmPlayerGag(opCode),
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
