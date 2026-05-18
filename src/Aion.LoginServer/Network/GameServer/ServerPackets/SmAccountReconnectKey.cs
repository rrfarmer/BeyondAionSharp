using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmAccountReconnectKey : GsServerPacket
{
	private readonly int _accountId;
	private readonly int _reconnectKey;

	public SmAccountReconnectKey(int accountId, int reconnectKey)
	{
		_accountId = accountId;
		_reconnectKey = reconnectKey;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(3);
		buffer.WriteD(_accountId);
		buffer.WriteD(_reconnectKey);
	}
}
