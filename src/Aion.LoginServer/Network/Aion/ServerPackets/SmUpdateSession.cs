using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmUpdateSession : AionServerPacket
{
	private readonly SessionKey _sessionKey;

	public SmUpdateSession(SessionKey sessionKey)
		: base(0x0C)
	{
		_sessionKey = sessionKey;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteD(_sessionKey.AccountId);
		buffer.WriteD(_sessionKey.LoginOk);
		buffer.WriteC(0);
	}
}
