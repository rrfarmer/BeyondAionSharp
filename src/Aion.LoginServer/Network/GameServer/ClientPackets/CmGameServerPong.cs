using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmGameServerPong : GsClientPacket
{
	public CmGameServerPong(byte opCode)
		: base(opCode)
	{
	}

	protected override void ReadPayload(PacketBuffer buffer)
	{
	}
}
