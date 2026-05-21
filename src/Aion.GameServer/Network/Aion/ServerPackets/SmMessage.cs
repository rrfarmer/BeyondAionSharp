using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMessage : GameServerPacket
{
	public const int PacketOpCode = 24;
	private const int MessageSizeHardCap = 4000;
	private const byte GoldenYellowChatType = 25;

	private readonly int _senderObjectId;
	private readonly string? _senderName;
	private readonly string _message;
	private readonly byte _chatType;

	public SmMessage(string message)
		: this(senderObjectId: 0, senderName: null, message, GoldenYellowChatType)
	{
	}

	public SmMessage(int senderObjectId, string? senderName, string message, byte chatType)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MESSAGE manual constructor.
		_senderObjectId = senderObjectId;
		_senderName = senderName;
		_message = message.Length > MessageSizeHardCap ? message[..MessageSizeHardCap] : message;
		_chatType = chatType;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: PacketSendUtility.sendMessage -> SM_MESSAGE(0, null, msg, ChatType.GOLDEN_YELLOW).
		buffer.WriteC(_chatType);
		buffer.WriteC(0);
		buffer.WriteD(_senderObjectId);
		buffer.WriteS(_senderName);
		buffer.WriteS(_message);
	}
}
