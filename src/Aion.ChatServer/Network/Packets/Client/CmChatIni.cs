using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Client;

public sealed class CmChatIni : AbstractClientPacket
{
	public CmChatIni(byte opCode)
		: base(opCode)
	{
	}

	public byte UnknownC { get; private set; }

	public ushort UnknownH { get; private set; }

	public int UnknownD1 { get; private set; }

	public int UnknownD2 { get; private set; }

	public int UnknownD3 { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		UnknownC = buffer.ReadC();
		UnknownH = buffer.ReadH();
		UnknownD1 = buffer.ReadD();
		UnknownD2 = buffer.ReadD();
		UnknownD3 = buffer.ReadD();
	}
}
