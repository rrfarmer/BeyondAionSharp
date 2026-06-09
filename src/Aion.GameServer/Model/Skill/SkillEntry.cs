namespace Aion.GameServer.Model.Skill;

/// <summary>Java parity: model/skill/SkillEntry.</summary>
public abstract class SkillEntry
{
    protected readonly int skillId;
    protected volatile int skillLevel;

    protected SkillEntry(int skillId, int skillLevel)
    {
        this.skillId = skillId;
        this.skillLevel = skillLevel;
    }

    public int GetSkillId()
    {
        return skillId;
    }

    public int GetSkillLevel()
    {
        return skillLevel;
    }

    public virtual void SetSkillLvl(int skillLevel)
    {
        this.skillLevel = skillLevel;
    }
}
