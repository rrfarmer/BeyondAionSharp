using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

public sealed class SmAccountConnectionInfo : LoginServerPacket
{
	public SmAccountConnectionInfo(int accountId, long time, string ip, string mac, string hddSerial)
	{
		AccountId = accountId;
		Time = time;
		Ip = ip;
		Mac = mac;
		HddSerial = hddSerial;
	}

	public int AccountId { get; }

	public long Time { get; }

	public string Ip { get; }

	public string Mac { get; }

	public string HddSerial { get; }

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: gameserver/network/loginserver/serverpackets/SM_ACCOUNT_CONNECTION_INFO.writeImpl.
		buffer.WriteC(0x07);
		buffer.WriteD(AccountId);
		buffer.WriteQ(Time);
		buffer.WriteS(Ip);
		buffer.WriteS(Mac);
		buffer.WriteS(HddSerial);
	}
}
