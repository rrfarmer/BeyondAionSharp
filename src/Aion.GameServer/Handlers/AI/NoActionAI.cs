using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/NoActionAI (ATracer).</summary>
[AIName("noaction")]
public class NoActionAI : NpcAI
{
    public NoActionAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        switch (GetOwner().GetObjectTemplate().GetTribe())
        { // hp regen for training dummies
            case TribeClass.TARGETBASFELT_DF1:
            case TribeClass.TARGETBASFELT2_DF1:
            case TribeClass.DUMMY:
            case TribeClass.DUMMY2:
            case TribeClass.LF5_DUMMY1:
            case TribeClass.LF5_DUMMY2:
            case TribeClass.DF5_DUMMY1:
            case TribeClass.DF5_DUMMY2:
                GetOwner().GetController().LoseAggro(true);
                break;
        }
    }
}
