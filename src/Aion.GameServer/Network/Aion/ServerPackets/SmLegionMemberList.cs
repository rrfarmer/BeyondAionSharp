using Aion.Commons.Network;
using Aion.GameServer.Model.Legion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionMemberList : GameServerPacket
{
	public const int PacketOpCode = 157; // Java parity: ServerPacketsOpcodes addPacketOpcode(157, SM_LEGION_MEMBERLIST.class).

	private readonly IReadOnlyList<LegionMemberListEntry> _members;
	private readonly bool _isFirst;
	private readonly bool _isLast;
	private readonly int _gameServerId;

	public SmLegionMemberList(
		IReadOnlyList<LegionMemberListEntry> members,
		bool isFirst,
		bool isLast,
		int gameServerId)
		: base(PacketOpCode)
	{
		_members = members;
		_isFirst = isFirst;
		_isLast = isLast;
		_gameServerId = gameServerId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_MEMBERLIST.writeImpl.
		var count = _members.Count;
		buffer.WriteC(_isFirst ? 1 : 0);
		buffer.WriteH(_isLast ? -count : count);
		foreach (var member in _members)
			WriteLegionMember(buffer, member);
	}

	private void WriteLegionMember(PacketBuffer buffer, LegionMemberListEntry member)
	{
		buffer.WriteD(member.PlayerObjectId);
		buffer.WriteS(member.Name);
		buffer.WriteC(ToClassId(member.PlayerClass));
		buffer.WriteD(member.Level);
		buffer.WriteC(Math.Max(0, LegionRanks.GetRankId(member.Rank)));
		buffer.WriteD(member.WorldId);
		buffer.WriteC(member.IsOnline ? 1 : 0);
		buffer.WriteS(member.SelfIntro);
		buffer.WriteS(member.Nickname);
		buffer.WriteD(member.IsOnline ? 0 : ToUnixSeconds(member.LastOnline));
		buffer.WriteD(member.HouseAddressId);
		buffer.WriteD(member.HouseDoorStateId);
		buffer.WriteD(_gameServerId);
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

public sealed record LegionMemberListEntry(
	int PlayerObjectId,
	string Name,
	string PlayerClass,
	int Level,
	string Rank,
	int WorldId,
	bool IsOnline,
	string SelfIntro,
	string Nickname,
	DateTime? LastOnline = null,
	int HouseAddressId = 0,
	int HouseDoorStateId = 0);
