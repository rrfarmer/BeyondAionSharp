using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Ritsu, Luzien
/// </summary>
[AIName("ebonsoul")]
public class EbonsoulAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(95);
    private ScheduledTask skillTask;

    public EbonsoulAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        StartSkillTask();
    }

    /// <summary>
    /// Retail's <c>live_time</c> on the summon this class calls, against a branch timer of the same
    /// seventy seconds.
    /// </summary>
    /// <remarks>
    /// <b>Retail scatters these within fifteen metres of the caster</b> (<c>SPAWN_LOCATION_MY_POINT</c>,
    /// <c>spawn_range=15</c>) where this class uses two fixed marks. That divergence predates this change
    /// and is left alone: it is a placement question, not a lifetime one.
    /// </remarks>
    private const int SummonLife = 70;

    /// <summary>
    /// Retail's <c>BTIMERI_INDEX_1</c>: <b>fifty</b> seconds to the first pair, seventy between.
    /// </summary>
    /// <remarks>
    /// The seventy was already right and the fifty was five — so the first pair arrived forty-five
    /// seconds early, which is most of a first phase. The two numbers are not the same thing: seventy is
    /// the cycle, and it matches the summons' own lifetime so a set expires as the next is due; fifty is
    /// just how long the raid gets before any of it starts.
    /// </remarks>
    private static readonly TimeSpan SummonFirst = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan SummonRepeat = TimeSpan.FromSeconds(70);

    /// <summary>
    /// <c>IDAbRe_Core_Sum_Dark_Die</c> — the giant retail leaves at a fixed point when Ebonsoul dies.
    /// </summary>
    /// <remarks>
    /// Sixty seconds, and it announces itself: its own pattern broadcasts <c>11111</c> at fifty metres on
    /// waking and <c>11112</c> on leaving, with a system message between. <b>Nothing in this port placed
    /// it</b>, so his death was silent and left nothing behind.
    /// </remarks>
    private const int DeathGiant = 282012;
    private const int DeathGiantLife = 60;
    private const float DeathGiantX = 448.99f;
    private const float DeathGiantY = 694.32f;
    private const float DeathGiantZ = 433.06f;

    private void StartSkillTask()
    {
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTask();
            else
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19159, 55, GetOwner()).UseNoAnimationSkill();
                // Retail IDAbRe_Core_Named*: these carry live_time 70 and the branch re-arms at 70
                // seconds, so a set expires exactly as the next is due. They had no lifetime here, and
                // the "only if none are standing" test below is what that cost -- with adds that never
                // die it never passes again, so the cycle ran once per fight.
                //
                // The test is dropped rather than left inert. Pazuzu's equivalent was harmless once its
                // adds expired, because its life (71) is a second under its cycle (72); here the two are
                // both 70, so a check landing on the same tick as the expiry could still see them
                // standing and skip. Retail spawns unconditionally.
                SpawnFor(281908, 462.47913f, 707.4807f, 433.78372f, (sbyte)93, SummonLife);
                SpawnFor(281908, 456.09427f, 707.4807f, 433.78372f, (sbyte)93, SummonLife);
            }
            return ValueTask.CompletedTask;
        }, SummonFirst, SummonRepeat);
    }

    private void CancelTask()
    {
        if (skillTask != null && !skillTask.IsCancelled)
        {
            skillTask.Cancel(true);
        }
    }

    protected override void HandleDied()
    {
        // Retail's on_die: a giant of darkness at a fixed point for a minute. Placed before base, which
        // clears his position -- retail's branch runs while he is still standing there.
        SpawnFor(DeathGiant, DeathGiantX, DeathGiantY, DeathGiantZ, (sbyte)0, DeathGiantLife);

        base.HandleDied();
        CancelTask();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelTask();
        hpPhases.Reset();
        GetEffectController().RemoveEffect(19266);
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
