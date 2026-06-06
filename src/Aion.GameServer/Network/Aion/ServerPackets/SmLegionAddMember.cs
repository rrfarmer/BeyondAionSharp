using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Legion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionAddMember : GameServerPacket
{
	public const int PacketOpCode = 111; // Java parity: ServerPacketsOpcodes addPacketOpcode(111, SM_LEGION_ADD_MEMBER.class).

	private readonly Player _player;
	private readonly bool _isMember;
	private readonly int _gameServerId;
	private readonly int _messageId;
	private readonly string _text;

	public SmLegionAddMember(Player player, bool isMember, int gameServerId, int messageId, string? text)
		: base(PacketOpCode)
	{
		_player = player;
		_isMember = isMember;
		_gameServerId = gameServerId;
		_messageId = messageId;
		_text = text ?? string.Empty;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_ADD_MEMBER.writeImpl.
		buffer.WriteD(_player.ObjectId);
		buffer.WriteS(_player.Name);
		buffer.WriteC(Math.Max(0, LegionRanks.GetRankId(_player.LegionRank)));
		buffer.WriteC(_isMember ? 0x01 : 0x00);
		buffer.WriteC(ToClassId(_player.PlayerClass));
		buffer.WriteC(Math.Clamp(_player.Level, 0, byte.MaxValue));
		buffer.WriteD(_player.Position.WorldId);
		buffer.WriteD(_gameServerId);
		buffer.WriteD(_messageId);
		buffer.WriteS(_text);
	}

	private static int ToClassId(string playerClass)
	{
		// Java parity: model/PlayerClass.getClassId.
		return playerClass.ToUpperInvariant() switch
		{
			"GLADIATOR" => 1,
			"TEMPLAR" => 2,
			"SCOUT" => 3,
			"ASSASSIN" => 4,
			"RANGER" => 5,
			"MAGE" => 6,
			"SORCERER" => 7,
			"SPIRIT_MASTER" => 8,
			"PRIEST" => 9,
			"CLERIC" => 10,
			"CHANTER" => 11,
			"ENGINEER" => 12,
			"RIDER" => 13,
			"GUNNER" => 14,
			"ARTIST" => 15,
			"BARD" => 16,
			_ => 0,
		};
	}
}
