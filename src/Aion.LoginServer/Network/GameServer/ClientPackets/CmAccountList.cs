using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmAccountList : GsClientPacket
{
	public CmAccountList(byte opCode)
		: base(opCode)
	{
	}

	public IReadOnlyList<int> AccountIds { get; private set; } = Array.Empty<int>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		var count = buffer.ReadH();
		var accounts = new int[count];
		for (var i = 0; i < count; i++)
			accounts[i] = buffer.ReadD();
		AccountIds = accounts;
	}
}
