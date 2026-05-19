using Aion.Commons.Network;
using Aion.LoginServer.Network.Aion.ClientPackets;

namespace Aion.LoginServer.Network.Aion;

public static class AionClientPacketFactory
{
	public static AionClientPacket? Create(PacketBuffer payload, LoginClientState state)
	{
		var opCode = payload.ReadC();
		AionClientPacket? packet = state switch
		{
			LoginClientState.Connected => opCode switch
			{
				0x07 => new CmAuthGameGuard(opCode),
				0x08 => new CmUpdateSession(opCode),
				_ => null
			},
			LoginClientState.AuthedGameGuard => opCode switch
			{
				0x00 => new CmLogin(opCode),
				_ => null
			},
			LoginClientState.AuthedLogin => opCode switch
			{
				0x05 => new CmServerList(opCode),
				0x02 => new CmPlay(opCode),
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
