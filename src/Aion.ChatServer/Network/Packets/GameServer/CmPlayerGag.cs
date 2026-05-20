using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.GameServer;

public sealed class CmPlayerGag : GsClientPacket
{
	public CmPlayerGag(byte opCode)
		: base(opCode)
	{
	}

	public int PlayerId { get; private set; }

	public long GagTimeMillis { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		PlayerId = buffer.ReadD();
		GagTimeMillis = buffer.ReadQ();
	}
}
