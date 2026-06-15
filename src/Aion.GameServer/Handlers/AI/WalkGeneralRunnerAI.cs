using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/walkers/WalkGeneralRunnerAI (Rolandas).</summary>
[AIName("generalrunner")]
public class WalkGeneralRunnerAI : GeneralNpcAI
{
    public WalkGeneralRunnerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        GetOwner().SetState(CreatureState.WEAPON_EQUIPPED);
    }
}
