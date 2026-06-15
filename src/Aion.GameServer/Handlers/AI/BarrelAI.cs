using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/crucibleChallenge/BarrelAI (@author xTz).</summary>
[AIName("barrel")]
public class BarrelAI : NpcAI
{
    public BarrelAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        switch (GetNpcId())
        {
            case 218560:
                RndSpawnInRange(218561, 4);
                break;
            case 217840:
                RndSpawnInRange(217841, 4);
                break;
        }
        AIActions.DeleteOwner(this);
    }
}
