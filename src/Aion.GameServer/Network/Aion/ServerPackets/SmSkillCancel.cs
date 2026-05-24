using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSkillCancel : GameServerPacket
{
	public const int PacketOpCode = 42;

	private readonly int _creatureObjectId;
	private readonly int _skillId;

	public SmSkillCancel(Player creature, int skillId)
		: this(creature.ObjectId, skillId)
	{
		// Java parity: network/aion/serverpackets/SM_SKILL_CANCEL(Creature, int).
	}

	public SmSkillCancel(int creatureObjectId, int skillId)
		: base(PacketOpCode)
	{
		_creatureObjectId = creatureObjectId;
		_skillId = skillId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_SKILL_CANCEL.writeImpl writes creature object id then skill id.
		buffer.WriteD(_creatureObjectId);
		buffer.WriteH(_skillId);
	}
}
