using System;
using Aion.GameServer.Model;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Schedule;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Services.Siege;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Quartz;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/SiegeService (SoulKeeper, Source, Neon, Estrayl) — 3.0 siege. Singleton; "SIEGE_LOG" logger; activeSieges ConcurrentDictionary; Map&lt;Integer,Siege&lt;? extends SiegeLocation&gt;&gt;→Siege&lt;SiegeLocation&gt; (wildcard erasure to invariance bound); AtomicBoolean isInitialized.compareAndSet; Collections.emptyMap→new Dictionary; Date→DateTimeOffset, currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; synchronized→lock(this); forEachPlayer→ForEachPlayer; streams→LINQ; map.compute→TryGetValue; anonymous Consumer→lambda; Byte.parseByte→byte.Parse; NumberFormat/UnsupportedOperation→Format/NotSupported. CronExpression/Quartz/Siege subclasses/DAO red-tolerated.</summary>
public class SiegeService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("SIEGE_LOG");

    /// <summary>We should broadcast fortress status every hour. Actually only an influence packet must be sent, but that doesn't matter.</summary>
    private static readonly CronExpression SIEGE_LOCATION_STATUS_BROADCAST_SCHEDULE = CronExpressions.GetOrCreate("0 0 * ? * *");

    /// <summary>Singleton that is loaded on the class initialization.</summary>
    private static readonly SiegeService instance = new SiegeService();
    /// <summary>Map that holds fortressId to Siege. We can easily know what fortresses are under siege ATM.</summary>
    private readonly Dictionary<int, global::Aion.GameServer.Services.Siege.Siege> activeSieges = new Dictionary<int, global::Aion.GameServer.Services.Siege.Siege>();

    // Player list on RVR Event.
    private readonly AtomicBoolean isInitialized = new AtomicBoolean();
    private readonly Dictionary<int, ArtifactLocation> artifacts;
    private readonly Dictionary<int, FortressLocation> fortresses;
    private readonly Dictionary<int, OutpostLocation> outposts;
    private readonly Dictionary<int, SiegeLocation> locations;
    private AgentLocation agent;
    private DateTimeOffset? nextStateUpdateTime;
    private HashSet<Player> rvrEventPlayers = new HashSet<Player>();

    public static SiegeService GetInstance()
    {
        return instance;
    }

    private SiegeService()
    {
        if (SiegeConfig.SIEGE_ENABLED)
        {
            log.LogInformation("Initializing sieges...");

            // initialize current siege locations
            artifacts = DataManager.SIEGE_LOCATION_DATA.GetArtifacts();
            fortresses = DataManager.SIEGE_LOCATION_DATA.GetFortress();
            outposts = DataManager.SIEGE_LOCATION_DATA.GetOutpost();
            locations = DataManager.SIEGE_LOCATION_DATA.GetSiegeLocations();
            agent = DataManager.SIEGE_LOCATION_DATA.GetAgentLoc();
            SiegeDAO.LoadSiegeLocations(locations);
        }
        else
        {
            artifacts = new Dictionary<int, ArtifactLocation>();
            fortresses = new Dictionary<int, FortressLocation>();
            outposts = new Dictionary<int, OutpostLocation>();
            locations = new Dictionary<int, SiegeLocation>();
            log.LogInformation("Sieges are disabled in config.");
        }
    }

    private void UpdateNextStateUpdateTime()
    {
        nextStateUpdateTime = SIEGE_LOCATION_STATUS_BROADCAST_SCHEDULE.GetTimeAfter(DateTimeOffset.Now);
    }

    public void InitSieges()
    {
        if (!isInitialized.CompareAndSet(false, true) || !SiegeConfig.SIEGE_ENABLED)
            return;

        // despawn all NPCs spawned by spawn engine.
        // Siege spawns should be controlled by siege service
        foreach (int i in GetSiegeLocations().Keys)
        {
            DeSpawnNpcs(i);
        }

        // spawn fortress common npcs
        foreach (FortressLocation f in GetFortresses().Values)
        {
            SpawnNpcs(f.GetLocationId(), f.GetRace(), SiegeModType.PEACE);
        }

        // spawn outpost protectors...
        foreach (OutpostLocation o in GetOutposts().Values)
        {
            SpawnNpcs(o.GetLocationId(), o.GetRace(), SiegeModType.PEACE);
        }

        // spawn artifacts
        foreach (ArtifactLocation a in GetStandaloneArtifacts())
        {
            SpawnNpcs(a.GetLocationId(), a.GetRace(), SiegeModType.PEACE);
        }

        // initialize siege schedule
        SiegeSchedules siegeSchedules = SiegeSchedules.Load();

        // Schedule fortresses sieges protector spawn
        foreach (SiegeSchedules.Fortress f in siegeSchedules.GetFortresses())
        {
            foreach (string siegeTime in f.GetSiegeTimes())
            {
                string preparationCron = GetPreparationCronString(siegeTime, f.GetId());
                CronService.GetInstance().Schedule(new SiegeStartRunnable(f.GetId()), preparationCron);
                log.LogDebug("Scheduled siege of fortressID {Id} based on cron expression: {Cron}", f.GetId(), preparationCron);
            }
        }

        // Schedule agent fights
        foreach (SiegeSchedules.AgentFight a in siegeSchedules.GetAgentFights())
        {
            foreach (string siegeTime in a.GetSiegeTimes())
            {
                CronService.GetInstance().Schedule(new SiegeStartRunnable(a.GetId()), siegeTime);
                log.LogDebug("Scheduled agent fight based on cron expression: {Cron}", siegeTime);
            }
        }

        // Start siege of artifacts
        foreach (ArtifactLocation artifact in artifacts.Values)
        {
            if (artifact.IsStandAlone())
            {
                log.LogDebug("Starting siege of artifact #" + artifact.GetLocationId());
                StartSiege(artifact.GetLocationId());
            }
            else
            {
                log.LogDebug("Artifact #{Id} siege was not started, it belongs to fortress", artifact.GetLocationId());
            }
        }

        // We should set valid next state for fortress on startup. No need to broadcast state here, no players @ server ATM
        UpdateNextStateUpdateTime();
        UpdateFortressNextState();

        // Schedule siege status broadcast (every hour)
        CronService.GetInstance().Schedule(() =>
        {
            UpdateNextStateUpdateTime();
            UpdateFortressNextState();
            World.World.GetInstance().ForEachPlayer(player =>
            {
                foreach (FortressLocation fortress in GetFortresses().Values)
                    PacketSendUtility.SendPacket(player, new SM_FORTRESS_INFO(fortress.GetLocationId(), false));

                PacketSendUtility.SendPacket(player, new SM_FORTRESS_STATUS());

                foreach (FortressLocation fortress in GetFortresses().Values)
                    PacketSendUtility.SendPacket(player, new SM_FORTRESS_INFO(fortress.GetLocationId(), true));
            });
        }, SIEGE_LOCATION_STATUS_BROADCAST_SCHEDULE);
        log.LogDebug("Broadcasting Siege Location status based on expression: {Cron}", SIEGE_LOCATION_STATUS_BROADCAST_SCHEDULE);
    }

    public void CheckSiegeStart(int locationId)
    {
        if (agent.GetLocationId() == locationId)
            StartSiege(locationId);
        else
            StartPreparations(locationId);
    }

    private void StartPreparations(int locationId)
    {
        log.LogDebug("Starting preparations of siege Location:{LocationId}", locationId);
        FortressLocation loc = GetFortress(locationId);
        // Set siege start timer..
        ThreadPoolManager.GetInstance().Schedule(ct => { StartSiege(locationId); return System.Threading.Tasks.ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(300 * 1000));
        if (loc.GetTemplate().GetMaxOccupyCount() > 0 && loc.GetOccupiedCount() >= loc.GetTemplate().GetMaxOccupyCount()
            && !loc.GetRace().Equals(SiegeRace.BALAUR))
        {
            log.LogDebug("Resetting fortress to balaur control due to exceeded occupy count! locId:{LocationId}", locationId);
            ResetSiegeLocation(loc);
        }
        // TODO:
        if (locationId > 10000)
        { // Panesterra
            PanesterraService.GetInstance().PrepareFortressSiege(loc);
        }
    }

    public void StartSiege(int siegeLocationId)
    {
        lock (this)
        {
            log.LogDebug("Starting siege of siege location: " + siegeLocationId);

            // Siege should not be started two times
            if (activeSieges.ContainsKey(siegeLocationId))
            {
                log.LogError(new Exception(), "Attempt to start siege twice for siege location: " + siegeLocationId);
                return;
            }
            global::Aion.GameServer.Services.Siege.Siege siege = NewSiege(siegeLocationId);
            activeSieges[siegeLocationId] = siege;

            siege.StartSiege();

            // certain sieges are endless
            // should end only manually on siege boss death
            if (siege.IsEndless())
                return;

            // schedule siege end
            ThreadPoolManager.GetInstance().Schedule(ct => { StopSiege(siegeLocationId); return System.Threading.Tasks.ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(siege.GetSiegeLocation().GetSiegeDuration() * 1000L));
        }
    }

    public void StopSiege(int siegeLocationId)
    {
        lock (this)
        {
            log.LogDebug("Stopping siege of siege location: {LocationId}", siegeLocationId);

            if (!activeSieges.TryGetValue(siegeLocationId, out global::Aion.GameServer.Services.Siege.Siege siege))
            {
                log.LogDebug("Siege of siege location {LocationId} is not in progress, it was captured earlier?", siegeLocationId);
                return;
            }
            activeSieges.Remove(siegeLocationId);
            if (siege.IsFinished())
                return;
            siege.StopSiege();
        }
    }

    /// <summary>Used to capture fortresses or artifacts without regular siege</summary>
    public void CaptureSiege(SiegeRace sr, int legionId, int locId)
    {
        lock (this)
        {
            SiegeLocation loc = GetSiegeLocation(locId);
            global::Aion.GameServer.Services.Siege.Siege s = GetSiege(locId);
            if (s != null)
            {
                s.GetSiegeCounter().AddRaceDamage(sr, s.GetBoss().GetLifeStats().GetMaxHp() + 1);
                s.SetBossKilled(true);
                StopSiege(locId);
                loc.SetLegionId(legionId);
            }
            else
            {
                DeSpawnNpcs(locId);
                loc.SetVulnerable(false);
                loc.SetUnderShield(false);
                loc.SetRace(sr);
                loc.SetLegionId(legionId);
                SpawnNpcs(locId, sr, SiegeModType.PEACE);
                SiegeDAO.UpdateSiegeLocation(loc);
                switch (locId)
                {
                    case 2011:
                    case 2021:
                    case 3011:
                    case 3021:
                        UpdateOutpostSiegeState((FortressLocation)loc);
                        break;
                }
            }
            BroadcastUpdate(loc);
        }
    }

    /// <summary>Return location to balaur control</summary>
    private void ResetSiegeLocation(SiegeLocation loc)
    {
        lock (this)
        {
            // Despawn old npc
            DeSpawnNpcs(loc.GetLocationId());
            loc.ClearLocation(); // remove all players
            // Store old owner for msg
            int oldOwnerRaceId = loc.GetRace().GetRaceId();
            int legionId = loc.GetLegionId();
            string legionName = legionId != 0 ? LegionService.GetInstance().GetLegion(legionId).GetName() : "";
            string locL10n = loc.GetTemplate().GetL10n();

            // Reset owner
            loc.SetRace(SiegeRace.BALAUR);
            loc.SetLegionId(0);

            if (loc is FortressLocation)
            {
                ArtifactLocation artifact = GetFortressArtifact(loc.GetLocationId());
                if (artifact != null)
                {
                    artifact.SetRace(SiegeRace.BALAUR);
                    artifact.SetLegionId(0);
                }
            }
            loc.SetOccupiedCount(0);

            // On start preparations msg
            World.World.GetInstance().ForEachPlayer(player =>
            {
                if (legionId != 0 && player.GetRace().GetRaceId() == oldOwnerRaceId)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ABYSS_GUILD_CASTLE_TAKEN(legionName, locL10n));
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ABYSS_WIN_CASTLE(loc.GetRace().GetL10n(), locL10n));
                PacketSendUtility.SendPacket(player, new SM_SIEGE_LOCATION_INFO(loc));
            });

            BroadcastUpdate(loc);

            // Spawn new npc
            SpawnNpcs(loc.GetLocationId(), SiegeRace.BALAUR, SiegeModType.PEACE);

            SiegeDAO.UpdateSiegeLocation(loc);
        }
    }

    /// <summary>Updates next state for fortresses</summary>
    private void UpdateFortressNextState()
    {
        Dictionary<int, DateTimeOffset> startDatesByLocId = CollectNextSiegeStartDates();
        // update each fortress next state
        foreach (KeyValuePair<int, DateTimeOffset> entry in startDatesByLocId)
        {
            // update fortress state that will be valid within the next hour
            SiegeLocation fortress = GetSiegeLocation(entry.Key);
            if (!(entry.Value > nextStateUpdateTime)) // date is > now and <= next update time (this check also accounts for the preparation time)
                fortress.SetNextState(SiegeLocation.STATE_VULNERABLE);
            else
                fortress.SetNextState(SiegeLocation.STATE_INVULNERABLE);
        }
    }

    private Dictionary<int, DateTimeOffset> CollectNextSiegeStartDates()
    {
        Dictionary<SiegeStartRunnable, DateTimeOffset> dates = CronService.GetInstance().FindNextFireTimes<SiegeStartRunnable>(true);
        Dictionary<int, DateTimeOffset> nextSiegeStartDates = new Dictionary<int, DateTimeOffset>(dates.Count);
        foreach (KeyValuePair<SiegeStartRunnable, DateTimeOffset> entry in dates)
        {
            int locId = entry.Key.GetLocationId();
            DateTimeOffset date = entry.Value;
            if (!nextSiegeStartDates.TryGetValue(locId, out DateTimeOffset oldDate) || oldDate > date)
                nextSiegeStartDates[locId] = date;
        }
        return nextSiegeStartDates;
    }

    /// <summary>Number of seconds until fortress.getNextState() will be the current state (max. 3600 since states update every hour).</summary>
    public int GetSecondsUntilNextFortressState()
    {
        if (nextStateUpdateTime == null) // null if siege service is deactivated
            return 0;
        return (int)(nextStateUpdateTime.Value.ToUnixTimeMilliseconds() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000;
    }

    public int GetRemainingSiegeTimeInSeconds(int siegeLocationId)
    {
        global::Aion.GameServer.Services.Siege.Siege siege = GetSiege(siegeLocationId);
        if (siege == null || siege.IsFinished() || !siege.IsStarted())
            return 0;

        long endTime = siege.GetStartTime() / 1000 + siege.GetSiegeLocation().GetSiegeDuration();
        int secondsLeft = (int)(endTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);

        return Math.Max(secondsLeft, 0);
    }

    public global::Aion.GameServer.Services.Siege.Siege GetSiege(SiegeLocation loc)
    {
        return GetSiege(loc.GetLocationId());
    }

    public global::Aion.GameServer.Services.Siege.Siege GetSiege(int siegeLocationId)
    {
        return activeSieges.GetValueOrDefault(siegeLocationId);
    }

    public bool IsSiegeInProgress(int fortressId)
    {
        return activeSieges.ContainsKey(fortressId);
    }

    public Dictionary<int, OutpostLocation> GetOutposts()
    {
        return outposts;
    }

    public OutpostLocation GetOutpost(int id)
    {
        return outposts.GetValueOrDefault(id);
    }

    public Dictionary<int, FortressLocation> GetFortresses()
    {
        return fortresses;
    }

    public FortressLocation GetFortress(int id)
    {
        return fortresses.GetValueOrDefault(id);
    }

    public Dictionary<int, ArtifactLocation> GetArtifacts()
    {
        return artifacts;
    }

    public ArtifactLocation GetArtifact(int id)
    {
        return GetArtifacts().GetValueOrDefault(id);
    }

    public List<ArtifactLocation> GetStandaloneArtifacts()
    {
        return artifacts.Values.Where(a => a.IsStandAlone()).ToList();
    }

    public ArtifactLocation GetFortressArtifact(int siegeLocId)
    {
        ArtifactLocation loc = GetArtifact(siegeLocId);
        return loc == null || loc.GetOwningFortress() == null ? null : loc;
    }

    public DoorRepairData GetDoorRepairData(int siegeId)
    {
        FortressLocation fortressLocation = GetFortress(siegeId);
        if (fortressLocation == null)
            return null;
        return fortressLocation.GetTemplate().GetDoorRepairData();
    }

    public DoorRepairStone GetRepairStone(int siegeId, int repairStoneStaticId)
    {
        DoorRepairData doorRepairData = GetDoorRepairData(siegeId);
        if (doorRepairData == null)
            return null;
        return doorRepairData.GetRepairStone(repairStoneStaticId);
    }

    public Dictionary<int, SiegeLocation> GetSiegeLocations()
    {
        return locations;
    }

    public SiegeLocation GetSiegeLocation(int id)
    {
        return locations.GetValueOrDefault(id);
    }

    public Dictionary<int, SiegeLocation> GetSiegeLocations(int worldId)
    {
        Dictionary<int, SiegeLocation> mapLocations = new Dictionary<int, SiegeLocation>();
        foreach (SiegeLocation location in GetSiegeLocations().Values)
            if (location.GetWorldId() == worldId)
                mapLocations[location.GetLocationId()] = location;

        return mapLocations;
    }

    public AgentLocation GetAgentLocation()
    {
        return agent;
    }

    private global::Aion.GameServer.Services.Siege.Siege NewSiege(int siegeLocationId)
    {
        if (fortresses.ContainsKey(siegeLocationId))
            return new FortressSiege(fortresses[siegeLocationId]);
        else if (outposts.ContainsKey(siegeLocationId))
            return new OutpostSiege(outposts[siegeLocationId]);
        else if (artifacts.ContainsKey(siegeLocationId))
            return new ArtifactSiege(artifacts[siegeLocationId]);
        else if (agent != null && agent.GetLocationId() == siegeLocationId)
            return new AgentSiege(agent);
        else
            throw new SiegeException("Unknown siege handler for siege location: " + siegeLocationId);
    }

    public void CleanLegionId(int legionId)
    {
        foreach (SiegeLocation loc in GetSiegeLocations().Values)
        {
            if (loc.GetLegionId() == legionId)
            {
                loc.SetLegionId(0);
                SiegeDAO.UpdateSiegeLocation(loc);
            }
        }
    }

    public void UpdateOutpostSiegeState(FortressLocation fortressLoc)
    {
        lock (this)
        {
            foreach (OutpostLocation outpost in GetOutposts().Values)
            {
                List<int> dependencies = outpost.GetFortressDependency();
                if (!dependencies.Contains(fortressLoc.GetLocationId()))
                    continue;
                if (dependencies.Any(dependency => GetSiegeLocation(dependency).IsVulnerable()))
                    break;

                SiegeRace validRaceForSiege = outpost.GetLocationId() == 2111 ? SiegeRace.ASMODIANS : SiegeRace.ELYOS;
                bool isSiegeAllowed = true;

                foreach (int fortressId in dependencies)
                {
                    SiegeRace dependencyFortressRace = GetFortresses()[fortressId].GetRace();
                    if (validRaceForSiege != dependencyFortressRace)
                    {
                        isSiegeAllowed = false;
                        break;
                    }
                }

                StopSiege(outpost.GetLocationId());
                DeSpawnNpcs(outpost.GetLocationId());

                // broadcast to all new Silentera infiltration route state
                BroadcastStatusAndUpdate(outpost, outpost.IsSilenteraAllowed());

                // spawn NPC's or sieges
                if (isSiegeAllowed)
                    StartSiege(outpost.GetLocationId());
                else
                    SpawnNpcs(outpost.GetLocationId(), outpost.GetRace(), SiegeModType.PEACE);
            }
        }
    }

    public void SpawnNpcs(int siegeLocationId, SiegeRace race, SiegeModType type)
    {
        List<SpawnGroup> siegeSpawns = DataManager.SPAWNS_DATA.GetSiegeSpawnsByLocId(siegeLocationId);
        if (siegeSpawns == null)
            return;
        foreach (SpawnGroup group in siegeSpawns)
        {
            foreach (SpawnTemplate template in group.GetSpawnTemplates())
            {
                SiegeSpawnTemplate siegetemplate = (SiegeSpawnTemplate)template;
                if (siegetemplate.GetSiegeRace() == race && siegetemplate.GetSiegeModType() == type)
                {
                    Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(siegetemplate, 1);
                }
            }
        }
    }

    public void DeSpawnNpcs(int siegeLocationId)
    {
        // iterate over an array copy, since onDelete directly modifies the underlying collection
        foreach (object siegeNpc in World.World.GetInstance().GetLocalSiegeNpcs(siegeLocationId).ToArray())
            ((SiegeNpc)siegeNpc).GetController().Delete();
    }

    public bool IsRespawnAllowed(Npc npc)
    {
        if (npc is SiegeNpc)
        {
            FortressLocation fort = GetFortress(((SiegeNpc)npc).GetSiegeId());
            if (fort != null)
            {
                if (fort.IsVulnerable())
                    return false;
                else if (fort.GetNextState() == SiegeLocation.STATE_VULNERABLE)
                    return npc.GetSpawn().GetRespawnTime() < GetSecondsUntilNextFortressState();
            }
        }
        return true;
    }

    public void BroadcastUpdate(SiegeLocation loc)
    {
        Influence.GetInstance().RecalculateInfluence();
        SM_SIEGE_LOCATION_INFO pkt1 = new SM_SIEGE_LOCATION_INFO(loc);
        SM_INFLUENCE_RATIO pkt2 = new SM_INFLUENCE_RATIO();
        World.World.GetInstance().ForEachPlayer(player =>
        {
            PacketSendUtility.SendPacket(player, pkt1);
            PacketSendUtility.SendPacket(player, pkt2);
        });
    }

    public void BroadcastStatusAndUpdate(OutpostLocation outpost, bool oldSilentraState)
    {
        SM_SYSTEM_MESSAGE info = null;
        if (oldSilentraState != outpost.IsSilenteraAllowed())
        {
            if (outpost.IsSilenteraAllowed())
                info = outpost.GetLocationId() == 2111 ? SM_SYSTEM_MESSAGE.STR_FIELDABYSS_LIGHTUNDERPASS_SPAWN()
                    : SM_SYSTEM_MESSAGE.STR_FIELDABYSS_DARKUNDERPASS_SPAWN();
            else
                info = outpost.GetLocationId() == 2111 ? SM_SYSTEM_MESSAGE.STR_FIELDABYSS_LIGHTUNDERPASS_DESPAWN()
                    : SM_SYSTEM_MESSAGE.STR_FIELDABYSS_DARKUNDERPASS_DESPAWN();
        }

        Broadcast(new SM_RIFT_ANNOUNCE(GetOutpost(3111).IsSilenteraAllowed(), GetOutpost(2111).IsSilenteraAllowed()), info);
    }

    private void Broadcast(SM_RIFT_ANNOUNCE rift, SM_SYSTEM_MESSAGE info)
    {
        World.World.GetInstance().ForEachPlayer(player =>
        {
            PacketSendUtility.SendPacket(player, rift);
            if (info != null && player.GetWorldType().Equals(WorldType.BALAUREA))
                PacketSendUtility.SendPacket(player, info);
        });
    }

    public FortressLocation FindFortress(int worldId, float x, float y, float z)
    {
        foreach (FortressLocation fortress in GetFortresses().Values)
        {
            if (fortress.GetWorldId() == worldId && fortress.IsInsideLocation(x, y, z))
                return fortress;
        }
        return null;
    }

    public void OnPlayerLogin(Player player)
    {
        // First part will be sent to all
        if (SiegeConfig.SIEGE_ENABLED)
        {
            PacketSendUtility.SendPacket(player, new SM_INFLUENCE_RATIO());
            PacketSendUtility.SendPacket(player, new SM_SIEGE_LOCATION_INFO());
            PacketSendUtility.SendPacket(player, new SM_AFTER_SIEGE_LOCINFO_475());
            PacketSendUtility.SendPacket(player, new SM_RIFT_ANNOUNCE(GetOutpost(3111).IsSilenteraAllowed(), GetOutpost(2111).IsSilenteraAllowed()));
        }
    }

    public void OnEnterSiegeWorld(Player player)
    {
        // Second part only for siege world
        Dictionary<int, SiegeLocation> worldLocations = new Dictionary<int, SiegeLocation>();
        Dictionary<int, ArtifactLocation> worldArtifacts = new Dictionary<int, ArtifactLocation>();

        foreach (SiegeLocation location in GetSiegeLocations().Values)
            if (location.GetWorldId() == player.GetWorldId())
                worldLocations[location.GetLocationId()] = location;

        foreach (ArtifactLocation artifact in GetArtifacts().Values)
            if (artifact.GetWorldId() == player.GetWorldId())
                worldArtifacts[artifact.GetLocationId()] = artifact;

        PacketSendUtility.SendPacket(player, new SM_SHIELD_EFFECT(worldLocations.Values));
        PacketSendUtility.SendPacket(player, new SM_ABYSS_ARTIFACT_INFO3(worldArtifacts.Values));
    }

    public void OnAbyssPointsAdded(Player player, VisibleObject obj, int abyssPoints)
    {
        if (obj is Player || obj is SiegeNpc siegeNpc && siegeNpc.GetSpawn().GetSiegeModType() != SiegeModType.PEACE)
            foreach (global::Aion.GameServer.Services.Siege.Siege a in activeSieges.Values)
                a.OnAbyssPointsAdded(player, abyssPoints);
    }

    public int GetSiegeIdByLocId(int locId)
    {
        switch (locId)
        {
            case 49:
            case 61:
                return 1011; // Divine Fortress
            case 36:
            case 54:
                return 1131; // Siel's Western Fortress
            case 37:
            case 55:
                return 1132; // Siel's Eastern Fortress
            case 39:
            case 56:
                return 1141; // Sulfur Archipelago
            case 44:
            case 62:
                return 1211; // Roah Fortress
            case 45:
            case 57:
            case 72:
            case 75:
                return 1221; // Krotan Refuge
            case 46:
            case 58:
            case 73:
            case 76:
                return 1231; // Kysis Fortress
            case 47:
            case 59:
            case 74:
            case 77:
                return 1241; // Miren Fortress
            case 48:
            case 60:
                return 1251; // Asteria Fortress
            case 90:
                return 2011; // Temple of Scales
            case 91:
                return 2021; // Altar of Avarice
            case 93:
                return 3011; // Vorgaltem Citadel
            case 94:
                return 3021; // Crimson Temple
            case 322:
            case 323:
            case 358:
            case 359:
                return 7011; // Wealhtheow Fortress
            case 316:
            case 317:
            case 368:
            case 369:
                return 7012; // Hero's Fall Artifact
            case 370:
            case 371:
                return 7013; // Ashen Glade Artifact
            case 372:
            case 373:
                return 7014; // Molten Cliffs Artifact
            default:
                return 0;
        }
    }

    public HashSet<Player> GetRvrEventPlayers()
    {
        return rvrEventPlayers;
    }

    /// <summary>Checks if the player is in an RVR event list, if not the player is added.</summary>
    public void CheckRvrEventPlayer(Player player)
    {
        if (player != null && !rvrEventPlayers.Contains(player))
            rvrEventPlayers.Add(player);
    }

    public void ClearRvrEventPlayers()
    {
        rvrEventPlayers = new HashSet<Player>();
    }

    /// <summary>Modifies the original cron expression to add additional time for preparations. Five minutes for regular fortress sieges. Ten minutes for Panesterra fortress sieges.</summary>
    private string GetPreparationCronString(string siegeTime, int fortressId)
    {
        try
        {
            string[] cronParts = siegeTime.Split(" ");
            sbyte minutes = sbyte.Parse(cronParts[1]);
            sbyte hours = sbyte.Parse(cronParts[2]);
            minutes -= (sbyte)(fortressId < 10000 ? 5 : 10);
            if (minutes < 0)
            {
                minutes += 60;
                hours -= 1;
                if (hours < 0)
                    // Java parity: services/SiegeService.java throws UnsupportedOperationException (preparation over midnight unsupported)
                    throw new NotSupportedException("Failed converting cron expression: " + siegeTime + "\nPreparation over midnight not supported.");
            }
            cronParts[1] = minutes.ToString();
            cronParts[2] = hours.ToString();
            return string.Join(" ", cronParts);
        }
        catch (FormatException e)
        {
            // Java parity: services/SiegeService.java catch(NumberFormatException) throws UnsupportedOperationException
            throw new NotSupportedException("Failed converting cron expression: " + siegeTime, e);
        }
    }
}
