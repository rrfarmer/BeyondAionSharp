using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Vortexspawns;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Services.Rift;
using Aion.GameServer.Services.Vortex;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/VortexService (Source). Dimensional-vortex invasion lifecycle. **DimensionalVortex&lt;?&gt;→DimensionalVortex&lt;VortexLocation&gt;** (codebase invariance bound — Invasion extends DimensionalVortex&lt;VortexLocation&gt;); ConcurrentHashMap→ConcurrentDictionary (get→GetValueOrDefault, put→indexer, remove→TryRemove); synchronized(this)→lock(this); schedule(...,HOURS)→Schedule(TimeSpan.FromHours); enum.equals→==; WorldMapType.X.getId()→GetId(). DimensionalVortex/Invasion/CronService/RiftManager red-tolerated.</summary>
public class VortexService
{
    private readonly ConcurrentDictionary<int, DimensionalVortex<VortexLocation>> activeInvasions = new ConcurrentDictionary<int, DimensionalVortex<VortexLocation>>();

    public void InitVortexLocations()
    {
        if (CustomConfig.VORTEX_ENABLED)
        {
            foreach (VortexLocation loc in DataManager.VORTEX_DATA.GetVortexLocations().Values)
                Spawn(loc, VortexStateType.PEACE);

            CronService.GetInstance().Schedule(() => StartInvasion(0), CustomConfig.VORTEX_THEOBOMOS_SCHEDULE);
            CronService.GetInstance().Schedule(() => StartInvasion(1), CustomConfig.VORTEX_BRUSTHONIN_SCHEDULE);
        }
    }

    public void StartInvasion(int id)
    {
        DimensionalVortex<VortexLocation> invasion;

        lock (this)
        {
            if (activeInvasions.ContainsKey(id))
            {
                return;
            }
            invasion = new Invasion(DataManager.VORTEX_DATA.GetVortexLocations().GetValueOrDefault(id));
            activeInvasions[id] = invasion;
        }

        invasion.Start();

        // schedule invasion end
        ThreadPoolManager.GetInstance().Schedule(ct => { StopInvasion(id); return ValueTask.CompletedTask; }, System.TimeSpan.FromHours(GetDuration()));
    }

    public void StopInvasion(int id)
    {
        if (!IsInvasionInProgress(id))
        {
            return;
        }

        DimensionalVortex<VortexLocation> invasion;
        lock (this)
        {
            activeInvasions.TryRemove(id, out invasion);
        }

        if (invasion == null || invasion.IsFinished())
        {
            return;
        }

        invasion.Stop();
    }

    public void Spawn(VortexLocation loc, VortexStateType state)
    {
        // Spawn Dimensional Vortex
        if (state == VortexStateType.INVASION)
        {
            RiftManager.GetInstance().SpawnVortex(loc);
            RiftInformer.SendRiftsInfo(loc.GetHomeWorldId());
        }

        // Spawn NPC
        List<SpawnGroup> locSpawns = DataManager.SPAWNS_DATA.GetVortexSpawnsByLocId(loc.GetId());
        foreach (SpawnGroup group in locSpawns)
        {
            foreach (SpawnTemplate st in group.GetSpawnTemplates())
            {
                VortexSpawnTemplate vortextemplate = (VortexSpawnTemplate)st;
                if (vortextemplate.GetStateType() == state)
                {
                    loc.GetSpawned().Add(SpawnEngine.SpawnObject(vortextemplate, 1));
                }
            }
        }
    }

    public void Despawn(VortexLocation loc)
    {
        // Unset Vortex controller
        loc.SetVortexController(null);

        // Despawn all NPC
        foreach (VisibleObject npc in loc.GetSpawned())
        {
            npc.GetController().DeleteIfAliveOrCancelRespawn();
        }

        loc.GetSpawned().Clear();
    }

    public bool IsInvasionInProgress(int id)
    {
        return activeInvasions.ContainsKey(id);
    }

    public IDictionary<int, DimensionalVortex<VortexLocation>> GetActiveInvasions()
    {
        return activeInvasions;
    }

    public int GetDuration()
    {
        return CustomConfig.VORTEX_DURATION;
    }

    public void RemoveDefenderPlayer(Player player)
    {
        foreach (DimensionalVortex<VortexLocation> invasion in activeInvasions.Values)
        {
            if (invasion.GetDefenders().ContainsKey(player.GetObjectId()))
            {
                invasion.KickPlayer(player, false);
                return;
            }
        }
    }

    public void RemoveInvaderPlayer(Player player)
    {
        foreach (DimensionalVortex<VortexLocation> invasion in activeInvasions.Values)
        {
            if (invasion.GetInvaders().ContainsKey(player.GetObjectId()))
            {
                invasion.KickPlayer(player, true);
                return;
            }
        }
    }

    public bool IsInvaderPlayer(Player player)
    {
        foreach (DimensionalVortex<VortexLocation> invasion in activeInvasions.Values)
        {
            if (invasion.GetInvaders().ContainsKey(player.GetObjectId()))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsInsideVortexZone(Player player)
    {
        VortexLocation loc = GetLocationByWorld(player.GetWorldId());
        return loc != null && loc.GetPlayers().ContainsKey(player.GetObjectId());
    }

    public VortexLocation GetLocationByRift(int npcId)
    {
        return GetLocationByWorld(npcId == 831141 ? WorldMapType.BRUSTHONIN.GetId() : WorldMapType.THEOBOMOS.GetId());
    }

    public VortexLocation GetLocationByWorld(int worldId)
    {
        if (worldId == WorldMapType.THEOBOMOS.GetId())
        {
            return DataManager.VORTEX_DATA.GetVortexLocations().GetValueOrDefault(0);
        }
        else if (worldId == WorldMapType.BRUSTHONIN.GetId())
        {
            return DataManager.VORTEX_DATA.GetVortexLocations().GetValueOrDefault(1);
        }
        else
        {
            return null;
        }
    }

    public static VortexService GetInstance()
    {
        return VortexServiceHolder.INSTANCE;
    }

    private static class VortexServiceHolder
    {
        internal static readonly VortexService INSTANCE = new VortexService();
    }
}
