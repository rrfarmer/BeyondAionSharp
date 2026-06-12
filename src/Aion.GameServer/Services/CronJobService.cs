using System;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>
/// This class is used for miscellaneous long-time schedules like specific spawns.
/// Java parity: services/CronJobService (Estrayl).
/// </summary>
public class CronJobService
{
    private static readonly CronJobService INSTANCE = new CronJobService();

    private CronJobService()
    {
        ScheduleMoltenusSpawn();
        ScheduleAhserionsFlight();
        ScheduleIdianDepthPortalSpawns();
        ScheduleLegionDominionCalculation();
    }

    public static CronJobService GetInstance()
    {
        return INSTANCE;
    }

    private void ScheduleMoltenusSpawn()
    {
        CronService.GetInstance().Schedule(new MoltenusSpawnTask().Run, SiegeConfig.MOLTENUS_SPAWN_SCHEDULE);
    }

    // Java parity: anonymous Runnable with persistent 'moltenus' field across cron fires.
    private class MoltenusSpawnTask
    {
        private Npc moltenus;

        public void Run()
        {
            if (moltenus != null && moltenus.IsSpawned())
                return;

            SpawnTemplate template = Rnd.Get(1, 3) switch
            {
                1 => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(400010000, 251045, 2464.9199f, 1689f, 2882.221f, (byte) 0),
                2 => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(400010000, 251045, 2263.4812f, 2587.1633f, 2879.5447f, (byte) 0),
                _ => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(400010000, 251045, 1692.96f, 1809.04f, 2886.027f, (byte) 0),
            };
            moltenus = (Npc) Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(template, 1);
            // Despawn task
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                if (moltenus != null && !moltenus.IsDead())
                {
                    moltenus.GetController().Delete();
                    moltenus = null;
                }
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(3600 * 1000));
        }
    }

    private void ScheduleAhserionsFlight()
    {
        CronService.GetInstance().Schedule(() => PanesterraService.GetInstance().StartAhserionRaid(), SiegeConfig.AHSERION_START_SCHEDULE);
    }

    private void ScheduleIdianDepthPortalSpawns()
    {
        new IdianDepthPortalSpawner().Run(); // not a cronjob anymore, but let's keep it here
    }

    private void ScheduleLegionDominionCalculation()
    {
        CronService.GetInstance().Schedule(() => LegionDominionService.GetInstance().StartWeeklyCalculation(), "0 0 9 ? * WED *");
    }

    private class IdianDepthPortalSpawner
    {
        private Npc asmodianUndergroundEntrance;
        private Npc elyosUndergroundEntrance;

        public void Run()
        {
            SpawnTemplate elyosSpawn = Rnd.Get(1, 4) switch
            {
                1 => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(600100000, 731631, 721.39f, 268.67f, 291.636f, (byte) 60), // Levinshor
                2 => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(600100000, 731631, 332.40f, 1903.37f, 232.000f, (byte) 110), // Levinshor
                3 => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(600090000, 731631, 1179.58f, 687.52f, 190.625f, (byte) 0), // Kaldor
                _ => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(210070000, 731631, 777.01f, 1479.86f, 457.375f, (byte) 30), // Cygnea
            };
            SpawnTemplate asmodianSpawn = Rnd.Get(1, 4) switch
            {
                1 => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(600100000, 731632, 1478.78f, 1844.20f, 225.987f, (byte) 45), // Levinshor
                2 => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(600100000, 731632, 1870.49f, 41.64f, 244.711f, (byte) 15), // Levinshor
                3 => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(600090000, 731632, 415.01f, 564.42f, 142.0f, (byte) 100), // Kaldor
                _ => Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(220080000, 731632, 233.39f, 1137.03f, 225.875f, (byte) 105), // Enshar
            };
            if (asmodianUndergroundEntrance != null)
                asmodianUndergroundEntrance.GetController().Delete();
            if (elyosUndergroundEntrance != null)
                elyosUndergroundEntrance.GetController().Delete();
            elyosUndergroundEntrance = (Npc) Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(elyosSpawn, 1);
            asmodianUndergroundEntrance = (Npc) Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(asmodianSpawn, 1);
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                Run();
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(Rnd.Get(3600, 18000)));
        }
    }
}
