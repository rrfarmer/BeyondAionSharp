using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Schedule;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Rift;
using Aion.GameServer.Model.Templates.Rift;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Riftspawns;
using Aion.GameServer.Services.Cron;
using GameWorld = Aion.GameServer.World.World;
using SpawnEngineClass = Aion.GameServer.SpawnEngine.SpawnEngine;

namespace Aion.GameServer.Services.Rift;

/// <summary>
/// Java parity: services/RiftService.java (author Source). Ported 1:1 from the faithful Java source.
/// Java package com.aionemu.gameserver.services maps to C# namespace Aion.GameServer.Services; the reworked
/// slop RiftService still occupies that exact FQN (Aion.GameServer.Services.RiftService) and holds the DI
/// singleton, so this faithful 1:1 port lands additively under Aion.GameServer.Services.Rift (NO collision).
/// The singleton swap + consumer repoint + slop deletion is the next tick (step C/D). ConcurrentHashMap ->
/// ConcurrentDictionary; Map -> IDictionary; Collections.emptyMap() -> new Dictionary; putIfAbsent/remove(k,v)
/// emulated 1:1; RespawnService is the faithful static; SpawnsData.getRiftSpawnsByLocId may return null ->
/// guarded (Java holder always returns a list, the C# holder is nullable). RVController/RiftManager red-tolerated.
/// </summary>
public class RiftService
{
    private RiftSchedule schedule;
    private IDictionary<int, RiftLocation> locations;
    private readonly ConcurrentDictionary<int, RiftLocation> activeRifts = new ConcurrentDictionary<int, RiftLocation>();

    public void InitRiftLocations()
    {
        if (CustomConfig.RIFT_ENABLED)
        {
            locations = DataManager.RIFT_DATA.GetRiftLocations();
        }
        else
        {
            locations = new Dictionary<int, RiftLocation>();
        }
    }

    public void InitRifts()
    {
        if (CustomConfig.RIFT_ENABLED)
        {
            schedule = RiftSchedule.Load();
            foreach (RiftSchedule.Rift rift in schedule.GetRiftsList())
                foreach (OpenRift open in rift.GetRift())
                    CronService.GetInstance().Schedule(new RiftOpenRunnable(rift.GetWorldId(), open.SpawnGuards()), open.GetSchedule());
        }
    }

    public bool IsValidId(int id)
    {
        if (IsRift(id))
        {
            return RiftService.GetInstance().GetRiftLocations().ContainsKey(id);
        }
        else
        {
            foreach (RiftLocation loc in RiftService.GetInstance().GetRiftLocations().Values)
            {
                if (loc.GetWorldId() == id)
                    return true;
            }
        }
        return false;
    }

    private bool IsRift(int id)
    {
        return id < 10000;
    }

    public bool OpenRifts(int id, bool guards)
    {
        if (IsValidId(id))
        {
            if (IsRift(id))
            {
                RiftLocation rift = GetRiftLocation(id);
                if (rift.GetSpawned().Count == 0)
                {
                    OpenRifts(rift, guards);

                    // Broadcast rift spawn on map
                    RiftInformer.SendRiftsInfo(rift.GetWorldId());
                    return true;
                }
            }
            else
            {
                bool opened = false;
                foreach (RiftLocation rift in GetRiftLocations().Values)
                {
                    if (rift.GetWorldId() == id && rift.GetSpawned().Count == 0)
                    {
                        OpenRifts(rift, guards);
                        opened = true;
                    }
                }

                // Broadcast rift spawn on map
                RiftInformer.SendRiftsInfo(id);
                return opened;
            }
        }
        return false;
    }

    public bool CloseRifts(int id)
    {
        if (IsValidId(id))
        {
            if (IsRift(id))
            {
                RiftLocation rift = GetRiftLocation(id);
                if (rift.GetSpawned().Count != 0)
                {
                    CloseRift(rift);
                    return true;
                }
            }
            else
            {
                bool opened = false;
                foreach (RiftLocation rift in GetRiftLocations().Values)
                {
                    if (rift.GetWorldId() == id && rift.GetSpawned().Count != 0)
                    {
                        CloseRift(rift);
                        opened = true;
                    }
                }
                return opened;
            }
        }
        return false;
    }

