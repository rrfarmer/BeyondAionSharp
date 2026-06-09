using System.Collections.Concurrent;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Taskmanager.Tasks;

/// <summary>Java parity: taskmanager/tasks/MoveTaskManager (ATracer, Rolandas).</summary>
public class MoveTaskManager : AbstractPeriodicTaskManager
{
    private const int UPDATE_PERIOD = 200;
    private readonly ConcurrentDictionary<int, Creature> movingCreatures = new();

    private MoveTaskManager()
        : base(UPDATE_PERIOD)
    {
    }

    public void AddCreature(Creature creature)
    {
        if (!creature.IsSpawned()) // log with stack trace to find the cause
        {
            log.LogWarning(new System.NotSupportedException(), "Failed attempt to add " + creature + " to moving creatures (despawned objects cannot move)");
            return;
        }
        movingCreatures.TryAdd(creature.GetObjectId(), creature);
    }

    public bool RemoveCreature(Creature creature)
    {
        return movingCreatures.TryRemove(creature.GetObjectId(), out _);
    }

    protected override void Run()
    {
        movingCreatures.Values.AsParallel().ForAll(creature =>
        {
            if (!creature.IsSpawned()) // can despawn concurrently, while this thread is already running
            {
                if (RemoveCreature(creature)) // should have been removed via onDespawn (MoveController#abortMove())
                    log.LogWarning(creature + " was still in moving creatures list but already despawned");
                return;
            }
            creature.GetMoveController().MoveToDestination();
            if (creature.GetAi().IsDestinationReached())
            {
                RemoveCreature(creature);
                creature.GetAi().OnGeneralEvent(AiEventType.MoveArrived);
                Aion.GameServer.World.Zone.ZoneUpdateService.GetInstance().Add(creature);
            }
            else
            {
                creature.GetAi().OnGeneralEvent(AiEventType.MoveValidate);
            }
        });
    }

    public static MoveTaskManager GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    private static class SingletonHolder
    {
        internal static readonly MoveTaskManager INSTANCE = new MoveTaskManager();
    }
}
