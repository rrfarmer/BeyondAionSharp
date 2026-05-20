using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Client;

public sealed class CmPlayerInfo : AbstractClientPacket
{
	public CmPlayerInfo(byte opCode)
		: base(opCode)
	{
	}

	public int ClassId { get; private set; }

	public int Level { get; private set; }

	public byte[] UnknownBytes { get; private set; } = [];

	protected override void ReadPayload(PacketBuffer buffer)
	{
		buffer.ReadC();
		buffer.ReadH();
		ClassId = buffer.ReadC();
		buffer.ReadD();
		Level = buffer.ReadD();
		UnknownBytes = buffer.ReadB(135);
	}
}
