using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.QuestEngine.Task.Checker;

/// <summary>Java parity: questEngine/task/checker/TargetDestinationChecker.</summary>
public class TargetDestinationChecker : DestinationChecker
{
    protected readonly Creature target;

    public TargetDestinationChecker(Creature follower, Creature target)
        : base(follower)
    {
        this.target = target;
    }

    public override bool Check()
    {
        return PositionUtil.IsInRange(target, follower, 20);
    }
}
