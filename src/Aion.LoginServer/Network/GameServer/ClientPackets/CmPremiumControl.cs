using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmPremiumControl : GsClientPacket
{
	public CmPremiumControl(byte opCode)
		: base(opCode)
	{
	}

	public int AccountId { get; private set; }

	public int RequestId { get; private set; }

	public long RequiredCost { get; private set; }

	public byte ServerId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		AccountId = buffer.ReadD();
		RequestId = buffer.ReadD();
		RequiredCost = buffer.ReadQ();
		ServerId = buffer.ReadC();
	}
}
