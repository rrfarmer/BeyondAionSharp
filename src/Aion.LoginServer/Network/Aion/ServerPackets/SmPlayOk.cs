using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmPlayOk : AionServerPacket
{
	private readonly SessionKey _sessionKey;
	private readonly byte _serverId;

	public SmPlayOk(SessionKey sessionKey, byte serverId)
		: base(0x07)
	{
		_sessionKey = sessionKey;
		_serverId = serverId;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteD(_sessionKey.PlayOk1);
		buffer.WriteD(_sessionKey.PlayOk2);
		buffer.WriteC(_serverId);
		buffer.WriteB(new byte[0x0E]);
	}
}
