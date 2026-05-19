using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmGameServerCharacterResponse : GsServerPacket
{
	private readonly int _accountId;

	public SmGameServerCharacterResponse(int accountId)
	{
		_accountId = accountId;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(8);
		buffer.WriteD(_accountId);
	}
}
