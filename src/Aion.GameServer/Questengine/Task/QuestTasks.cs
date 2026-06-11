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

/// <summary>Java parity: questEngine/task/QuestTasks (ATracer). Future&lt;?&gt;→ScheduledTask (the repo's idiomatic
/// async ThreadPoolManager handle); Java scheduleAtFixedRate(Runnable,1000,1000)→ScheduleAtFixedRateTask(ct=>{run;return},
/// TimeSpan,TimeSpan); IllegalArgumentException→ArgumentException.</summary>
public class QuestTasks
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1000);

    private static ScheduledTask Schedule(FollowingNpcCheckTask task)
    {
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            ct => { task.Run(); return ValueTask.CompletedTask; }, Interval, Interval);
    }

    /// <summary>Schedule new following checker task</summary>
    public static ScheduledTask NewFollowingToTargetCheckTask(QuestEnv env, Npc npc, Npc target)
    {
        return Schedule(new FollowingNpcCheckTask(env, new TargetDestinationChecker(npc, target)));
    }

    /// <summary>Schedule new following checker task</summary>
    public static ScheduledTask NewFollowingToTargetCheckTask(QuestEnv env, Npc npc, int npcTargetId)
    {
        SpawnSearchResult searchResult = DataManager.SPAWNS_DATA.GetFirstSpawnByNpcId(npc.GetWorldId(), npcTargetId);
        if (searchResult == null)
        {
            throw new ArgumentException("Supplied npc doesn't exist: " + npcTargetId);
        }
        return Schedule(new FollowingNpcCheckTask(env, new CoordinateDestinationChecker(npc, searchResult.GetSpot().GetX(), searchResult.GetSpot().GetY(), searchResult
                .GetSpot().GetZ())));
    }

    /// <summary>Schedule new following checker task</summary>
    public static ScheduledTask NewFollowingToTargetCheckTask(QuestEnv env, Npc npc, float x, float y, float z)
    {
        return Schedule(new FollowingNpcCheckTask(env, new CoordinateDestinationChecker(npc, x, y, z)));
    }

    public static ScheduledTask NewFollowingToTargetCheckTask(QuestEnv env, Npc npc, ZoneName zoneName)
    {
        return Schedule(new FollowingNpcCheckTask(env, new ZoneChecker(npc, zoneName)));
    }
}
