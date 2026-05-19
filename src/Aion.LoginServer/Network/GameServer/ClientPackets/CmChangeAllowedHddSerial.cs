using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmChangeAllowedHddSerial : GsClientPacket
{
	public CmChangeAllowedHddSerial(byte opCode)
		: base(opCode)
	{
	}

	public int AccountId { get; private set; }

	public string HddSerial { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		AccountId = buffer.ReadD();
		HddSerial = buffer.ReadS();
	}
}
