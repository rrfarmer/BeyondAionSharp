using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/tiamatStrongHold/InvincibleShabokanAI (@author Cheatkiller).
/// </summary>
[AIName("invincibleshabokan")]
public class InvincibleShabokanAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? skillTask;
    private bool isFinalBuff;

    public InvincibleShabokanAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
            StartSkillTask();
        if (!isFinalBuff && GetOwner().GetLifeStats().GetHpPercentage() <= 25)
        {
            isFinalBuff = true;
            AIActions.UseSkill(this, 20941);
        }
    }

    /// <summary>
    /// Retail's <c>BTIMERI_INDEX_3</c> (the earthquake) and <c>_2</c> (the sink).
    /// </summary>
    /// <remarks>
    /// <b>They are two rungs, not a coin flip.</b> Retail arms the earthquake at thirty seconds and
    /// re-arms it at fifty; the sink is armed at twenty and re-armed at twenty-two. This class ran one
    /// task from five seconds every thirty and tossed a coin between them, so each mechanic came at
    /// random and at about the wrong rate — the sink less than half as often as it should, and the
    /// earthquake more.
    /// </remarks>
    private static readonly TimeSpan QuakeFirst = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QuakeRepeat = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan SinkFirst = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SinkRepeat = TimeSpan.FromSeconds(22);

    /// <summary>Retail's <c>is_hp_in_boundary larger_than=16</c> on both rungs.</summary>
    private const int FloorPercent = 16;

    /// <summary>Retail's <c>total_set_to_spawn</c> and <c>valid_distance</c> on the sink.</summary>
    private const int SinkTargets = 6;
    private const float SinkReach = 100f;

    private ScheduledTask? sinkTask;

    private void StartSkillTask()
    {
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else if (GetLifeStats().GetHpPercentage() > FloorPercent)
                EarthQuakeEvent();
            return ValueTask.CompletedTask;
        }, QuakeFirst, QuakeRepeat);

        sinkTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else if (GetLifeStats().GetHpPercentage() > FloorPercent)
                SinkEvent();
            return ValueTask.CompletedTask;
        }, SinkFirst, SinkRepeat);
    }

    private void CancelTask()
    {
        if (skillTask != null && !skillTask.IsCancelled)
        {
            skillTask.Cancel(true);
        }

        if (sinkTask != null && !sinkTask.IsCancelled)
        {
            sinkTask.Cancel(true);
        }
    }

    private void EarthQuakeEvent()
    {
        Npc invisible = GetPosition().GetWorldMapInstance().GetNpc(283082); // 4.0
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20717, 55, GetOwner()).UseNoAnimationSkill();
        if (invisible == null)
        {
            Spawn(283082, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // 4.0
        }
    }

    /// <summary>
    /// Retail's sink: one on each of up to six attackers, inside a hundred metres.
    /// </summary>
    /// <remarks>
    /// <b>Six, not everybody.</b> Retail's <c>spawn_on_multi_target</c> carries
    /// <c>total_set_to_spawn=6</c> and <c>valid_distance=100</c>, taking attackers in ascending order.
    /// This class put one on <i>every</i> player it could see inside thirty metres, so a large raid took
    /// more sinks than a small one and anybody standing back took none.
    /// <para>
    /// <b>And one npc, not two.</b> Retail spawns only the sink; the sink's own pattern places its
    /// <c>SinkDMG</c> twin. Both ids run <c>SinkingSandAI</c> here, which casts and removes itself — so
    /// spawning the pair meant two casts where retail has one.
    /// </para>
    /// </remarks>
    private void SinkEvent()
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20720, 55, GetOwner()).UseNoAnimationSkill();

        foreach (Creature victim in GetAggroList().StreamValidTargets(SinkReach).Take(SinkTargets))
            Spawn(283083, victim.GetX(), victim.GetY(), victim.GetZ(), (sbyte)0);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelTask();
        GetOwner().GetEffectController().RemoveEffect(20941);
        isHome.Set(true);
    }
}
