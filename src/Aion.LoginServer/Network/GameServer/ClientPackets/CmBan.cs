using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmBan : GsClientPacket
{
	public CmBan(byte opCode)
		: base(opCode)
	{
	}

	public byte Type { get; private set; }

	public int AccountId { get; private set; }

	public string Ip { get; private set; } = string.Empty;

	public int Time { get; private set; }

	public int AdminObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		Type = buffer.ReadC();
		AccountId = buffer.ReadD();
		Ip = buffer.ReadS();
		Time = buffer.ReadD();
		AdminObjectId = buffer.ReadD();
	}
}
