using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>
/// Java parity: gameserver/network/loginserver/serverpackets/SM_ACCOUNT_RECONNECT_KEY (@author -Nemesiss-).
/// Sent by GameServer when a player requests a fast reconnect; LoginServer responds with the reconnect key.
/// super(0x02) -> WriteC(0x02) opcode prefix (matches the C# GS-bridge WritePayload opcode convention).
/// </summary>
public sealed class SmAccountReconnectKey : LoginServerPacket
{
	private readonly int _accountId;

	public SmAccountReconnectKey(int accountId)
	{
		_accountId = accountId;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: SM_ACCOUNT_RECONNECT_KEY.writeImpl -> writeD(accountId).
		buffer.WriteC(0x02);
		buffer.WriteD(_accountId);
	}
}
