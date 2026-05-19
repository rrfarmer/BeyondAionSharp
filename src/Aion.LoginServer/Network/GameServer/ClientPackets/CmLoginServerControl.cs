using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmLoginServerControl : GsClientPacket
{
	public CmLoginServerControl(byte opCode)
		: base(opCode)
	{
	}

	public byte Type { get; private set; }

	public byte Param { get; private set; }

	public int AccountId { get; private set; }

	public int AdminId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		Type = buffer.ReadC();
		Param = buffer.ReadC();
		AccountId = buffer.ReadD();
		AdminId = buffer.ReadD();
	}
}
