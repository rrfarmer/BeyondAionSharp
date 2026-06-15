using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// recieve only 1 dmg with each attack(handled by super) Aggro the whole room on attack.
/// Java parity: ai/instance/dredgion/SurkanaAI (Luzien).
/// </summary>
[AIName("surkana")]
public class SurkanaAI : OneDmgNoActionAI
{
    public SurkanaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        // roomaggro
        CheckForSupport(creature);
    }

    private void CheckForSupport(Creature creature)
    {
        GetKnownList().ForEachNpc(npc =>
        {
            if (!npc.IsDead() && IsInRange(npc, 25))
                npc.GetAi().OnCreatureEvent(AiEventType.CREATURE_AGGRO, creature);
        });
    }
}
