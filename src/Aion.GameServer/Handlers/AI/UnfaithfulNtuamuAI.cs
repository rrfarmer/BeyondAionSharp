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

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        Npc ntuamu = GetOwner();
        Npc vampireQueen = (Npc)Spawn(214583, ntuamu.GetX(), ntuamu.GetY(), ntuamu.GetZ(), (sbyte)ntuamu.GetHeading());
        vampireQueen.GetLifeStats().SetCurrentHpPercent(phaseHpPercent);
        vampireQueen.GetObserveController().Attach(new DeathObserver(_ => AIActions.ScheduleRespawn(this)));
        AIActions.DeleteOwner(this);
    }
}
