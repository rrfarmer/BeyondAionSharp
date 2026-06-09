using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Model.Skill;

/// <summary>Java parity: model/skill/NpcSkillEntry (ATracer, nrg, Yeats). Abstract NpcSkillEntry : SkillEntry; setLastTimeUsed→System.currentTimeMillis()→DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(). NpcSkillConditionTemplate/NpcSkillTemplate red-tolerated.</summary>
public abstract class NpcSkillEntry : SkillEntry
{
    protected long lastTimeUsed = 0;

    protected NpcSkillEntry(int skillId, int skillLevel) : base(skillId, skillLevel)
    {
    }

    public abstract bool IsReady(int hpPercentage, long fightingTimeInMSec);

    public abstract bool ChanceReady();

    public abstract bool HpReady(int hpPercentage);

    public abstract bool TimeReady(long fightingTimeInMSec);

    public abstract bool HasCooldown();

    public abstract bool HasPostSpawnCondition();

    public long GetLastTimeUsed()
    {
        return lastTimeUsed;
    }

    public void SetLastTimeUsed()
    {
        this.lastTimeUsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public abstract int GetPriority();

    public abstract bool ConditionReady(Creature creature);

    public abstract NpcSkillConditionTemplate GetConditionTemplate();

    public abstract bool HasCondition();

    public abstract int GetNextSkillTime();

    public abstract bool HasChain();

    public abstract int GetNextChainId();

    public abstract int GetChainId();

    public abstract bool CanUseNextChain(Npc owner);

    public abstract NpcSkillTemplate GetTemplate();

    public abstract void FireOnEndCastEvents(Npc npc);
}
