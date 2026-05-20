using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

public sealed class SmAccountDisconnected : LoginServerPacket
{
	public SmAccountDisconnected(int accountId)
	{
		AccountId = accountId;
	}

	public int AccountId { get; }

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: gameserver/network/loginserver/serverpackets/SM_ACCOUNT_DISCONNECTED.writeImpl.
		buffer.WriteC(0x03);
		buffer.WriteD(AccountId);
	}
}
