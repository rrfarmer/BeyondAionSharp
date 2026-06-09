using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Taskmanager.Tasks;

/// <summary>Java parity: taskmanager/tasks/PlayerMoveTaskManager (ATracer).</summary>
public class PlayerMoveTaskManager : AbstractPeriodicTaskManager
{
    private readonly ConcurrentDictionary<int, Creature> movingPlayers = new();

    private PlayerMoveTaskManager()
        : base(200)
    {
    }

    public void AddPlayer(Creature player)
    {
        movingPlayers[player.GetObjectId()] = player;
    }

    public void RemovePlayer(Creature player)
    {
        movingPlayers.TryRemove(player.GetObjectId(), out _);
    }

    protected override void Run()
    {
        foreach (Creature player in movingPlayers.Values)
        {
            if (player.IsSpawned())
                player.GetMoveController().MoveToDestination();
            else
                RemovePlayer(player);
        }
    }

    public static PlayerMoveTaskManager GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    private static class SingletonHolder
    {
        internal static readonly PlayerMoveTaskManager INSTANCE = new PlayerMoveTaskManager();
    }
}
