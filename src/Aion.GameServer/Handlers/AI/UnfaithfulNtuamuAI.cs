using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/brusthonin/UnfaithfulNtuamuAI (Cheatkiller, Neon).</summary>
[AIName("unfaithfulntuamu")]
public class UnfaithfulNtuamuAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50);

    public UnfaithfulNtuamuAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Retail's <c>live_time</c> on this spawn. <b>An hour is not a mechanic</b> - it is retail bounding
    /// an npc that would otherwise outlive the reason it was summoned. Ported for the same reason: the
    /// bound is cheap, and its absence is only visible on a server that has been up a long time.
    /// </summary>
    private const int SummonLife = 3600;

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        Npc ntuamu = GetOwner();
        Npc vampireQueen = (Npc)SpawnFor(214583, ntuamu.GetX(), ntuamu.GetY(), ntuamu.GetZ(), (sbyte)ntuamu.GetHeading(), SummonLife);
        vampireQueen.GetLifeStats().SetCurrentHpPercent(phaseHpPercent);
        vampireQueen.GetObserveController().Attach(new DeathObserver(_ => AIActions.ScheduleRespawn(this)));
        AIActions.DeleteOwner(this);
    }
}
