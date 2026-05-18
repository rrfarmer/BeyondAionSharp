using Aion.Commons.Network;
using Aion.LoginServer.Network.GameServer.ClientPackets;

namespace Aion.LoginServer.Network.GameServer;

public static class GsClientPacketFactory
{
	public static GsClientPacket? Create(PacketBuffer payload, GameServerConnectionState state)
	{
		var opCode = payload.ReadC();
		GsClientPacket? packet = state switch
		{
			GameServerConnectionState.Connected => opCode == 0 ? new CmGameServerAuth(opCode) : null,
			GameServerConnectionState.Authed => opCode switch
			{
				1 => new CmAccountAuth(opCode),
				2 => new CmAccountReconnectKey(opCode),
				3 => new CmAccountDisconnected(opCode),
				4 => new CmAccountList(opCode),
				12 => new CmGameServerPong(opCode),
				_ => new UnknownGsClientPacket(opCode)
			},
			_ => null
		};

		packet?.Read(payload);
		return packet;
	}
}
