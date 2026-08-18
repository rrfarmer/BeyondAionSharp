using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/abyssal_splinter/RukrilAI (Ritsu, Luzien).</summary>
[AIName("rukril")]
public class RukrilAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(95);
    private ScheduledTask skillTask;

    public RukrilAI(Npc owner)
        : base(owner)
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

    private void StartSkillTask()
    {
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelTask();
            }
            else
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19266, 55, GetOwner()).UseNoAnimationSkill();
                // Retail IDAbRe_Core_Named*: these carry live_time 70 and the branch re-arms at 70
                // seconds, so a set expires exactly as the next is due. They had no lifetime here, and
                // the "only if none are standing" test below is what that cost -- with adds that never
                // die it never passes again, so the cycle ran once per fight.
                //
                // The test is dropped rather than left inert. Pazuzu's equivalent was harmless once its
                // adds expired, because its life (71) is a second under its cycle (72); here the two are
                // both 70, so a check landing on the same tick as the expiry could still see them
                // standing and skip. Retail spawns unconditionally.
                SpawnFor(281907, 447.3828f, 675.9968f, 433.95636f, (sbyte)19, SummonLife);
                SpawnFor(281907, 441.49512f, 680.38495f, 434.02753f, (sbyte)19, SummonLife);
            }

            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(5000), System.TimeSpan.FromMilliseconds(70000));
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
