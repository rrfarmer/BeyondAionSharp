using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SKILL_REMOVE (xTz, Neon). Removes a skill from the client list (skillId + level/professionFlag + type). PlayerSkillEntry red-tolerated.</summary>
public class SM_SKILL_REMOVE : AionServerPacket
{
    private int skillId;
    private int skillLevel;
    private int skillType;

    public SM_SKILL_REMOVE(PlayerSkillEntry skill)
    {
        this.skillId = skill.GetSkillId();
        this.skillLevel = skill.IsProfessionSkill() ? skill.GetProfessionFlag() : skill.GetSkillLevel();
        this.skillType = skill.GetSkillType();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(skillId);
        WriteC(skillLevel); // for professions, the getProfessionFlag() sent in SM_SKILL_LIST value is relevant, otherwise client won't remove it...
        WriteC(skillType);
    }
}