    /**
     * Just a work-around, this stuff needs refactoring
     *
     * @param id
     *          better use map IDs
     */
    public void PrepareRiftOpening(int id, bool guards)
    {
        if (id != 210070000 && id != 220080000)
        {
            if (!guards && Rnd.NextBoolean())
                return;
        }
        List<RiftLocation> possibleLocs = new List<RiftLocation>();
        foreach (RiftLocation loc in locations.Values)
        {
            if (loc.GetWorldId() == id)
            {
                if (guards)
                {
                    if (loc.HasSpawns() && loc.IsAutoCloseable())
                        possibleLocs.Add(loc);
                    continue;
                }
                if (!loc.HasSpawns() && loc.IsAutoCloseable())
                    possibleLocs.Add(loc);
            }
        }

        int maxCount = 1;
        if (!guards)
        {
            switch (id)
            {
                case 210050000:
                case 220070000:
                    maxCount = 3;
                    break;
                default:
                    maxCount = 4;
                    break;
            }
        }

        int count = Rnd.Get(1, maxCount);
        while (possibleLocs.Count > count)
            possibleLocs.RemoveAt(Rnd.Get(0, possibleLocs.Count - 1));

        foreach (RiftLocation loc in possibleLocs)
            OpenRifts(loc, guards);
    }

    public void OpenRifts(RiftLocation location, bool isWithGuards)
    {
        if (!activeRifts.TryAdd(location.GetId(), location))
            return;
        location.SetOpened(true);

        // Spawn NPC guards
        if (isWithGuards)
        {
            List<SpawnGroup> locSpawns = DataManager.SPAWNS_DATA.GetRiftSpawnsByLocId(location.GetId());
            if (locSpawns != null)
            {
                foreach (SpawnGroup group in locSpawns)
                {
                    foreach (SpawnTemplate st in group.GetSpawnTemplates())
                    {
                        RiftSpawnTemplate template = (RiftSpawnTemplate)st;
                        location.AddSpawned(SpawnEngineClass.SpawnObject(template, 1));
                    }
                }
            }
        }

        // Spawn rifts
        RiftManager.GetInstance().SpawnRift(location, isWithGuards);
    }

    public void CloseRift(RiftLocation location)
    {
        if (!activeRifts.TryRemove(new KeyValuePair<int, RiftLocation>(location.GetId(), location)))
            return;
        location.SetOpened(false);

        // Despawn rift NPCs and cancel their respawns
        foreach (KeyValuePair<int, SpawnTemplate> entry in location.GetSpawned())
        {
            int npcObjectId = entry.Key;
            SpawnTemplate npcSpawnTemplate = entry.Value;
            VisibleObject npc = GameWorld.GetInstance().FindVisibleObject(npcObjectId);
            // npcObjectId may have been released and reassigned to a completely unrelated mob, if the original (now despawned) rift npc was a non
            // respawning mob. spawned list doesn't clean up removed spawns but only updates respawning npcObjectIds, therefore the npcSpawnTemplate check
            if (npc != null && npc.GetSpawn() == npcSpawnTemplate)
                npc.GetController().DeleteIfAliveOrCancelRespawn();
            else
                RespawnService.CancelRespawn(npcObjectId, npcSpawnTemplate);
        }

        // Clear spawned list
        location.GetSpawned().Clear();
    }

    public bool IsRiftOpened(int riftId)
    {
        return activeRifts.ContainsKey(riftId);
    }

    public void CloseAutoCloseableRifts(int worldId)
    {
        foreach (RiftLocation rift in activeRifts.Values)
        {
            if (rift.IsAutoCloseable() && rift.GetWorldId() == worldId)
            {
                CloseRift(rift);
            }
        }
    }

    public void UpdateSpawned(int oldObjectId, VisibleObject respawn)
    {
        foreach (RiftLocation loc in locations.Values)
        {
            if (loc.ReplaceSpawned(oldObjectId, respawn))
                break;
        }
    }

    public int GetDuration()
    {
        return CustomConfig.RIFT_DURATION;
    }

    public RiftLocation GetRiftLocation(int id)
    {
        return locations.TryGetValue(id, out RiftLocation loc) ? loc : null;
    }

    public IDictionary<int, RiftLocation> GetRiftLocations()
    {
        return locations;
    }

    public static RiftService GetInstance()
    {
        return RiftServiceHolder.INSTANCE;
    }

    private static class RiftServiceHolder
    {
        internal static readonly RiftService INSTANCE = new RiftService();
    }
}
