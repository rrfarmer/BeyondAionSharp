using Aion.Commons.Network;
using Aion.GameServer.Model.Legion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionUpdateMember : GameServerPacket
{
	public const int PacketOpCode = 113; // Java parity: ServerPacketsOpcodes addPacketOpcode(113, SM_LEGION_UPDATE_MEMBER.class).

	private readonly LegionMemberSnapshot _member;
	private readonly int _level;
	private readonly int _gameServerId;
	private readonly int _messageId;
	private readonly string? _text;

	public SmLegionUpdateMember(
		LegionMemberSnapshot member,
		int level,
		int gameServerId,
		int messageId = 0,
		string? text = null)
		: base(PacketOpCode)
	{
		_member = member;
		_level = level;
		_gameServerId = gameServerId;
		_messageId = messageId;
		_text = text;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_MEMBER.writeImpl.
		buffer.WriteD(_member.PlayerObjectId);
		buffer.WriteC(Math.Max(0, LegionRanks.GetRankId(_member.Rank)));
		buffer.WriteC(ToClassId(_member.PlayerClass));
		buffer.WriteC(Math.Clamp(_level, 0, byte.MaxValue));
		buffer.WriteD(_member.WorldId);
		buffer.WriteC(_member.IsOnline ? 1 : 0);
		buffer.WriteD(_member.IsOnline ? 0 : ToUnixSeconds(_member.LastOnline));
		buffer.WriteD(_gameServerId);
		buffer.WriteD(_messageId);
		buffer.WriteS(_text ?? string.Empty);
	}

	private static int ToUnixSeconds(DateTime? value)
	{
		if (value == null)
			return 0;

		var offset = value.Value.Kind == DateTimeKind.Unspecified
			? new DateTimeOffset(value.Value)
			: new DateTimeOffset(value.Value.ToUniversalTime(), TimeSpan.Zero);
		var seconds = offset.ToUnixTimeSeconds();
		return seconds < int.MinValue ? int.MinValue : seconds > int.MaxValue ? int.MaxValue : (int)seconds;
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
