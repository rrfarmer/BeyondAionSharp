using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmFriendList : GameServerPacket
{
	public const int PacketOpCode = 132;

	private readonly IReadOnlyList<PlayerFriend> _friends;
	private readonly PlayerExperienceTable? _experienceTable;

	public SmFriendList(IReadOnlyList<PlayerFriend> friends, PlayerExperienceTable? experienceTable = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_LIST.
		_friends = friends;
		_experienceTable = experienceTable;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_LIST.writeImpl.
		buffer.WriteH((-_friends.Count) & 0xffff);
		buffer.WriteC(0);
		foreach (var friend in _friends)
		{
			var isOnline = friend.IsOnline;
			buffer.WriteD(friend.ObjectId);
			buffer.WriteS(friend.Name);
			buffer.WriteD(GetLevel(friend.Exp));
			buffer.WriteD(ToClassId(friend.PlayerClass));
			buffer.WriteC(ToGenderId(friend.Gender));
			buffer.WriteD(friend.MapId);
			buffer.WriteD(isOnline ? 0 : ToEpochSeconds(friend.LastOnline));
			buffer.WriteS(friend.Note);
			buffer.WriteC(isOnline ? 1 : 0);
			buffer.WriteD(0);
			buffer.WriteC(0);
			buffer.WriteS(friend.Memo);
		}
	}

	private int GetLevel(long exp)
	{
		return Math.Max(1, _experienceTable?.GetLevelForExp(exp) ?? 1);
	}

	private static int ToClassId(string playerClass)
	{
		return playerClass.ToUpperInvariant() switch
		{
			"WARRIOR" => 0,
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

	private static int ToGenderId(string gender)
	{
		return string.Equals(gender, "FEMALE", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
	}

	private static int ToEpochSeconds(DateTime? value)
	{
		if (!value.HasValue)
			return 0;
		var local = DateTime.SpecifyKind(value.Value, DateTimeKind.Local);
		return checked((int)new DateTimeOffset(local).ToUnixTimeSeconds());
	}
}
