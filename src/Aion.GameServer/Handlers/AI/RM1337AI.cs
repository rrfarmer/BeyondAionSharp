using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// RM-1337 (217593). Retail pattern <c>IDArena_S8_Named_6</c>.
/// </summary>
/// <remarks>
/// Java parity: @author Luzien.
/// Retail pattern <c>IDArena_S8_Named_6</c> (217593). Retail-sourced corrections; see
/// docs/retail-ai-fidelity.md.
/// <para>
/// <b>Both of his rungs change pace at half health and neither did here.</b> Retail arms the fire rung
/// at thirty seconds and re-arms it at <b>sixty above half and fifty below</b>; the cast rung opens at
/// ten and re-arms at <b>fifteen above half and eighteen below</b>. This class opened the fire rung
/// immediately and repeated at a flat sixty, and ran the cast rung at a flat twenty-three, which is a
/// number retail does not have anywhere.
/// </para>
/// <para>
/// <b>And the fire itself is a fixed shape, not a roll.</b> Retail places <b>four at five metres and
/// five at fifteen</b> above half health, and below half <b>eight at five, ten at fifteen, and five
/// more on one random attacker</b> — so crossing half turns nine sparks into twenty-three and moves
/// some of them onto a player. This class rolled eight to twelve in one band, at one spread, whatever
/// his health.
/// </para>
/// </remarks>
[AIName("rm_1337")]
public class RM1337AI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    /// <summary>Retail's <c>BTIMERI_INDEX_0</c>, the fire rung: thirty seconds, then sixty or fifty.</summary>
    private static readonly TimeSpan FireFirst = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FireAboveHalf = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FireBelowHalf = TimeSpan.FromSeconds(50);

    /// <summary>Retail's <c>BTIMERI_INDEX_1</c>, the cast rung: ten seconds, then fifteen or eighteen.</summary>
    private static readonly TimeSpan CastFirst = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CastAboveHalf = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CastBelowHalf = TimeSpan.FromSeconds(18);

    /// <summary>
    /// Retail's fire, by band: near, far, and on one attacker.
    /// </summary>
    private const int SparkNpc = 282373;
    private const int NearSpread = 5;
    private const int FarSpread = 15;
    private const int NearAbove = 4;
    private const int FarAbove = 5;
    private const int NearBelow = 8;
    private const int FarBelow = 10;
    private const int OnAttackerBelow = 5;

    /// <summary>Retail's <c>valid_distance</c> on the attacker drop.</summary>
    private const float AttackerReach = 50f;

    private bool BelowHalf => GetLifeStats().GetHpPercentage() <= 50;

    private ScheduledTask task1, task2;

    public RM1337AI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500229, 2000);
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTask();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500231);
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelTask();
        base.HandleBackHome();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            // Both rungs, on entering combat. Retail arms BTIMERI_INDEX_0 and _1 together in
            // on_enter_attack_state with no health condition -- the health only decides how fast each
            // one re-arms and how much fire it drops. This class held the fire rung back until he was
            // under seventy-five per cent, which is a gate retail does not have.
            StartSkillTask1();
            StartSkillTask2();
        }
    }

    private void CancelTask()
    {
        if (task1 != null && !task1.IsCancelled)
        {
            task1.Cancel(true);
        }
        if (task2 != null && !task2.IsCancelled)
        {
            task2.Cancel(true);
        }
    }

    private void ArmCastRung(TimeSpan delay)
    {
        task1 = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (IsDead())
            {
                CancelTask();
            }
            else
            {
                if (GetOwner().GetCastingSkill() != null)
                    return ValueTask.CompletedTask;
                if (GetLifeStats().GetHpPercentage() <= 50)
                {
                    if (Rnd.NextBoolean())
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19550, 10, GetRandomTarget()).UseNoAnimationSkill();
                    }
                    else
                    {
                        Creature target = GetRandomTarget();
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19552, 10, target).UseNoAnimationSkill();
                        ThreadPoolManager.GetInstance().Schedule(ct2 =>
                        {
                            if (!IsDead())
                            {
                                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19553, 10, target).UseNoAnimationSkill();
                            }
                            return ValueTask.CompletedTask;
                        }, 4000L);
                    }
                }
                else
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19550, 10, GetRandomTarget()).UseNoAnimationSkill();
            }
            if (!IsDead())
                ArmCastRung(BelowHalf ? CastBelowHalf : CastAboveHalf);
            return ValueTask.CompletedTask;
        }, (long)delay.TotalMilliseconds);
    }

    /// <summary>Retail re-arms this rung with whichever delay matches his health at that moment.</summary>
    private void StartSkillTask1() => ArmCastRung(CastFirst);

    private void ArmFireRung(TimeSpan delay)
    {
        task2 = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (IsDead())
            {
                CancelTask();
            }
            else
            {
                GetOwner().GetController().CancelCurrentSkill(null);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500230);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19551, 10, GetTarget()).UseNoAnimationSkill();
                SpawnSparks();
            }
            if (!IsDead())
                ArmFireRung(BelowHalf ? FireBelowHalf : FireAboveHalf);
            return ValueTask.CompletedTask;
        }, (long)delay.TotalMilliseconds);
    }

    private void StartSkillTask2() => ArmFireRung(FireFirst);

    private void SpawnSparks()
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!IsDead())
            {
                // Two rings and, below half, a third drop on one attacker. SparkOfDarknessAI carries
                // retail's five-second life on all of them.
                bool wounded = BelowHalf;
                for (int i = 0; i < (wounded ? NearBelow : NearAbove); i++)
                    RndSpawnInRange(SparkNpc, NearSpread);
                for (int i = 0; i < (wounded ? FarBelow : FarAbove); i++)
                    RndSpawnInRange(SparkNpc, FarSpread);

                if (wounded && GetAggroList().GetTarget(AggroTarget.RANDOM, AttackerReach) is Creature victim)
                {
                    for (int i = 0; i < OnAttackerBelow; i++)
                        SpawnNear(SparkNpc, victim);
                }
            }
            return ValueTask.CompletedTask;
        }, 4000L);
    }

    /// <summary>Retail's <c>spawn_on_multi_target</c> lands its fire on the player, not around him.</summary>
    private void SpawnNear(int npcId, Creature victim) =>
        Spawn(npcId, victim.GetX(), victim.GetY(), victim.GetZ(), (sbyte)0);

    private Creature GetRandomTarget()
    {
        return GetAggroList().GetTarget(AggroTarget.RANDOM, 37);
    }
}
