using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Commons.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Schedule;

namespace Aion.GameServer.Services.Worldraid;

/// <summary>Java parity: services/worldraid/WorldRaidRunnable. Java Runnable→plain class+Run(); stream filter/collect→Where/ToList.</summary>
public class WorldRaidRunnable : Aion.Commons.Lang.Runnable
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly WorldRaidSchedules.WorldRaidSchedule worldRaidSchedule;

    public WorldRaidRunnable(WorldRaidSchedules.WorldRaidSchedule worldRaidSchedule)
    {
        this.worldRaidSchedule = worldRaidSchedule;
    }

    public void Run()
    {
        log.LogDebug("Attempting to start world raid with ID: " + worldRaidSchedule.GetId() + " and location pool: " + worldRaidSchedule.GetLocations());
        List<int> validRaidLocations = worldRaidSchedule.GetLocations()
            .Where(locationId => WorldRaidService.GetInstance().IsValidWorldRaidLocation(locationId)
                && !WorldRaidService.GetInstance().IsWorldRaidInProgress(locationId))
            .ToList();
        if (validRaidLocations.Count != worldRaidSchedule.GetLocations().Count)
        {
            log.LogWarning("Invalid world raid location count for raid with ID: " + worldRaidSchedule.GetId()
                + ". Some locations may be invalid due to a misconfiguration or due to currently running raids!");
            return;
        }
        // determine location count
        int spawnLocationCount = worldRaidSchedule.GetLocations().Count;
        if (worldRaidSchedule.GetMinCount() > 0 && spawnLocationCount > worldRaidSchedule.GetMinCount())
            spawnLocationCount = worldRaidSchedule.GetMinCount();
        if (worldRaidSchedule.GetMaxCount() > 0 && worldRaidSchedule.GetMaxCount() > spawnLocationCount)
            spawnLocationCount = Rnd.Get(spawnLocationCount, worldRaidSchedule.GetMaxCount());
        // remove unused locations due to location count restriction
        while (validRaidLocations.Count > spawnLocationCount)
            validRaidLocations.RemoveAt(Rnd.NextInt(validRaidLocations.Count));
        // start actual world raids using the remaining locations
        foreach (int locationId in validRaidLocations)
            WorldRaidService.GetInstance().StartRaid(locationId, worldRaidSchedule.IsSpecialRaid());
        if (validRaidLocations.Count != 0)
            log.LogDebug("Started scheduled world raid with ID " + worldRaidSchedule.GetId() + " at the following raid locations: " + validRaidLocations);
    }
}
