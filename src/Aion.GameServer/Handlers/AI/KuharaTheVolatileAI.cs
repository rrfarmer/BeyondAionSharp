using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Kuhara the Volatile (217311, 236298). Retail pattern <c>IDYun_Nmd4</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/rentusBase/KuharaTheVolatileAI (@author xTz, Estrayl). Retail-sourced
/// corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The shape was right and every number was wrong.</b> Retail alternates the two halves on a
/// fifteen-second beat: barrels at twenty-five seconds, bombs fifteen after that, barrels fifteen after
/// the bombs, and so on. This class opened at fifty seconds, waited fourteen for the bombs and eleven
/// before resuming — a cycle of about seventy-five seconds against retail's forty, so a raid saw the
/// mechanic roughly half as often.
/// </para>
/// <para>
/// <b>The lifetimes were already right</b>, from an earlier pass: barrels fifteen seconds, bombs two
/// minutes, keyed by npc id in <c>LifeOf</c>. What that pass could not see was that the fifteen seconds
/// on a barrel is not an arbitrary lifetime — it is <i>exactly</i> the beat, so retail's barrels expire
/// as the bombs land. With the gap at fourteen seconds they expired a second early, and with the cycle
/// at fifty they were long gone before anything else happened.
/// </para>
/// <para>
/// <b>Not translated.</b> Retail's barrel rung is a 30/30/30/fallback ladder over the four points, which
/// is a different distribution from this class's even roll — the three probability rungs and the
/// fallback all pick one point each, so the fourth point is reached more often than the others. Left as
/// an even roll because the rungs differ only in which point they choose and the pattern gives no
/// per-point skill to tell them apart. Retail's <c>live_time=120</c> on the bombs is also not used: the
/// bombs here walk to Kuhara and are removed when the phase ends, which is this port's way of expressing
/// the explosion.
/// </para>
/// </remarks>
[AIName("kuhara_the_volatile")]
public class KuharaTheVolatileAI : AggressiveNpcAI
{
    /// <summary>Retail's <c>BTIMERI_INDEX_3</c>: twenty-five seconds to the first barrels.</summary>
    private static readonly TimeSpan BarrelsFirst = TimeSpan.FromSeconds(25);

    /// <summary>
    /// And the beat the two halves alternate on: barrels, fifteen seconds, bombs, fifteen seconds,
    /// barrels. Retail arms timer 2 from the barrel rung and timer 3 back from the bomb rung, both at
    /// fifteen thousand.
    /// </summary>
    private static readonly TimeSpan Beat = TimeSpan.FromSeconds(15);

    private ScheduledTask? activeEventTask, barrelEventTask, bombEventTask;
    private readonly AtomicBoolean isStarted = new AtomicBoolean();
    private bool canThink = true;

