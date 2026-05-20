using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmCharacterSelect : GameServerPacket
{
	public const int PacketOpCode = 177;
	public const int DefaultMaxWrongCount = 5;

	public SmCharacterSelect(int type, int messageType = 0, int wrongCount = 0, int maxWrongCount = DefaultMaxWrongCount)
		: base(PacketOpCode)
	{
		Type = type;
		MessageType = messageType;
		WrongCount = wrongCount;
		MaxWrongCount = maxWrongCount;
	}

	public int Type { get; }

	public int MessageType { get; }

	public int WrongCount { get; }

	public int MaxWrongCount { get; }

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_CHARACTER_SELECT.writeImpl.
		buffer.WriteC(Type);
		if (Type != 2)
			return;

		buffer.WriteH(MessageType);
		buffer.WriteC(WrongCount > 0 ? 1 : 0);
		buffer.WriteD(WrongCount);
		buffer.WriteD(MaxWrongCount);
	}
}
