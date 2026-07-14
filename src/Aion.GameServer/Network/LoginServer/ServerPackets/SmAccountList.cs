using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>
/// Java parity: gameserver/network/loginserver/serverpackets/SM_ACCOUNT_LIST.
/// Sent immediately after bridge authentication so the Login Server can rebuild its online-account view.
/// </summary>
public sealed class SmAccountList : LoginServerPacket
{
	private readonly int[] _accountIds;

	public SmAccountList(IEnumerable<int> accountIds)
	{
		_accountIds = accountIds.ToArray();
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(0x04);
		buffer.WriteD(_accountIds.Length);
		foreach (var accountId in _accountIds)
			buffer.WriteD(accountId);
	}
}
