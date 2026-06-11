using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Panesterra.Ahserion;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using static Aion.GameServer.Services.Panesterra.Ahserion.PanesterraFaction;

namespace Aion.GameServer.Services.Panesterra;

/// <summary>Java parity: services/panesterra/PanesterraService (Estrayl). Panesterra fortress siege + Ahserion raid orchestration: prepare/start/stopFortressSiege (base state transitions, advance corridors), createTeams/removeTeams per faction, start/stopAhserionRaid, onEnterPanesterra (faction assignment/teleport), team queries, teleportToStart/EventLocation, revive. Singleton; ConcurrentHashMap->ConcurrentDictionary; static-import enum->using static; switch-arrow->switch statement; switch-expression w/ null-case->switch+default; Stream.of/anyMatch/map/findFirst.orElse->LINQ; getType()->GetType_(); getByFortressId->PanesterraFactionExtensions; Rnd.nextBoolean->NextBoolean. Base/Siege/WorldMapType/SpawnEngine/PanesterraTeam red-tolerated.</summary>
public class PanesterraService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("SIEGE_LOG");

    private readonly ConcurrentDictionary<PanesterraFaction, PanesterraTeam> activeFactionTeams = new ConcurrentDictionary<PanesterraFaction, PanesterraTeam>();

    /// <summary>
    /// 1. Stop outer bases 2. Start faction camps 3. Despawn default corridors 4. Spawn corridors
    /// </summary>
    public void PrepareFortressSiege(FortressLocation loc)
    {
        CreateTeams(loc.GetLocationId());
        SpawnAdvanceCorridors();
        PrepareBases(loc.GetTemplate().GetSiegeRelatedBases());
    }

    private void PrepareBases(SiegeRelatedBases relatedBases)
    {
        if (relatedBases == null)
            return;

        foreach (var baseLoc in relatedBases.GetBaseIds().Select(id => BaseService.GetInstance().GetBaseLocation(id)))
        {
            switch (baseLoc.GetType_())
            {
                case BaseType.PANESTERRA:
                    BaseService.GetInstance().Capture(baseLoc.GetId(), BaseOccupier.PEACE);
                    break;
                case BaseType.PANESTERRA_ARTIFACT:
                    BaseService.GetInstance().Start(baseLoc.GetId());
                    break;
                case BaseType.PANESTERRA_FACTION_CAMP:
                    BaseService.GetInstance().Capture(baseLoc.GetId(), baseLoc.GetTemplate().GetDefaultOccupier());
                    break;
            }
        }
    }

    public void StartFortressSiege(FortressLocation loc)
    {
        SiegeRelatedBases relatedBases = loc.GetTemplate().GetSiegeRelatedBases();
        if (relatedBases != null)
        {
            foreach (var baseLoc in relatedBases.GetBaseIds().Select(id => BaseService.GetInstance().GetBaseLocation(id))
                .Where(baseLoc => baseLoc.GetType_() == BaseType.PANESTERRA_ARTIFACT))
                BaseService.GetInstance().Capture(baseLoc.GetId(), BaseOccupier.BALAUR);
        }
    }

    public void StopFortressSiege(FortressLocation loc)
    {
        // Remove Teams
        switch (loc.GetLocationId())
        {
            case 10111:
                RemoveTeams(IVY_TEMPLE, HIGHLAND_TEMPLE, ALPINE_TEMPLE, GRANDWEIR_TEMPLE);
                break;
            case 10211:
                RemoveTeams(NOERREN_TEMPLE, BOREALIS_TEMPLE, MYRKREN_TEMPLE, GLUMVEILEN_TEMPLE);
                break;
            case 10311:
                RemoveTeams(MEMORIA_TEMPLE, SYBILLINE_TEMPLE, AUSTERITY_TEMPLE, SERENITY_TEMPLE);
                break;
            case 10411:
                RemoveTeams(NECROLUCE_TEMPLE, ESMERAUDUS_TEMPLE, VOLTAIC_TEMPLE, ILLUMINATUS_TEMPLE);
                break;
        }
        // Change base states
        SiegeRelatedBases relatedBases = loc.GetTemplate().GetSiegeRelatedBases();
        if (relatedBases != null)
        {
            foreach (var baseLoc in relatedBases.GetBaseIds().Select(id => BaseService.GetInstance().GetBaseLocation(id)))
            {
                switch (baseLoc.GetType_())
                {
                    case BaseType.PANESTERRA:
                        BaseService.GetInstance().Capture(baseLoc.GetId(), BaseOccupier.BALAUR);
                        break;
                    case BaseType.PANESTERRA_ARTIFACT:
                        BaseService.GetInstance().Stop(baseLoc.GetId());
                        break;
                    case BaseType.PANESTERRA_FACTION_CAMP:
                        BaseService.GetInstance().Capture(baseLoc.GetId(), BaseOccupier.PEACE);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Up to 100 players from each faction (1-Star Officer+) can apply; spawns advance corridors with rank-gated static ids.
    /// </summary>
    private void SpawnAdvanceCorridors()
    {
        // Elyos
        PacketSendUtility.BroadcastToMap(World.GetInstance().GetWorldMap(110070000).GetMainWorldMapInstance(),
            SM_SYSTEM_MESSAGE.STR_MSG_SVS_INVADE_DIRECT_PORTAL_OPEN());
        // Governor exclusive
        SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(110070000, 730940, 503.624f, 460.202f, 132.081f, (byte)90), 257);
        // Advance Corridor for Contributors | Officer 5-Star to Commander
        SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(110070000, 730942, 490.262f, 409.850f, 126.79f, (byte)90), 256);
        // Walk of Honor | Officer 1-Star to 4-Star
        SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(110070000, 731193, 518.142f, 409.967f, 126.79f, (byte)90), 252);
        // Asmodians
        PacketSendUtility.BroadcastToMap(World.GetInstance().GetWorldMap(120080000).GetMainWorldMapInstance(),
            SM_SYSTEM_MESSAGE.STR_MSG_SVS_INVADE_DIRECT_PORTAL_OPEN());
        // Governor exclusive
        SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(120080000, 730941, 342.298f, 251.135f, 98.553f, (byte)0), 338);
        // Advance Corridor for Contributors | Officer 5-Star to Commander
        SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(120080000, 730943, 393.321f, 236.963f, 93.113f, (byte)0), 337);
        // Walk of Glory | Officer 1-Star to 4-Star
        SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(120080000, 731194, 393.476f, 263.704f, 93.113f, (byte)0), 336);
    }

    private void SpawnCorridor(SpawnTemplate template, int staticId)
    {
        template.SetStaticId(staticId);
        SpawnEngine.SpawnObject(template, 1);
    }

    public void StartAhserionRaid()
    {
        if (new[] { 10111, 10211, 10311, 10411 }.Any(id => SiegeService.GetInstance().GetSiege(id) != null))
        {
            log.LogError("Ahserion raid cannot be started while any Panesterra fortress is under siege.");
            return;
        }
        CreateTeams(-1);
        SpawnEngine.SpawnObject(SpawnEngine.NewSingleTimeSpawn(110070000, 802223, 485.692f, 401.079f, 127.789f, (byte)0), 1);
        SpawnEngine.SpawnObject(SpawnEngine.NewSingleTimeSpawn(120080000, 802225, 400.772f, 231.517f, 93.113f, (byte)30), 1);
        AhserionRaid.GetInstance().Start();
    }

    public void StopAhserionRaid()
    {
        AhserionRaid.GetInstance().Stop();
        if (!activeFactionTeams.IsEmpty)
        {
            foreach (PanesterraTeam team in activeFactionTeams.Values)
            {
                team.SetIsEliminated(true);
                team.MoveTeamMembersToOriginPosition();
            }
            activeFactionTeams.Clear();
        }
    }

    public PanesterraTeam HandleTeamElimination(PanesterraFaction faction)
    {
        PanesterraTeam team = activeFactionTeams.GetValueOrDefault(faction);
        if (team == null)
            return null; // Using the //base command

        team.SetIsEliminated(true);
        team.MoveTeamMembersToOriginPosition();
        return team;
    }

    private void CreateTeams(int siegeId)
    {
        switch (siegeId)
        {
            case -1: // Transidium Annex
                if (SiegeService.GetInstance().GetSiegeLocation(10111).GetRace() != SiegeRace.BALAUR)
                    activeFactionTeams[BELUS] = new PanesterraTeam(BELUS);
                if (SiegeService.GetInstance().GetSiegeLocation(10211).GetRace() != SiegeRace.BALAUR)
                    activeFactionTeams[ASPIDA] = new PanesterraTeam(ASPIDA);
                if (SiegeService.GetInstance().GetSiegeLocation(10311).GetRace() != SiegeRace.BALAUR)
                    activeFactionTeams[ATANATOS] = new PanesterraTeam(ATANATOS);
                if (SiegeService.GetInstance().GetSiegeLocation(10411).GetRace() != SiegeRace.BALAUR)
                    activeFactionTeams[DISILLON] = new PanesterraTeam(DISILLON);
                break;
            case 10111: // Belus
                activeFactionTeams[IVY_TEMPLE] = new PanesterraTeam(IVY_TEMPLE);
                activeFactionTeams[HIGHLAND_TEMPLE] = new PanesterraTeam(HIGHLAND_TEMPLE);
                activeFactionTeams[ALPINE_TEMPLE] = new PanesterraTeam(ALPINE_TEMPLE);
                activeFactionTeams[GRANDWEIR_TEMPLE] = new PanesterraTeam(GRANDWEIR_TEMPLE);
                break;
            case 10211: // Aspida
                activeFactionTeams[NOERREN_TEMPLE] = new PanesterraTeam(NOERREN_TEMPLE);
                activeFactionTeams[BOREALIS_TEMPLE] = new PanesterraTeam(BOREALIS_TEMPLE);
                activeFactionTeams[MYRKREN_TEMPLE] = new PanesterraTeam(MYRKREN_TEMPLE);
                activeFactionTeams[GLUMVEILEN_TEMPLE] = new PanesterraTeam(GLUMVEILEN_TEMPLE);
                break;
            case 10311: // Atanatos
                activeFactionTeams[MEMORIA_TEMPLE] = new PanesterraTeam(MEMORIA_TEMPLE);
                activeFactionTeams[SYBILLINE_TEMPLE] = new PanesterraTeam(SYBILLINE_TEMPLE);
                activeFactionTeams[AUSTERITY_TEMPLE] = new PanesterraTeam(AUSTERITY_TEMPLE);
                activeFactionTeams[SERENITY_TEMPLE] = new PanesterraTeam(SERENITY_TEMPLE);
                break;
            case 10411: // Disillon
                activeFactionTeams[NECROLUCE_TEMPLE] = new PanesterraTeam(NECROLUCE_TEMPLE);
                activeFactionTeams[ESMERAUDUS_TEMPLE] = new PanesterraTeam(ESMERAUDUS_TEMPLE);
                activeFactionTeams[VOLTAIC_TEMPLE] = new PanesterraTeam(VOLTAIC_TEMPLE);
                activeFactionTeams[ILLUMINATUS_TEMPLE] = new PanesterraTeam(ILLUMINATUS_TEMPLE);
                break;
        }
    }

    private void RemoveTeams(params PanesterraFaction[] factions)
    {
        foreach (PanesterraFaction faction in factions)
        {
            activeFactionTeams.TryRemove(faction, out PanesterraTeam team);
            team.MoveTeamMembersToOriginPosition();
        }
    }

    private void SpawnAhserionCorridors(int fortressId)
    {
        switch (fortressId)
        {
            case 10111:
                SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(400020000, 802219, 1024.159f, 1076.24f, 1530.2688f, (byte)90), 0);
                break;
            case 10211:
                SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(400040000, 802221, 1024.159f, 1076.24f, 1530.2688f, (byte)90), 0);
                break;
            case 10311:
                SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(400050000, 802223, 1024.159f, 1076.24f, 1530.2688f, (byte)90), 0);
                break;
            case 10411:
                SpawnCorridor(SpawnEngine.NewSingleTimeSpawn(400060000, 802225, 1024.159f, 1076.24f, 1530.2688f, (byte)90), 0);
                break;
        }
    }

    public void OnEnterPanesterra(Player player)
    {
        int siegeId = GetSiegeId(player.GetWorldId());
        if (siegeId == 0)
            return;
        // Player is in Transidium Annex or on a map with an active siege
        if (siegeId == -1 || SiegeService.GetInstance().IsSiegeInProgress(siegeId))
        {
            PanesterraTeam team = GetTeam(player);
            if (team == null)
                TeleportService.MoveToBindLocation(player);
            else if (team.IsEliminated())
                team.MovePlayerToOriginPosition(player);
            else
                player.SetPanesterraFaction(team.GetFaction());
        }
        else
        {
            // Check if the player's faction owns any related fortress
            PanesterraFaction faction = new[] { 10111, 10211, 10311, 10411 }
                .Where(id => SiegeService.GetInstance().GetFortress(id).GetRace() == SiegeRace.GetByRace(player.GetRace()))
                .Select(id => PanesterraFactionExtensions.GetByFortressId(id)).DefaultIfEmpty(PEACE).First();

            if (faction == PEACE)
                TeleportService.MoveToBindLocation(player);

            player.SetPanesterraFaction(faction);
        }
    }

    private int GetSiegeId(int worldId)
    {
        switch (WorldMapType.GetWorld(worldId))
        {
            case WorldMapType.BELUS:
                return 10111; // Belus
            case WorldMapType.TRANSIDIUM_ANNEX:
                return -1; // Transidium Annex
            case WorldMapType.ASPIDA:
                return 10211; // Aspida
            case WorldMapType.ATANATOS:
                return 10311; // Atanatos
            case WorldMapType.DISILLON:
                return 10411; // Disillon
            default:
                return 0;
        }
    }

    public bool IsAhserionRaidStarted()
    {
        return AhserionRaid.GetInstance().IsStarted();
    }

    public int GetTeamMemberCount(PanesterraFaction faction)
    {
        PanesterraTeam team = activeFactionTeams.GetValueOrDefault(faction);
        return team != null ? team.GetMemberCount() : 0;
    }

    public PanesterraTeam GetTeam(PanesterraFaction faction)
    {
        return activeFactionTeams.GetValueOrDefault(faction);
    }

    public PanesterraTeam GetTeam(Player player)
    {
        foreach (PanesterraTeam team in activeFactionTeams.Values)
        {
            if (team.IsTeamMember(player.GetObjectId()))
                return team;
        }
        return null;
    }

    public bool TeleportToStartPosition(Player player)
    {
        if (!WorldMapType.IsPanesterraMap(player.GetWorldId()))
            return false;

        PanesterraTeam team = GetTeam(player);
        if (team != null && !team.IsEliminated())
        {
            team.MovePlayerToStartPosition(player);
            return true;
        }
        return false;
    }

    // TODO: Event START
    public bool ReviveInEventLocation(Player player)
    {
        if (!WorldMapType.IsPanesterraMap(player.GetWorldId()))
            return false;

        TeleportToEventLocation(player);
        return true;
    }

    public void TeleportToEventLocation(Player player)
    {
        Teleport(player);
        PanesterraService.GetInstance().OnEnterPanesterra(player);
    }

    private void Teleport(Player player)
    {
        switch (player.GetRace())
        {
            case Race.ELYOS:
                {
                    // North + South
                    WorldPosition pos = Rnd.NextBoolean() ? new WorldPosition(400020000, 11.173f, 1024.187f, 1428.60f, (byte)0)
                        : new WorldPosition(400020000, 2037.754f, 1023.808f, 1428.60f, (byte)0);
                    TeleportService.TeleportTo(player, pos);
                    break;
                }
            case Race.ASMODIANS:
                {
                    // West + East
                    WorldPosition pos = Rnd.NextBoolean() ? new WorldPosition(400020000, 1023.702f, 10.531f, 1428.60f, (byte)90)
                        : new WorldPosition(400020000, 1024.310f, 2036.593f, 1428.60f, (byte)90);
                    TeleportService.TeleportTo(player, pos);
                    break;
                }
        }
    }
    // TODO: Event END

    public static PanesterraService GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    private static class SingletonHolder
    {
        internal static readonly PanesterraService INSTANCE = new PanesterraService();
    }
}
