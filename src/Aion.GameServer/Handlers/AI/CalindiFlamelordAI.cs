using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Utils;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/CalindiFlamelordAI (Cheatkiller, Yeats, Estrayl).</summary>
[AIName("IDTiamat_2_calindi_flamelord")]
public class CalindiFlamelordAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>
    /// Retail thresholds, from pattern IDTiamat_Kalrindy: four identical steps at 80/60/40/25
    /// that each cast and spawn the ground surukana, then a different finisher at 15. We ran
    /// only three of the repeats, at an invented 75/50/25, and finished at 12.
    /// See docs/retail-ai-fidelity.md.
    /// </summary>
    private readonly HpPhases hpPhases = new HpPhases(80, 60, 40, 25, 15);

    /// <summary>283132, <c>IDTiamat_Kalyndi_ShadowFire</c> — fifteen seconds, out to a hundred metres.</summary>
    private const int ShadowFlame = 283132;
    private const float ShadowFlameReach = 100f;
    private const long ShadowFlameLifeMillis = 15000L;

    /// <summary>283059, <c>IDTiamat_BurrowingWorm_BurrowDispel</c> — ten seconds on a non-tank.</summary>
    private const int DispelWorm = 283059;
    private const int DispelBandLow = 16;
    private const int DispelBandHigh = 70;
    private const long DispelHeartbeatMillis = 3000L;
    private const long DispelIntervalMillis = 22000L;
    private const long DispelWormLifeMillis = 10000L;

    private ScheduledTask? dispelTask;

    public CalindiFlamelordAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
        StopDispelWorms();
    }

    protected override void HandleDied()
    {
        StopDispelWorms();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        StopDispelWorms();
        base.HandleDespawned();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        BlazeEngraving();
        StartDispelWorms();
        hpPhases.TryEnterNextPhase(this);
    }

    /// <summary>
    /// A burrowing dispel on somebody who is <b>not</b> the tank, every twenty-two seconds while she
    /// is between 16% and 70%.
    /// </summary>
    /// <remarks>
    /// Retail-sourced; see docs/retail-ai-fidelity.md. Retail's timer 2 carries two branches: inside
    /// the band it plants the worm and waits twenty-two seconds, outside it just ticks again after
    /// three. That is why this is a three-second heartbeat that mostly does nothing rather than a
    /// twenty-two second loop — the band can be entered or left between beats, and a slow loop would
    /// miss the moment.
    /// <para>
    /// <c>ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET</c> is the mechanic. A dispel dropped on the tank
    /// is a dispel on somebody who expects it; the point is that it lands on one of the others, and
    /// which one is not predictable. 283059 was spawned by nothing anywhere.
    /// </para>
    /// </remarks>
    private void StartDispelWorms()
    {
        if (dispelTask != null)
            return;

        ScheduleDispelTick(DispelHeartbeatMillis);
    }

    /// <summary>
    /// Reschedules itself rather than running at a fixed rate, which is retail's own shape: timer 2
    /// waits twenty-two seconds when it planted a worm and three when it did not.
    /// </summary>
    /// <remarks>
    /// A fixed-rate loop at twenty-two seconds would miss the moment she enters the band, and one at
    /// three would need a clock to know when the next worm is due — and reading wall time would make
    /// the pins depend on real elapsed time rather than the harness's own.
    /// </remarks>
    private void ScheduleDispelTick(long delayMillis)
    {
        dispelTask = ThreadPoolManager.GetInstance().Schedule(
            _ =>
            {
                TickDispelWorm();
                return ValueTask.CompletedTask;
            },
            delayMillis);
    }

    private void TickDispelWorm()
    {
        if (IsDead() || !GetOwner().IsSpawned())
        {
            dispelTask = null;
            return;
        }

        int hp = GetLifeStats().GetHpPercentage();
        if (hp < DispelBandLow || hp > DispelBandHigh)
        {
            ScheduleDispelTick(DispelHeartbeatMillis);
            return;
        }

        if (GetAggroList().GetTarget(AggroTarget.RANDOM_EXCEPT_CURRENT_TARGET) is Creature victim
            && Spawn(DispelWorm, victim.GetX(), victim.GetY(), victim.GetZ(), (sbyte)0) is Npc worm)
        {
            RetireAfter(worm, DispelWormLifeMillis);
        }

        ScheduleDispelTick(DispelIntervalMillis);
    }

    private void StopDispelWorms()
    {
        if (dispelTask != null && !dispelTask.IsCancelled)
            dispelTask.Cancel(true);
        dispelTask = null;
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 80:
            case 60:
            case 40:
            case 25:
                StartHallucinatoryVictoryEvent();
                DropShadowFlame(phaseHpPercent);
                break;
            case 15:
                GetOwner().QueueSkill(20942, 1);
                break;
        }
    }

    /// <summary>
    /// One shadow flame on <b>every</b> player in the room, and more of them each rung.
    /// </summary>
    /// <remarks>
    /// Retail-sourced; see docs/retail-ai-fidelity.md. The pattern's four surkana rungs each carry a
    /// <c>spawn_on_multi_target</c> whose count and scatter climb together — one flame within five
    /// metres at 80%, two within seven at 60%, three within nine at 40%, four within ten at 25% — so
    /// the room fills faster the further the fight goes. It reaches a hundred metres, which is
    /// everyone, and each flame lasts fifteen seconds.
    /// <para>
    /// This was missing entirely: 283132 was spawned by nothing anywhere. The class placed 283133 on a
    /// skill hook instead, which is a different npc and lands once near the boss rather than once per
    /// player — the escalation and the per-player placement are both the mechanic.
    /// </para>
    /// </remarks>
    private void DropShadowFlame(int rung)
    {
        (int count, float range) = rung switch
        {
            80 => (1, 5f),
            60 => (2, 7f),
            40 => (3, 9f),
            _ => (4, 10f),
        };

        foreach (Creature target in GetAggroList().StreamValidTargets(ShadowFlameReach).ToList())
        {
            for (int i = 0; i < count; i++)
            {
                double angle = Rnd.NextFloat(360f) * Math.PI / 180.0;
                float distance = Rnd.NextFloat(range);
                Npc? flame = Spawn(
                    ShadowFlame,
                    target.GetX() + (float)(Math.Cos(angle) * distance),
                    target.GetY() + (float)(Math.Sin(angle) * distance),
                    target.GetZ(),
                    (sbyte)0) as Npc;

                if (flame != null)
                    RetireAfter(flame, ShadowFlameLifeMillis);
            }
        }
    }

    /// <summary>Retail's <c>live_time</c> on a flame; nothing else removes it.</summary>
    private static void RetireAfter(Npc npc, long millis) =>
        ThreadPoolManager.GetInstance().Schedule(
            _ =>
            {
                if (npc.IsSpawned())
                    npc.GetController().Delete();
                return ValueTask.CompletedTask;
            },
            millis);

    protected virtual void StartHallucinatoryVictoryEvent()
    {
        if (GetPosition().GetWorldMapInstance().GetNpc(730695) == null && GetPosition().GetWorldMapInstance().GetNpc(730696) == null)
            GetOwner().QueueSkill(20911, 1);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20911:
                Spawn(730695, 482.21f, 458.06f, 427.42f, (sbyte)98);
                Spawn(730696, 482.21f, 571.16f, 427.42f, (sbyte)22);
                RndSpawn(283133);
                break;
            case 20913:
                Creature target = GetRandomTarget();
                if (target != null)
                    Spawn(283131, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0);
                break;
        }
    }

    protected virtual void BlazeEngraving()
    {
        if (Rnd.Chance() < 2 && GetPosition().GetWorldMapInstance().GetNpc(283131) == null)
            GetOwner().QueueSkill(20913, 60);
    }

    protected void RndSpawn(int npcId)
    {
        for (int i = 0; i < 10; i++)
        {
            RndSpawnInRange(npcId, 5, 20);
        }
    }

    protected Creature GetRandomTarget()
    {
        return GetAggroList().GetTarget(AggroTarget.RANDOM, 50);
    }
}
