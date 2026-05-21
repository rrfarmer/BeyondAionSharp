using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmFriendResponse : GameServerPacket
{
	public const int PacketOpCode = 222;
	public const byte TargetAdded = 0x00;
	public const byte TargetOffline = 0x01;
	public const byte TargetAlreadyFriend = 0x02;
	public const byte TargetNotFound = 0x03;
	public const byte TargetDenied = 0x04;
	public const byte ListFull = 0x05;
	public const byte TargetRemoved = 0x06;
	public const byte TargetBlockedYou = 0x08;
	public const byte TargetDead = 0x09;
	public const byte TargetListFull = 0x0A;
	public const byte TargetOfflineSentRequest = 0x0B;
	public const byte TargetRequestedAlready = 0x0C;
	public const byte TooManyRequests = 0x0D;
	public const byte RequesterListFullCantAccept = 0x0E;
	public const byte CloseSendRequestWindow = 0x11;
	public const byte RequestDenied = 0x12;
	public const byte RequestAlreadyReceived = 0x13;

	private readonly string _playerName;
	private readonly byte _code;

	public SmFriendResponse(byte code, string playerName = "")
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_RESPONSE(String playerName, int messageType).
		_playerName = playerName;
		_code = code;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_RESPONSE.writeImpl.
		buffer.WriteS(_playerName);
		buffer.WriteC(_code);
	}
}
