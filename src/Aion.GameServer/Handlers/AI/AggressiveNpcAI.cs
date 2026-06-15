using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/AggressiveNpcAI (ATracer).</summary>
[AIName("aggressive")]
public class AggressiveNpcAI : GeneralNpcAI
{
    public AggressiveNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        CreatureEventHandler.OnCreatureSee(this, creature);
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        if (CanThink())
            AggroEventHandler.OnAggro(this, creature);
    }

    protected override bool HandleCreatureNeedsSupportByGuard(Creature creature)
    {
        return AggroEventHandler.OnCreatureNeedsSupportByGuard(this, creature);
    }
}
