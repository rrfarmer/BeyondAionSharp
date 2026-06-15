using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Rolandas
/// </summary>
[AIName("polorserin")]
public class PolorSerinAI : WalkGeneralRunnerAI
{
    public PolorSerinAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleMoveArrived()
    {
        bool adultsNear = GetKnownList().Stream()
            .Any(obj => obj.Get() is Npc npc && (npc.GetNpcId() == 203129 || npc.GetNpcId() == 203132) && IsInRange(npc, GetOwner().GetAggroRange()));
        if (adultsNear)
        {
            MoveEventHandler.OnMoveArrived(this);
            GetOwner().UnsetState(CreatureState.WEAPON_EQUIPPED);
        }
        else
        {
            base.HandleMoveArrived();
        }
    }
}
