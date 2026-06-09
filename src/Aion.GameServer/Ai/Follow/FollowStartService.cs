using System;
using System.Threading.Tasks;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Ai.Follow;

/// <summary>Java parity: ai/follow/FollowStartService (xTz).</summary>
public class FollowStartService
{
    /// <summary>Schedule new following checker task.</summary>
    public static ScheduledTask NewFollowingToTargetCheckTask(Summon follower, Creature leading)
    {
        FollowSummonTaskAI task = new FollowSummonTaskAI(leading, follower);
        return ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            task.Run();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000));
    }
}
