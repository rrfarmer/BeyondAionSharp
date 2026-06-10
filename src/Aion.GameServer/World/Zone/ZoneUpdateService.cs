using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Taskmanager;

namespace Aion.GameServer.World.Zone;

/// <summary>Java parity: world/zone/ZoneUpdateService (ATracer) : AbstractFIFOPeriodicTaskManager&lt;Creature&gt;. callTask/getCalledMethodName→override; instanceof→is; SingletonHolder→static readonly. ZoneLevelService red-tolerated.</summary>
public class ZoneUpdateService : AbstractFIFOPeriodicTaskManager<Creature>
{
    private ZoneUpdateService() : base(500)
    {
    }

    protected override void CallTask(Creature creature)
    {
        // validate all zones irrespective of the current zone
        creature.RevalidateZones();
        if (creature is Player)
        {
            ZoneLevelService.CheckZoneLevels((Player)creature);
        }
    }

    protected override string GetCalledMethodName()
    {
        return "ZoneUpdateService()";
    }

    public static ZoneUpdateService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly ZoneUpdateService instance = new();
    }
}
