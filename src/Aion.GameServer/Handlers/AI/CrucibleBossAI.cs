using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/CrucibleBossAI.
/// Base for the Empyrean Crucible bosses, which cast nothing at random: every skill sits on a fight timer or an HP threshold. The fight clock
/// starts on the first hit and resets when the boss leaves the fight; every threshold fires once per fight.
/// </summary>
public abstract class CrucibleBossAI : AggressiveNpcAI
{
    // Java ConcurrentHashMap.newKeySet() -> the keys of a concurrent dictionary; TryAdd is its add-if-absent.
    private readonly ConcurrentDictionary<int, byte> passedThresholds = new();
    private ScheduledTask? tickTask;
    private int fightSeconds;

    public CrucibleBossAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        lock (this)
        {
            if (tickTask != null || IsDead())
                return;
            fightSeconds = 0;
            passedThresholds.Clear();
            tickTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
            {
                Tick();
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000));
        }
        OnFightStart();
    }

    private void Tick()
    {
        if (IsDead())
            return;
        OnFightTick(++fightSeconds, GetLifeStats().GetHpPercentage());
    }

    public bool IsInFight()
    {
        lock (this)
        {
            return tickTask != null;
        }
    }

    /// <summary>Called once when the fight starts.</summary>
    protected virtual void OnFightStart()
    {
    }

    /// <summary>Called every second of the fight.</summary>
    protected abstract void OnFightTick(int seconds, int hpPercentage);

    /// <summary>Called when the boss leaves the fight, dies or despawns.</summary>
    protected virtual void OnFightEnd()
    {
    }

    /// <summary>True once per fight, as soon as the HP fall to the given percentage.</summary>
    protected bool Passed(int hpPercentage)
    {
        return GetLifeStats().GetHpPercentage() <= hpPercentage && passedThresholds.TryAdd(hpPercentage, 0);
    }

    /// <summary>True at second <paramref name="first"/> of the fight and every <paramref name="interval"/> seconds after it.</summary>
    protected static bool Every(int seconds, int first, int interval)
    {
        return seconds >= first && (seconds - first) % interval == 0;
    }

    protected void Cast(int skillId, int level)
    {
        GetOwner().QueueSkill(skillId, level);
    }

    protected void Cast(int skillId, int level, NpcSkillTargetAttribute target)
    {
        GetOwner().QueueSkill(skillId, level, 0, target);
    }

    /// <summary>Casts a skill on the boss itself right away, also outside the fight.</summary>
    protected void CastOnSelf(int skillId, int level)
    {
        Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skillId, level, GetOwner());
        if (skill != null)
            skill.UseNoAnimationSkill();
    }

    protected void Say(int msgId)
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), msgId);
    }

    protected Creature RandomAttacker()
    {
        return GetAggroList().GetTarget(AggroTarget.RANDOM, 50);
    }

    protected Creature Attacker(AggroTarget target)
    {
        return GetAggroList().GetTarget(target, 50);
    }

    protected Creature WeakestAttacker()
    {
        return GetAggroList().StreamValidTargets(50).OrderBy(c => c.GetLifeStats().GetHpPercentage()).FirstOrDefault();
    }

    /// <summary>Makes the given creature the most hated one, so the next skills aimed at the target hit it.</summary>
    protected void SwitchTarget(Creature creature)
    {
        if (creature != null)
            GetAggroList().AddHate(creature, 10000);
    }

    private void StopFight()
    {
        lock (this)
        {
            if (tickTask == null)
                return;
            tickTask.Cancel();
            tickTask = null;
        }
        OnFightEnd();
    }

    protected override void HandleNotAtHome()
    {
        StopFight();
        base.HandleNotAtHome();
    }

    protected override void HandleBackHome()
    {
        StopFight();
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        StopFight();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        StopFight();
        base.HandleDespawned();
    }
}
