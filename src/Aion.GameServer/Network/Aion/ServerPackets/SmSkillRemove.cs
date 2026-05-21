using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSkillRemove : GameServerPacket
{
	public const int PacketOpCode = 45;

	private readonly PlayerSkill _skill;

	public SmSkillRemove(PlayerSkill skill)
		: base(PacketOpCode)
	{
		_skill = skill;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SKILL_REMOVE.writeImpl.
		buffer.WriteH(_skill.SkillId);
		buffer.WriteC(GetRemovedSkillLevel(_skill));
		buffer.WriteC(_skill.SkillType);
	}

	private static int GetRemovedSkillLevel(PlayerSkill skill)
	{
		// Java parity: model/skill/PlayerSkillEntry.getProfessionFlag for SM_SKILL_REMOVE.
		if (!skill.IsProfessionSkill)
			return skill.SkillLevel;
		if (skill.IsTappingSkill || skill.IsMorphSkill)
			return 1;
		return skill.IsCraftingSkill ? skill.CurrentXp : 0;
	}
}
