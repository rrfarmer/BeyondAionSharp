using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Summons;

/// <summary>Java parity: model/summons/SkillOrder (Rolandas, Neon).</summary>
public class SkillOrder
{
    private readonly int skillId;
    private readonly int skillLvl;
    private readonly Creature target;
    private readonly int hate;
    private readonly bool release;

    public SkillOrder(int skillId, int skillLvl, Creature target, int hate, bool release)
    {
        this.skillId = skillId;
        this.skillLvl = skillLvl;
        this.target = target;
        // since no summon skills generate any hate and the order cast itself has hate values which are never broadcast, we assume that
        // the summon should broadcast that hate instead
        this.hate = hate;
        this.release = release;
    }

    public int GetSkillId()
    {
        return skillId;
    }

    public int GetSkillLevel()
    {
        return skillLvl;
    }

    public Creature GetTarget()
    {
        return target;
    }

    public int GetHate()
    {
        return hate;
    }

    public bool IsRelease()
    {
        return release;
    }
}
