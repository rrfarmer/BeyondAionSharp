using System;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Taskmanager;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Taskmanager.Tasks;

/// <summary>Java parity: taskmanager/tasks/MovementNotifyTask (ATracer).</summary>
public class MovementNotifyTask : AbstractFIFOPeriodicTaskManager<Creature>
{
    private static class SingletonHolder
    {
        internal static readonly MovementNotifyTask INSTANCE = new MovementNotifyTask();
    }

    public static MovementNotifyTask GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    public MovementNotifyTask()
        : base(500)
    {
    }

    protected override void CallTask(Creature creature)
    {
        if (creature.IsDead())
            return;

        // In Reshanta:
        // max_move_broadcast_count is 200 and
        // min_move_broadcast_range is 75, as in client WorldId.xml
        int limit = creature.GetWorldId() == 400010000 ? 200 : int.MaxValue;
        foreach (var o in creature.GetKnownList().Stream()
            .Where(o => o.Get() is Npc)
            .Take(limit))
        {
            NotifyCreatureMoved((Npc)o.Get(), creature);
        }
    }

    internal void NotifyCreatureMoved(Npc npc, Creature creature)
    {
        try
        {
            if (npc.GetAi().GetState() == AIState.DIED || npc.IsDead())
            {
                if (npc.GetAi().IsLogging())
                {
                    Aion.GameServer.Ai.AILogger.Moveinfo(npc, "WARN: NPC died but still in knownlist");
                }
                return;
            }
            npc.GetAi().OnCreatureEvent(AiEventType.CreatureMoved, creature);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not notify " + npc + " about movement of " + creature);
        }
    }

    protected override string GetCalledMethodName()
    {
        return "notifyOnMove()";
    }
}
