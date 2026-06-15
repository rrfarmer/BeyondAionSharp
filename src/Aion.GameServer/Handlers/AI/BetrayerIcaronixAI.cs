using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/azoturanFortress/BetrayerIcaronixAI (Antraxx, Neon).</summary>
[AIName("betrayericaronix")]
public class BetrayerIcaronixAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50);

    public BetrayerIcaronixAI(Npc owner)
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
        Npc icaronixTheBetrayer = (Npc)Spawn(214599, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)GetPosition().GetHeading());
        icaronixTheBetrayer.GetLifeStats().SetCurrentHpPercent(50);
        AIActions.DeleteOwner(this);
    }
}
