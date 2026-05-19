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
				5 => new CmLoginServerControl(opCode),
				6 => new CmBan(opCode),
				7 => new CmAccountConnectionInfo(opCode),
				8 => new CmGameServerCharacter(opCode),
				9 => new CmAccountTollInfo(opCode),
				10 => new CmMacBanControl(opCode),
				11 => new CmPremiumControl(opCode),
				12 => new CmGameServerPong(opCode),
				13 => new CmPlayerTransferControl(opCode),
				14 => new CmHddBanControl(opCode),
				15 => new CmChangeAllowedHddSerial(opCode),
				_ => new UnknownGsClientPacket(opCode)
			},
			_ => null
		};

		packet?.Read(payload);
		return packet;
	}
}
