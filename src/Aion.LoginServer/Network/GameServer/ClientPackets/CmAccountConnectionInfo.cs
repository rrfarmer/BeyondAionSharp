using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmAccountConnectionInfo : GsClientPacket
{
	public CmAccountConnectionInfo(byte opCode)
		: base(opCode)
	{
	}

	public int AccountId { get; private set; }

	public long Time { get; private set; }

	public string Ip { get; private set; } = string.Empty;

	public string Mac { get; private set; } = string.Empty;

	public string HddSerial { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		AccountId = buffer.ReadD();
		Time = buffer.ReadQ();
		Ip = buffer.ReadS();
		Mac = buffer.ReadS();
		HddSerial = buffer.ReadS();
	}
}
