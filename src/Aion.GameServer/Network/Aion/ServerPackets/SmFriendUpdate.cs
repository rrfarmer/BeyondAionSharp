using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmFriendUpdate : GameServerPacket
{
	public const int PacketOpCode = 240;

	private readonly PlayerFriend _friend;
	private readonly byte _status;
	private readonly PlayerExperienceTable? _experienceTable;

	public SmFriendUpdate(PlayerFriend friend, byte status, PlayerExperienceTable? experienceTable = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_UPDATE(int friendObjId) after FriendList.setStatus updates friend PCD.
		_friend = friend;
		_status = status;
		_experienceTable = experienceTable;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_UPDATE.writeImpl.
		buffer.WriteS(_friend.Name);
		buffer.WriteD(GetLevel());
		buffer.WriteD(ToClassId(_friend.PlayerClass));
		buffer.WriteC(ToGenderId(_friend.Gender));
		buffer.WriteD(_friend.MapId);
		buffer.WriteD(_status == 1 ? 0 : ToEpochSeconds(_friend.LastOnline));
		buffer.WriteS(_friend.Note);
		buffer.WriteC(_status);
	}

	private int GetLevel()
	{
		return Math.Max(1, _experienceTable?.GetLevelForExp(_friend.Exp) ?? 1);
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
