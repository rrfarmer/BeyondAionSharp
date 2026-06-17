using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>Java parity: gameserver/network/loginserver/serverpackets/SM_ACCOUNT_TOLL_INFO (xTz, opcode 0x09).</summary>
public sealed class SM_ACCOUNT_TOLL_INFO : LoginServerPacket
{
	private readonly int _accountId;
	private readonly long _toll;

	public SM_ACCOUNT_TOLL_INFO(long toll, int accountId)
	{
		_accountId = accountId;
		_toll = toll;
	}

	// Java parity (writeImpl audited 1:1 vs game-server/.../loginserver/serverpackets/SM_ACCOUNT_TOLL_INFO.java): 2026-06-17
	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: gameserver/network/loginserver/serverpackets/SM_ACCOUNT_TOLL_INFO.writeImpl.
		buffer.WriteC(0x09);
		buffer.WriteD(_accountId);
		buffer.WriteQ(_toll);
	}
}
