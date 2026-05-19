using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmHddBanControl : GsClientPacket
{
	public CmHddBanControl(byte opCode)
		: base(opCode)
	{
	}

	public byte Type { get; private set; }

	public string Address { get; private set; } = string.Empty;

	public long Time { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		Type = buffer.ReadC();
		Address = buffer.ReadS();
		Time = buffer.ReadQ();
	}
}
