using System;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/NpcGameStats.</summary>
public class NpcGameStats : CreatureGameStats<Npc>
{
    private long lastAttackTime = 0;
    private long lastAttackedTime = 0;
    private long nextAttackTime = 0;
    private long lastSkillTime = 0;
    private int nextSkillDelay = 0;
    private Aion.GameServer.Model.Skill.NpcSkillEntry lastSkill = null;
    private long fightStartingTime = 0;
    private long nextGeoZUpdate;
    private long lastChangeTarget = 0;

    public NpcGameStats(Npc owner)
        : base(owner)
    {
    }

    // Java parity helper: Math.round(float) = floor(x+0.5).
    private static int JRound(float a) => (int)Math.Floor(a + 0.5f);

    private static long CurrentTimeMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    protected override void OnStatsChange(Effect effect)
    {
        base.OnStatsChange(effect);
        CheckSpeedStats();
    }

    public override StatsTemplate GetStatsTemplate()
    {
        return owner.GetObjectTemplate().GetStatsTemplate();
    }

    public override Stat2 GetStat(StatEnum statEnum, Stat2 stat, params CalculationType[] calculationTypes)
    {
        Stat2 s = base.GetStat(statEnum, stat, calculationTypes);
        owner.GetAi().ModifyOwnerStat(s);
        return s;
    }

    public override Stat2 GetAttackSpeed()
    {
        return GetStat(StatEnum.ATTACK_SPEED, owner.GetObjectTemplate().GetAttackSpeed());
    }

    public override Stat2 GetMovementSpeed()
    {
        Stat2 newSpeedStat;
        if (owner.IsInState(CreatureState.WeaponEquipped))
        {
            float speed;
            if (owner.GetWalkerGroup() != null)
                speed = GetStatsTemplate().GetGroupRunSpeedFight();
            else
                speed = GetStatsTemplate().GetRunSpeedFight();
            newSpeedStat = GetStat(StatEnum.SPEED, JRound(speed * 1000));
        }
        else if (owner.IsInState(CreatureState.WalkMode))
        {
            float speed;
            if (owner.GetWalkerGroup() != null && owner.GetAi().GetSubState() == Aion.GameServer.Ai.AiSubState.WalkPath)
                speed = GetStatsTemplate().GetGroupWalkSpeed();
            else
                speed = GetStatsTemplate().GetWalkSpeed();
            newSpeedStat = GetStat(StatEnum.SPEED, JRound(speed * 1000));
        }
        else
        {
            float multiplier = owner.IsFlying() ? 1.3f : 1.0f;
            newSpeedStat = GetStat(StatEnum.SPEED, JRound(GetStatsTemplate().GetRunSpeed() * multiplier * 1000));
        }
        return newSpeedStat;
    }

    public override Stat2 GetAttackRange()
    {
        return GetStat(StatEnum.ATTACK_RANGE, owner.GetObjectTemplate().GetAttackRange() * 1000);
    }

    public override Stat2 GetHpRegenRate()
    {
        int divider = 2;
        if (owner.GetAbyssNpcType() != AbyssNpcType.NONE)
            divider = 4; // Abyss type related NPCs restore their health by 25%
        return GetStat(StatEnum.REGEN_HP, GetStatsTemplate().GetMaxHp() / divider);
    }

    public override Stat2 GetMpRegenRate()
    {
        throw new InvalidOperationException("No mp regen for NPC");
    }

    public int GetCastSpeed()
    {
        return owner.GetObjectTemplate().GetCastSpeed();
    }

    public int GetLastAttackTimeDelta()
    {
        return JRound((CurrentTimeMillis() - lastAttackTime) / 1000f);
    }

    public int GetLastAttackedTimeDelta()
    {
        return JRound((CurrentTimeMillis() - lastAttackedTime) / 1000f);
    }

    public void RenewLastAttackTime()
    {
        this.lastAttackTime = CurrentTimeMillis();
    }

    public void RenewLastAttackedTime()
    {
        this.lastAttackedTime = CurrentTimeMillis();
    }

    public bool IsNextAttackScheduled()
    {
        return nextAttackTime - CurrentTimeMillis() > 50;
    }

    public void SetFightStartingTime()
    {
        this.fightStartingTime = CurrentTimeMillis();
    }

    public long GetFightStartingTime()
    {
        return this.fightStartingTime;
    }

    public void SetNextAttackTime(long nextAttackTime)
    {
        this.nextAttackTime = nextAttackTime;
    }

    public int GetNextAttackInterval()
    {
        long attackDelay = CurrentTimeMillis() - lastAttackTime;
        int attackSpeed = GetAttackSpeed().GetCurrent();
        if (attackSpeed == 0)
        {
            attackSpeed = 2000;
        }
        if (owner.GetAi().IsLogging())
        {
            AILogger.Info(owner.GetAi(), "adelay = " + attackDelay + " aspeed = " + attackSpeed);
        }
        int nextAttack = 0;
        if (lastAttackTime == 0 && !owner.GetMoveController().IsInMove() && owner.GetTarget() is Creature
            && PositionUtil.IsInAttackRange(owner, (Creature)owner.GetTarget(), GetAttackRange().GetCurrent() / 1000f))
        {
            nextAttack = 750;
        }
        if (attackDelay < attackSpeed)
        {
            nextAttack = (int)(attackSpeed - attackDelay);
        }
        return nextAttack;
    }

    public void RenewLastSkillTime()
    {
        this.lastSkillTime = CurrentTimeMillis();
    }

    public void RenewLastChangeTargetTime()
    {
        this.lastChangeTarget = CurrentTimeMillis();
    }

    public int GetLastSkillTimeDelta()
    {
        return JRound((CurrentTimeMillis() - lastSkillTime) / 1000f);
    }

    public int GetLastChangeTargetTimeDelta()
    {
        return JRound((CurrentTimeMillis() - lastChangeTarget) / 1000f);
    }

    public long GetLastSkillTime()
    {
        return lastSkillTime;
    }

    public bool CanUseNextSkill()
    {
        return nextSkillDelay == 0 || CurrentTimeMillis() >= lastSkillTime + nextSkillDelay;
    }

    public void SetNextSkillDelay(int nextSkillDelay)
    {
        if (nextSkillDelay == -1) // xml skills without specific times in templates
            this.nextSkillDelay = Rnd.Get(3000, 9000);
        else
            this.nextSkillDelay = nextSkillDelay;
    }

    public void SetLastSkill(Aion.GameServer.Model.Skill.NpcSkillEntry lastSkill)
    {
        this.lastSkill = lastSkill;
    }

    public Aion.GameServer.Model.Skill.NpcSkillEntry GetLastSkill()
    {
        return lastSkill;
    }

    public long GetNextGeoZUpdate()
    {
        return nextGeoZUpdate;
    }

    public void SetNextGeoZUpdate(long nextGeoZUpdate)
    {
        this.nextGeoZUpdate = nextGeoZUpdate;
    }

    public void ResetFightStats()
    {
        lastAttackTime = 0;
        lastAttackedTime = 0;
        lastChangeTarget = 0;
        fightStartingTime = 0;
        nextAttackTime = 0;
        lastSkillTime = 0;
        nextSkillDelay = 0;
    }

    public int GetInitialSkillDelay()
    {
        return owner.GetAi().ModifyInitialSkillDelay(Rnd.Get(GetAttackSpeed().GetCurrent(), 3 * GetAttackSpeed().GetCurrent()));
    }
}
