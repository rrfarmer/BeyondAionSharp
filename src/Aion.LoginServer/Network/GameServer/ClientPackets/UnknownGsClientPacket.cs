using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class UnknownGsClientPacket : GsClientPacket
{
	public UnknownGsClientPacket(byte opCode)
		: base(opCode)
	{
	}

	public byte[] Payload { get; private set; } = Array.Empty<byte>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		Payload = buffer.ReadRemaining();
	}
}
