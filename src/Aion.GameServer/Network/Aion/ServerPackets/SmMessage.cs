using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMessage : GameServerPacket
{
	public const int PacketOpCode = 24;
	private const int MessageSizeHardCap = 4000;
	private const byte GoldenYellowChatType = 25;
	private const byte ShoutChatType = 3;

	private readonly int _senderObjectId;
	private readonly string? _senderName;
	private readonly string _message;
	private readonly byte _chatType;
	private readonly byte _senderRace;
	private readonly float _senderX;
	private readonly float _senderY;
	private readonly float _senderZ;
	private readonly bool _writeSenderCoordinates;

	public SmMessage(string message)
		: this(senderObjectId: 0, senderName: null, message, GoldenYellowChatType)
	{
	}

	public SmMessage(int senderObjectId, string? senderName, string message, byte chatType)
		: this(senderObjectId, senderName, message, chatType, senderRace: 0, senderX: 0, senderY: 0, senderZ: 0, writeSenderCoordinates: false)
	{
	}

	public SmMessage(Player sender, string message, byte chatType)
		: this(
			sender.ObjectId,
			sender.Name,
			message,
			chatType,
			GetSenderRaceFilter(sender),
			sender.GetPosition().X,
			sender.GetPosition().Y,
			sender.GetPosition().Z,
			chatType == ShoutChatType)
	{
		// Java parity: network/aion/serverpackets/SM_MESSAGE(Player, message, chatType).
	}

	private SmMessage(
		int senderObjectId,
		string? senderName,
		string message,
		byte chatType,
		byte senderRace,
		float senderX,
		float senderY,
		float senderZ,
		bool writeSenderCoordinates)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MESSAGE manual constructor.
		_senderObjectId = senderObjectId;
		_senderName = senderName;
		_message = message.Length > MessageSizeHardCap ? message[..MessageSizeHardCap] : message;
		_chatType = chatType;
		_senderRace = senderRace;
		_senderX = senderX;
		_senderY = senderY;
		_senderZ = senderZ;
		_writeSenderCoordinates = writeSenderCoordinates;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MESSAGE.writeImpl.
		buffer.WriteC(_chatType);
		buffer.WriteC(_senderRace);
		buffer.WriteD(_senderObjectId);
		buffer.WriteS(_senderName);
		buffer.WriteS(_message);
		if (_writeSenderCoordinates)
		{
			buffer.WriteF(_senderX);
			buffer.WriteF(_senderY);
			buffer.WriteF(_senderZ);
		}
	}

	private static byte GetSenderRaceFilter(Player sender)
	{
		// Java parity: player race filter is Race.raceId + 1 when cross-faction speech is disabled.
		if (sender.AccessLevel > 0)
			return 0;
		if (string.Equals(sender.Race.ToString(), "ELYOS", StringComparison.OrdinalIgnoreCase))
			return 1;
		if (string.Equals(sender.Race.ToString(), "ASMODIANS", StringComparison.OrdinalIgnoreCase))
			return 2;
		return 0;
	}
}