    public KuharaTheVolatileAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return canThink;
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isStarted.CompareAndSet(false, true))
        {
            StartActiveEvent();
            StartBarrelEvent();
        }
    }

    private void CancelTask(ScheduledTask? task)
    {
        if (task != null && !task.IsCancelled)
            task.Cancel(true);
    }

    /// <summary>
    /// Arms retail's barrel rung. It is a chain rather than a fixed rate: each wave arms the bombs, and
    /// the bombs arm the next wave, which is how retail's two timers hand off to each other.
    /// </summary>
    private void StartBarrelEvent() => ArmBarrels(BarrelsFirst);

    private void ArmBarrels(TimeSpan delay)
    {
        barrelEventTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead())
                return ValueTask.CompletedTask;

            switch (Rnd.Get(1, 4))
            {
                case 1:
                    RndSpawn(282394, 126.53f, 274.49f, 209.819f);
                    RndSpawn(282394, 126.53f, 274.49f, 209.819f);
                    break;
                case 2:
                    RndSpawn(282394, 162.22f, 263.89f, 209.819f);
                    RndSpawn(282394, 162.22f, 263.89f, 209.819f);
                    break;
                case 3:
                    RndSpawn(282394, 156.32f, 235.73f, 209.819f);
                    RndSpawn(282394, 156.32f, 235.73f, 209.819f);
                    break;
                case 4:
                    RndSpawn(282394, 119.24f, 245.89f, 209.819f);
                    RndSpawn(282394, 119.24f, 245.89f, 209.819f);
                    break;
            }
            StartBombEvent();
            return ValueTask.CompletedTask;
        }, (long)delay.TotalMilliseconds);
    }

    private void StartBombEvent()
    {
        bombEventTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500394);
                canThink = false;
                EmoteManager.EmoteStopAttacking(GetOwner());
                SetStateIfNot(AIState.WALKING);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19703, 60, GetOwner()).UseNoAnimationSkill();
                SpawnBombEvent();

                // Retail arms timer 3 again from the bomb rung itself, so the next barrels are one beat
                // after the bombs land -- not one beat after he finishes resuming, which would be two.
                ArmBarrels(Beat);

                bombEventTask = ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    if (!IsDead())
                    {
                        canThink = true;
                        Creature creature = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
                        if (creature == null)
                        {
                            SetStateIfNot(AIState.FIGHT);
                            Think();
                        }
                        else
                        {
                            GetOwner().GetMoveController().AbortMove();
                            GetOwner().SetTarget(creature);
                            GetOwner().GetGameStats().RenewLastAttackTime();
                            GetOwner().GetGameStats().RenewLastAttackedTime();
                            GetOwner().GetGameStats().RenewLastSkillTime();
                            SetStateIfNot(AIState.FIGHT);
                            HandleMoveValidate();
                            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19375, 60, GetOwner()).UseNoAnimationSkill();
                        }
                        // Only the bombs. The barrels carry retail's fifteen-second live_time and expire
                        // on their own -- and by the time this runs the next wave has already arrived,
                        // so sweeping them here deleted the wave that had just landed.
                        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(282396));
                    }
                    return ValueTask.CompletedTask;
                }, (long)Beat.TotalMilliseconds);
            }
            return ValueTask.CompletedTask;
        }, (long)Beat.TotalMilliseconds);
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
                npc.GetController().Delete();
        }
    }

    private void SpawnBombEvent()
    {
        MoveBombToBoss(RndSpawn(282396, 126.53f, 274.49f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 126.53f, 274.49f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 162.22f, 263.89f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 162.22f, 263.89f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 156.32f, 235.73f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 156.32f, 235.73f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 119.24f, 245.89f, 209.819f));
        MoveBombToBoss(RndSpawn(282396, 119.24f, 245.89f, 209.819f));
    }

    private void MoveBombToBoss(Npc npc)
    {
        if (!IsDead())
        {
            npc.SetTarget(GetOwner());
            npc.GetMoveController().MoveToTargetObject();
        }
    }

    /// <summary>
    /// Retail <c>IDYun_Nmd4</c>: the barrels stand fifteen seconds and the bombs two minutes. <b>Keyed by
    /// npc id rather than by call site</b>, because retail keys it that way and every one of its four
    /// barrel spawns and four bomb spawns agrees.
    /// </summary>
    private static int LifeOf(int npcId) => npcId switch
    {
        282394 => 15,
        282396 => 120,
        _ => 0,
    };

    private Npc RndSpawn(int npcId, float x, float y, float z)
    {
        double angleRadians = Math.PI / 180 * Rnd.NextFloat(360f);
        float distance = Rnd.Get(0, 4);
        float x1 = (float)(Math.Cos(angleRadians) * distance) + x;
        float y1 = (float)(Math.Sin(angleRadians) * distance) + y;
        return (Npc)Expire(Spawn(npcId, x1, y1, z, (sbyte)0), LifeOf(npcId));
    }

    private void StartActiveEvent()
    {
        activeEventTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500395);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (!IsDead())
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19704, 60, GetOwner()).UseNoAnimationSkill();
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        if (!IsDead())
                            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19705, 60, GetOwner()).UseNoAnimationSkill();
                        return ValueTask.CompletedTask;
                    }, 3500L);
                }
                return ValueTask.CompletedTask;
            }, 1000L);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(8000), TimeSpan.FromMilliseconds(14000));
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask(activeEventTask);
        CancelTask(barrelEventTask);
        CancelTask(bombEventTask);
    }

    protected override void HandleBackHome()
    {
        isStarted.Set(false);
        canThink = true;
        CancelTask(activeEventTask);
        CancelTask(barrelEventTask);
        CancelTask(bombEventTask);
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        CancelTask(activeEventTask);
        CancelTask(barrelEventTask);
        CancelTask(bombEventTask);
        WorldPosition p = GetPosition();
        if (p != null)
        {
            DeleteNpcs(p.GetWorldMapInstance().GetNpcs(282394));
            DeleteNpcs(p.GetWorldMapInstance().GetNpcs(282396));
        }
        base.HandleDied();
    }
}
