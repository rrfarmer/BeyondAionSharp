using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmRequestKickAccount : GsServerPacket
{
	private readonly int _accountId;
	private readonly bool _notifyDoubleLogin;

	public SmRequestKickAccount(int accountId, bool notifyDoubleLogin)
	{
		_accountId = accountId;
		_notifyDoubleLogin = notifyDoubleLogin;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(2);
		buffer.WriteD(_accountId);
		buffer.WriteC(_notifyDoubleLogin ? 1 : 0);
	}
}
