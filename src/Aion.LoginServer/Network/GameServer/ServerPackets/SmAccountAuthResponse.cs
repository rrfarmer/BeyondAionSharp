using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmAccountAuthResponse : GsServerPacket
{
	private readonly int _accountId;
	private readonly bool _ok;
	private readonly string _accountName;
	private readonly long _creationDate;
	private readonly long _accumulatedOnlineTime;
	private readonly long _accumulatedRestTime;
	private readonly byte _accessLevel;
	private readonly byte _membership;
	private readonly string _allowedHddSerial;

	public SmAccountAuthResponse(
		int accountId,
		bool ok,
		string accountName = "",
		long creationDate = 0,
		long accumulatedOnlineTime = 0,
		long accumulatedRestTime = 0,
		byte accessLevel = 0,
		byte membership = 0,
		string allowedHddSerial = "")
	{
		_accountId = accountId;
		_ok = ok;
		_accountName = accountName;
		_creationDate = creationDate;
		_accumulatedOnlineTime = accumulatedOnlineTime;
		_accumulatedRestTime = accumulatedRestTime;
		_accessLevel = accessLevel;
		_membership = membership;
		_allowedHddSerial = allowedHddSerial;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(1);
		buffer.WriteD(_accountId);
		buffer.WriteC(_ok ? 1 : 0);
		if (!_ok)
			return;

		buffer.WriteS(_accountName);
		buffer.WriteQ(_creationDate);
		buffer.WriteQ(_accumulatedOnlineTime);
		buffer.WriteQ(_accumulatedRestTime);
		buffer.WriteC(_accessLevel);
		buffer.WriteC(_membership);
		buffer.WriteS(_allowedHddSerial);
	}
}
