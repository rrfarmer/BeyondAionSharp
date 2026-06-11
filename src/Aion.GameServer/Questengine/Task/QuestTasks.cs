using System;
using System.Threading.Tasks;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.QuestEngine.Task.Checker;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.QuestEngine.Task;

/// <summary>Java parity: questEngine/task/QuestTasks (ATracer). Future&lt;?&gt;→IScheduledFuture; scheduleAtFixedRate(task,1000,1000); IllegalArgumentException→ArgumentException; ThreadPoolManager red-tolerated.</summary>
public class QuestTasks
{
    /// <summary>Schedule new following checker task</summary>
    public static IScheduledFuture NewFollowingToTargetCheckTask(QuestEnv env, Npc npc, Npc target)
    {
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRate(new FollowingNpcCheckTask(env, new TargetDestinationChecker(npc, target)), 1000, 1000);
    }

    /// <summary>Schedule new following checker task</summary>
    public static IScheduledFuture NewFollowingToTargetCheckTask(QuestEnv env, Npc npc, int npcTargetId)
    {
        SpawnSearchResult searchResult = DataManager.SPAWNS_DATA.GetFirstSpawnByNpcId(npc.GetWorldId(), npcTargetId);
        if (searchResult == null)
        {
            throw new ArgumentException("Supplied npc doesn't exist: " + npcTargetId);
        }
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRate(
            new FollowingNpcCheckTask(env, new CoordinateDestinationChecker(npc, searchResult.GetSpot().GetX(), searchResult.GetSpot().GetY(), searchResult
                .GetSpot().GetZ())), 1000, 1000);
    }

    /// <summary>Schedule new following checker task</summary>
    public static IScheduledFuture NewFollowingToTargetCheckTask(QuestEnv env, Npc npc, float x, float y, float z)
    {
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRate(new FollowingNpcCheckTask(env, new CoordinateDestinationChecker(npc, x, y, z)), 1000,
            1000);
    }

    public static IScheduledFuture NewFollowingToTargetCheckTask(QuestEnv env, Npc npc, ZoneName zoneName)
    {
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRate(new FollowingNpcCheckTask(env, new ZoneChecker(npc, zoneName)), 1000, 1000);
    }
}
