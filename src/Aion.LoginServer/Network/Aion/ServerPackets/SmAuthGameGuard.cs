using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmAuthGameGuard : AionServerPacket
{
	private readonly int _sessionId;

	public SmAuthGameGuard(int sessionId)
		: base(0x0B)
	{
		_sessionId = sessionId;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteD(_sessionId);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0xCD5000);
		buffer.WriteD(0);
		buffer.WriteD(0x0B << 24);
		buffer.WriteD(_sessionId ^ 0xCD5000);
		buffer.WriteB(new byte[3]);
	}
}
