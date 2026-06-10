using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Schedule;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Worldraid;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Services.Worldraid;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/WorldRaidService (Whoop, Sykra). World-raid schedule/lifecycle. Map→Dictionary/ConcurrentDictionary; Collections.emptyMap→empty Dictionary; Map.get→GetValueOrDefault, remove→TryRemove; synchronized(this)→lock(this); stream map/collect→Select/ToList; nested forEach→nested foreach; currentTimeMillis→UtcNow. CronService/WorldRaid/WorldRaidRunnable/WorldRaidSchedules red-tolerated.</summary>
public class WorldRaidService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(WorldRaidService));
    private static readonly WorldRaidService instance = new WorldRaidService();

    private Dictionary<int, WorldRaidLocation> raidLocationsById;
    private readonly ConcurrentDictionary<int, WorldRaid> activeRaids = new ConcurrentDictionary<int, WorldRaid>();
    private readonly ConcurrentDictionary<int, long> lastMsgDateByMapId = new ConcurrentDictionary<int, long>();

    private WorldRaidService()
    {
    }

    public void InitWorldRaidLocations()
    {
        log.LogDebug("Initializing world raid locations...");
        if (EventsConfig.ENABLE_WORLDRAID)
            raidLocationsById = DataManager.WORLD_RAID_DATA.GetLocations();
        else
            raidLocationsById = new Dictionary<int, WorldRaidLocation>();
        log.LogDebug("Finished initialization of world raid locations with size " + raidLocationsById.Count);
    }

    public void InitWorldRaids()
    {
        log.LogDebug("Initializing world raid schedules...");
        if (!EventsConfig.ENABLE_WORLDRAID)
            return;
        // Initialize Raid Schedules
        foreach (var worldRaidSchedule in WorldRaidSchedules.Load().GetWorldRaidSchedules())
        {
            foreach (var cronExpr in worldRaidSchedule.GetRaidTimes())
            {
                CronService.GetInstance().Schedule(new WorldRaidRunnable(worldRaidSchedule), cronExpr);
            }
        }
        log.LogDebug("Finished initialization of world raid schedules.");
    }

    public List<WorldRaidLocation> GetActiveWorldRaidLocations()
    {
        return activeRaids.Values.Select(worldRaid => raidLocationsById.GetValueOrDefault(worldRaid.GetLocationId())).ToList();
    }

    public bool IsValidWorldRaidLocation(int locationId)
    {
        if (raidLocationsById.ContainsKey(locationId))
            return true;
        log.LogDebug("No world raid location found for id: " + locationId);
        return false;
    }

    public bool IsWorldRaidInProgress(int locationId)
    {
        return activeRaids.ContainsKey(locationId);
    }

    public void StartRaid(int locationId, bool useSpecialSpawnMsg)
    {
        log.LogDebug("Starting world raid for location: " + locationId);
        WorldRaid worldRaid;
        lock (this)
        {
            if (!IsValidWorldRaidLocation(locationId))
            {
                log.LogError("Attempt to start world raid for an invalid location: " + locationId);
                return;
            }
            if (IsWorldRaidInProgress(locationId))
            {
                log.LogError("Attempt to start world raid twice for location: " + locationId);
                return;
            }
            bool sendMessages = true;
            WorldRaidLocation location = raidLocationsById.GetValueOrDefault(locationId);
            long currentMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (lastMsgDateByMapId.ContainsKey(location.GetMapId()))
            {
                long lastMsgDateMs = lastMsgDateByMapId[location.GetMapId()];
                // 30 seconds until another message is allowed to be displayed
                if (lastMsgDateMs + 30000 < currentMillis)
                    lastMsgDateByMapId[location.GetMapId()] = currentMillis;
                else
                    sendMessages = false;
            }
            else
            {
                lastMsgDateByMapId[location.GetMapId()] = currentMillis;
            }
            worldRaid = new WorldRaid(location, useSpecialSpawnMsg, sendMessages);
            activeRaids[locationId] = worldRaid;
        }
        worldRaid.StartWorldRaid();
        log.LogDebug("Finished world raid start for location: " + locationId);
    }

    public void StopRaid(int locationId)
    {
        log.LogDebug("Stopping world for location: " + locationId);
        WorldRaid raid;
        lock (this)
        {
            activeRaids.TryRemove(locationId, out raid);
        }
        if (raid == null || raid.IsFinished())
        {
            log.LogDebug("Attempt to stop world raid twice for location: " + locationId);
            return;
        }
        raid.StopWorldRaid();
        log.LogDebug("Succeeded to finish world raid for location: " + locationId);
    }

    public static WorldRaidService GetInstance()
    {
        return instance;
    }
}
