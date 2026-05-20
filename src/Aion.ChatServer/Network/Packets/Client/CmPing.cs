using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Client;

public sealed class CmPing : AbstractClientPacket
{
	public CmPing(byte opCode)
		: base(opCode)
	{
	}

	public byte UnknownC { get; private set; }

	public ushort UnknownH { get; private set; }

	public byte[] Padding { get; private set; } = [];

	protected override void ReadPayload(PacketBuffer buffer)
	{
		UnknownC = buffer.ReadC();
		UnknownH = buffer.ReadH();
		Padding = buffer.ReadB(16);
	}
}
