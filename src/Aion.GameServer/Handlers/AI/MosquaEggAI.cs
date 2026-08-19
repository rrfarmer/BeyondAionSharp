using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tallocsHollow/MosquaEggAI (@author xTz, Sykra).</summary>
/// <summary>
/// The mosqua egg (282006). Retail pattern <c>Elim_NeutflyEgg</c>.
/// </summary>
/// <remarks>
/// Retail-sourced corrections; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>It hatches when somebody sees it, not on a clock.</b> Retail's only rung is <c>on_see_user</c>,
/// flag-guarded so it fires once: cast, put a hatchling on itself, and despawn. This class hatched
/// seventeen seconds after spawning whatever anyone did, so an egg nobody went near still opened and an
/// egg walked straight past opened late.
/// </para>
/// <para>
/// <b>And it hatched the wrong npc.</b> Retail names <c>BIDElim_NeutWorkmanflySummon_51_n</c>, which is
/// <b>282082</b>; this class spawned <b>217132</b>, which is <c>IDElim_2F_NeutQeen_Summon_51_An</c> —
/// the queen's summon, a different npc that the instance also places from its own spawn table. Both are
/// called "spawned supraklaw", which is what let it pass.
/// </para>
/// <para>
/// The hatchling carries retail's eighteen-second <c>live_time</c>, which it had none of.
/// </para>
/// </remarks>
[AIName("mosquaegg")]
public class MosquaEggAI : AggressiveNpcAI
{
    private ScheduledTask supraklawSpawnTask;

    public MosquaEggAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary><c>BIDElim_NeutWorkmanflySummon_51_n</c>, and retail's <c>live_time</c> for it.</summary>
    private const int Hatchling = 282082;
    private const int HatchlingLife = 18;

    /// <summary>Retail's flag var: the egg opens once and only once.</summary>
    private readonly AtomicBoolean hatched = new AtomicBoolean();

    protected override void HandleCreatureSee(Creature creature)
    {
        base.HandleCreatureSee(creature);
        TryHatch(creature);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        base.HandleCreatureMoved(creature);
        TryHatch(creature);
    }

    /// <summary>Retail's <c>on_see_user</c>: a player comes near and the egg opens on them.</summary>
    private void TryHatch(Creature creature)
    {
        if (creature is not Player || creature.IsDead())
            return;
        if (!hatched.CompareAndSet(false, true))
            return;

        SpawnFor(Hatchling, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading(), HatchlingLife);
        GetOwner().GetController().Delete();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelSpawnTask();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelSpawnTask();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.REWARD_LOOT:
                return false;
            default:
                return base.Ask(question);
        }
    }

    private void CancelSpawnTask()
    {
        if (supraklawSpawnTask != null && !supraklawSpawnTask.IsDone())
        {
            supraklawSpawnTask.Cancel(true);
            supraklawSpawnTask = null;
        }
    }
}
