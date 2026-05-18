using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmAccountBanned2 : AionServerPacket
{
	public SmAccountBanned2()
		: base(0x09)
	{
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
	}
}
