using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmAccountBanned : AionServerPacket
{
	public SmAccountBanned()
		: base(0x02)
	{
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
	}
}
