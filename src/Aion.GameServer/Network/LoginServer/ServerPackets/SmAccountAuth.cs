using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

public sealed class SmAccountAuth : LoginServerPacket
{
	private readonly int _accountId;
	private readonly int _loginOk;
	private readonly int _playOk1;
	private readonly int _playOk2;

	public SmAccountAuth(int accountId, int loginOk, int playOk1, int playOk2)
	{
		_accountId = accountId;
		_loginOk = loginOk;
		_playOk1 = playOk1;
		_playOk2 = playOk2;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: gameserver/network/loginserver/serverpackets/SM_ACCOUNT_AUTH.writeImpl.
		buffer.WriteC(0x01);
		buffer.WriteD(_accountId);
		buffer.WriteD(_loginOk);
		buffer.WriteD(_playOk1);
		buffer.WriteD(_playOk2);
	}
}
