using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Skill;

/// <summary>Java parity: model/skill/PlayerSkillEntry extends SkillEntry implements Persistable.</summary>
public class PlayerSkillEntry : SkillEntry, IPersistable
{
    private int skillType; // 0 normal skill, 1 stigma skill, 3 linked stigma skill
    private volatile int currentXp; // for crafting skills
    private IPersistable.PersistentState persistentState;

    public PlayerSkillEntry(Aion.GameServer.Model.GameObjects.Player.Player player, int skillId, int skillLvl, IPersistable.PersistentState persistentState)
        : this(skillId, skillLvl, 0, persistentState)
    {
        List<Aion.GameServer.SkillEngine.Model.SkillLearnTemplate> learnTemplates = Aion.GameServer.Dataholders.DataManager.SKILL_TREE_DATA.GetTemplatesForSkill(skillId, player.GetPlayerClass(), player.GetRace());
        if (learnTemplates.Count == 0)
            skillType = Aion.GameServer.Dataholders.DataManager.SKILL_DATA.GetSkillTemplate(skillId).GetStigmaType() == Aion.GameServer.SkillEngine.Model.StigmaType.NONE ? 0 : 1; // no way to tell if linked stigma
        else
        {
            foreach (Aion.GameServer.SkillEngine.Model.SkillLearnTemplate template in learnTemplates)
            {
                if (template.IsStigma())
                {
                    skillType = template.IsLinkedStigma() ? 3 : 1;
                    break;
                }
            }
        }
    }

    public PlayerSkillEntry(int skillId, int skillLvl, int skillType, IPersistable.PersistentState persistentState)
        : base(skillId, skillLvl)
    {
        this.skillType = skillType;
        this.persistentState = persistentState;
    }

    public bool IsStigmaSkill()
    {
        return skillType > 0;
    }

    public bool IsNormalStigmaSkill()
    {
        return IsStigmaSkill() && !IsLinkedStigmaSkill();
    }

    public bool IsLinkedStigmaSkill()
    {
        return skillType >= 3;
    }

    public bool IsNormalSkill()
    {
        return !IsStigmaSkill() && skillId < 30000;
    }

    public bool IsNormalOrStigmaSkill()
    {
        return skillId < 30000;
    }

    public bool IsTappingSkill()
    {
        return skillId >= 30001 && skillId <= 30003;
    }

    public bool IsCraftingSkill()
    {
        return skillId >= 40001 && skillId <= 40010 && !IsMorphSkill();
    }

    public bool IsMorphSkill()
    {
        return skillId == 40009;
    }

    public bool IsProfessionSkill()
    {
        return skillId >= 30000 && skillId < 50000; // 50000 or greater are actions etc.
    }

    /// <summary>
    /// For profession skills, these values are also needed in SM_SKILL_REMOVE to be able to remove the skill from list.
    /// </summary>
    public int GetProfessionFlag()
    {
        if (IsTappingSkill() || IsMorphSkill())
            return 1; // not sure for morph
        if (IsCraftingSkill())
            return GetCurrentXp(); // not implemented in DB
        return 0;
    }

    public int GetFlag()
    {
        return IsNormalSkill() ? GetDateLearned() : 0;
    }

    public int GetDateLearned()
    {
        return (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000); // not implemented in DB
    }

    public int GetSkillType()
    {
        return skillType;
    }

    public override void SetSkillLvl(int skillLevel)
    {
        base.SetSkillLvl(skillLevel);
        if (GetPersistentState() != IPersistable.PersistentState.NOACTION)
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>
    /// Controls the max point number for the professions skill bar.
    /// Tapping: 0=99,1=199,2=299,3=399,4=499. Crafting: 0=99,1=199,2=299,3=399,4=449,5=499,6=549.
    /// </summary>
    public int GetProfessionSkillBarSize()
    {
        if (!IsProfessionSkill())
            return 0;
        int size = skillLevel / 100;
        if (IsCraftingSkill() && skillLevel >= 450)
            size += (skillLevel - 350) / 100; // above 400 points, the crafting max points increase by 50 instead of 100
        return IsTappingSkill() ? Math.Min(size, 4) : size; // limit tapping bar size to 4 (499) to prevent black bar above 500 points
    }

    public int GetCurrentXp()
    {
        return currentXp;
    }

    public void SetCurrentXp(int currentXp)
    {
        this.currentXp = currentXp;
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.DELETED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    this.persistentState = IPersistable.PersistentState.NOACTION;
                else
                    this.persistentState = IPersistable.PersistentState.DELETED;
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (this.persistentState != IPersistable.PersistentState.NEW)
                    this.persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
                break;
            case IPersistable.PersistentState.NOACTION:
                break;
            default:
                this.persistentState = persistentState;
                break;
        }
    }
}
