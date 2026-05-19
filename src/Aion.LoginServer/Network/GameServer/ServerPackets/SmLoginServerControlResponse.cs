using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmLoginServerControlResponse : GsServerPacket
{
	private readonly byte _type;
	private readonly byte _param;
	private readonly int _accountId;
	private readonly int _adminId;
	private readonly bool _result;

	public SmLoginServerControlResponse(byte type, byte param, int accountId, int adminId, bool result)
	{
		_type = type;
		_param = param;
		_accountId = accountId;
		_adminId = adminId;
		_result = result;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(4);
		buffer.WriteC(_type);
		buffer.WriteC(_param);
		buffer.WriteD(_accountId);
		buffer.WriteD(_adminId);
		buffer.WriteC(_result ? 1 : 0);
	}
}
