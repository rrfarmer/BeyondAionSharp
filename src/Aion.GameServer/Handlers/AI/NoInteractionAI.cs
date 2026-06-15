using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/NoInteractionAI.</summary>
[AIName("no_interaction")]
public class NoInteractionAI : NpcAI
{
    public NoInteractionAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleBeforeSpawned()
    {
        base.HandleBeforeSpawned();
        GetOwner().OverrideNpcType(CreatureType.PEACE);
    }

    public override bool CanThink()
    {
        return false; // minimal thinking to just support walkers
    }

    public override void Think()
    {
        ThinkEventHandler.OnThink(this);
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        MoveEventHandler.OnMoveArrived(this);
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }
}
