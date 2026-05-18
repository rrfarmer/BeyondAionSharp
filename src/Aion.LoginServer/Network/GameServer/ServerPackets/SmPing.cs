using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmPing : GsServerPacket
{
	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(11);
	}
}
