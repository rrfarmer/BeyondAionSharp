using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmBanResponse : GsServerPacket
{
	private readonly byte _type;
	private readonly int _accountId;
	private readonly string _ip;
	private readonly int _time;
	private readonly int _adminObjectId;
	private readonly bool _result;

	public SmBanResponse(byte type, int accountId, string ip, int time, int adminObjectId, bool result)
	{
		_type = type;
		_accountId = accountId;
		_ip = ip;
		_time = time;
		_adminObjectId = adminObjectId;
		_result = result;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(5);
		buffer.WriteC(_type);
		buffer.WriteD(_accountId);
		buffer.WriteS(_ip);
		buffer.WriteD(_time);
		buffer.WriteD(_adminObjectId);
		buffer.WriteC(_result ? 1 : 0);
	}
}
