using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>Java parity: controllers/movement/SummonMoveController (ATracer).</summary>
public class SummonMoveController : PlayableMoveController<Summon>
{
    public SummonMoveController(Summon owner)
        : base(owner)
    {
    }

    public void MoveToTargetObject()
    {
    }
}
