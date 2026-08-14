using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/azoturanFortress/BetrayerIcaronixAI (Antraxx, Neon).</summary>
[AIName("betrayericaronix")]
public class BetrayerIcaronixAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>
    /// Retail threshold, from pattern ND2_AhC_1: the swap is one latched step at 75%, where a
    /// shout is followed by spawning the successor at his own position and despawning himself.
    /// Structurally what we already did, at 50%. See docs/retail-ai-fidelity.md.
    /// </summary>
    private readonly HpPhases hpPhases = new HpPhases(75);

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
        // Carried over from the swap threshold so the successor picks up where this form left
        // off, as it did when both were 50. Retail's pattern sets no HP on the spawn at all,
        // so this figure is ours, not the spec's.
        icaronixTheBetrayer.GetLifeStats().SetCurrentHpPercent(75);
        AIActions.DeleteOwner(this);
    }
}
