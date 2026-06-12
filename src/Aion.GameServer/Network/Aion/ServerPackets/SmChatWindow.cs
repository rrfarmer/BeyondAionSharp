using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmChatWindow : GameServerPacket
{
	public const int PacketOpCode = 99;
	private readonly Player _target;
	private readonly bool _isGroup;
	private readonly PlayerExperienceTable? _experienceTable;

	public SmChatWindow(Player target, bool isGroup, PlayerExperienceTable? experienceTable = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_CHAT_WINDOW(Player target, boolean isGroup).
		_target = target;
		_isGroup = isGroup;
		_experienceTable = experienceTable;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_CHAT_WINDOW.writeImpl baseline no-team branches.
		if (_isGroup)
		{
			buffer.WriteC(4);
			buffer.WriteS(_target.Name);
			buffer.WriteD(0);
			buffer.WriteC(ToClassId(_target.PlayerClass.ToString()));
			buffer.WriteC(GetLevel());
			buffer.WriteC(0);
			return;
		}

		buffer.WriteC(1);
		buffer.WriteS(_target.Name);
		buffer.WriteS(_target.LegionName);
		buffer.WriteC(GetLevel());
		buffer.WriteH(ToClassId(_target.PlayerClass.ToString()));
		buffer.WriteS(_target.Note);
		buffer.WriteD(1);
		buffer.WriteC(_target.AccountMembership);
	}

	private int GetLevel()
	{
		return Math.Max(1, _experienceTable?.GetLevelForExp(_target.Exp) ?? 1);
	}

	private static int ToClassId(string playerClass)
	{
		// Java parity: model/PlayerClass enum ids.
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
			"GUNNER" => 13,
			"RIDER" => 14,
			"ARTIST" => 15,
			"BARD" => 16,
			_ => 0,
		};
	}
}
