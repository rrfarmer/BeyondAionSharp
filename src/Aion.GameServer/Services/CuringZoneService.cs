using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Curingzone;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Curingzones;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/CuringZoneService (xTz).</summary>
public class CuringZoneService
{
    private static readonly ILogger log = NullLogger.Instance;
    private List<CuringObject> curingObjects = new List<CuringObject>();

    private CuringZoneService()
    {
        foreach (CuringTemplate t in DataManager.CURING_OBJECTS_DATA.GetCuringObject())
        {
            CuringObject obj = new CuringObject(t, 0);
            obj.Spawn();
            curingObjects.Add(obj);
        }
        log.LogInformation("spawned Curing Zones");
        StartTask();
    }

    private void StartTask()
    {
        ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            foreach (CuringObject obj in curingObjects)
            {
                obj.GetKnownList().ForEachPlayer(player =>
                {
                    if (PositionUtil.IsInRange(obj, player, obj.GetRange()) && !player.GetEffectController().HasAbnormalEffect(8751))
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(player, 8751, 1, player).UseNoAnimationSkill();
                    }
                });
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000));
    }

    private static class SingletonHolder
    {
        internal static readonly CuringZoneService instance = new CuringZoneService();
    }

    public static CuringZoneService GetInstance()
    {
        return SingletonHolder.instance;
    }
}
