using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.GameServer;

public sealed class CmPlayerLogout : GsClientPacket
{
	public CmPlayerLogout(byte opCode)
		: base(opCode)
	{
	}

	public int PlayerId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		PlayerId = buffer.ReadD();
	}
}
