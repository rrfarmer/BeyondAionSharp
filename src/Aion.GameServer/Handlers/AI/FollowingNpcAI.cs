using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/FollowingNpcAI (ATracer).</summary>
[AIName("following")]
public class FollowingNpcAI : GeneralNpcAI
{
    public FollowingNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleFollowMe(Creature creature)
    {
        FollowEventHandler.Follow(this, creature);
    }

    protected override bool CanHandleEvent(AiEventType eventType)
    {
        switch (eventType)
        {
            case AiEventType.CREATURE_MOVED:
                return GetState() == AIState.FOLLOWING;
            case AiEventType.DIALOG_START:
            case AiEventType.DIALOG_FINISH:
                return GetState() == AIState.FOLLOWING || base.CanHandleEvent(eventType);
        }
        return base.CanHandleEvent(eventType);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature.Equals(GetOwner().GetTarget()))
        {
            FollowEventHandler.CreatureMoved(this, creature);
        }
        else if (GetOwner().GetTarget() == null)
        {
            FollowEventHandler.StopFollow(this, creature);
        }
    }

    protected override void HandleStopFollowMe(Creature creature)
    {
        FollowEventHandler.StopFollow(this, creature);
    }
}
