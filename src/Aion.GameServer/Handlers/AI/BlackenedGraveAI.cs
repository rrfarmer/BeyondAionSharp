using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/BlackenedGraveAI (Tibald).</summary>
[AIName("blackened_grave")]
public class BlackenedGraveAI : GeneralNpcAI
{
    public BlackenedGraveAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        Spawn(284262, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
        AIActions.DeleteOwner(this);
    }
}
