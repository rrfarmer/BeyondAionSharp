using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/AbyssGuardSimpleAI (Rolandas, Neon).</summary>
[AIName("simple_abyssguard")]
public class AbyssGuardSimpleAI : AggressiveNpcAI
{
    public AbyssGuardSimpleAI(Npc owner)
        : base(owner)
    {
    }

    protected override bool CanHandleEvent(AiEventType eventType)
    {
        switch (eventType)
        {
            case AiEventType.CREATURE_MOVED:
                return GetState() != AIState.FIGHT;
        }
        return base.CanHandleEvent(eventType);
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        if (creature is Npc)
            CheckAggro((Npc)creature); // custom checkAggro for npc vs npc
        else
            base.HandleCreatureSee(creature); // calls CreatureEventHandler.checkAggro
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Npc)
            CheckAggro((Npc)creature); // custom checkAggro for npc vs npc
        else
            base.HandleCreatureMoved(creature); // calls CreatureEventHandler.checkAggro
    }

    protected override bool HandleCreatureNeedsSupportByGuard(Creature creature)
    {
        return false;
    }

    private void CheckAggro(Npc npc)
    {
        if (IsInState(AIState.FIGHT))
            return;

        if (IsInState(AIState.RETURNING))
            return;

        Npc owner = GetOwner();
        if (npc.IsDead() || !owner.CanSee(npc))
            return;

        if (!owner.IsEnemy(npc) || npc.GetLevel() < 2)
            return;

        // ignore npcs which are under attack
        if (npc.GetTarget() != null)
            return;

        if (!owner.GetPosition().IsMapRegionActive())
            return;

        if (PositionUtil.IsInRange(owner, npc, owner.GetAggroRange()) && GeoService.GetInstance().CanSee(owner, npc))
            OnCreatureEvent(AiEventType.CREATURE_AGGRO, npc);
    }
}
