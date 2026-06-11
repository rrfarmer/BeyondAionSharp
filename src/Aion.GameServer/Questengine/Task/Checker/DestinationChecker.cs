using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.QuestEngine.Task.Checker;

/// <summary>Java parity: questEngine/task/checker/DestinationChecker.</summary>
public abstract class DestinationChecker
{
    protected readonly Creature follower;

    protected DestinationChecker(Creature follower)
    {
        this.follower = follower;
    }

    public Creature GetFollower()
    {
        return follower;
    }

    public abstract bool Check();
}
