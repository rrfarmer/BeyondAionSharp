using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.QuestEngine.Task.Checker;

/// <summary>Java parity: questEngine/task/checker/CoordinateDestinationChecker (ATracer, Neon).</summary>
public class CoordinateDestinationChecker : DestinationChecker
{
    protected readonly float x;
    protected readonly float y;
    protected readonly float z;

    public CoordinateDestinationChecker(Creature follower, float x, float y, float z) : base(follower)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public override bool Check()
    {
        return PositionUtil.IsInRange(follower, x, y, z, 20);
    }
}
