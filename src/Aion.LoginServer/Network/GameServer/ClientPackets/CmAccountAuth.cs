using Aion.Commons.Network;
using Aion.LoginServer.Network.Aion;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmAccountAuth : GsClientPacket
{
	public CmAccountAuth(byte opCode)
		: base(opCode)
	{
	}

	public SessionKey SessionKey { get; private set; } = new(0, 0, 0, 0);

	protected override void ReadPayload(PacketBuffer buffer)
	{
		SessionKey = new SessionKey(buffer.ReadD(), buffer.ReadD(), buffer.ReadD(), buffer.ReadD());
	}
}
