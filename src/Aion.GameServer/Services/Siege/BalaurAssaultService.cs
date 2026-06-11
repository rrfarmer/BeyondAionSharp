using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Assemblednpc;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Assemblednpc;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/BalaurAssaultService (synchro2, Luzien, Estrayl). Schedules/tracks Balaur assaults on fortresses (1-15 min) and artifacts (3-48 h): onSiegeStart/Finish, calculateFortressAssault (vulnerability/per-map limits/influence chance), startAssault, newAssault (build FortressAssault/ArtifactAssault), spawnDredgion (assembled-npc carrier broadcast). ConcurrentHashMap->ConcurrentDictionary; map.remove(k).call->TryRemove(out)+call; Siege<?>/Siege<? extends SiegeLocation>->Siege<SiegeLocation>; instanceof X x->is X x; getClass().getSimpleName()->GetType().Name; forEach->foreach; IllegalArgument->Argument. FortressAssault/FortressSiege/Influence/AssembledNpc red-tolerated.</summary>
public class BalaurAssaultService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("SIEGE_LOG");
    private readonly ConcurrentDictionary<int, FortressAssault> fortressAssaults = new ConcurrentDictionary<int, FortressAssault>();
    private readonly ConcurrentDictionary<int, ArtifactAssault> artifactAssaults = new ConcurrentDictionary<int, ArtifactAssault>();

    public static BalaurAssaultService GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    public void OnSiegeStart(Siege<SiegeLocation> siege)
    {
        if (siege is FortressSiege)
        {
            if (!CalculateFortressAssault(((FortressSiege)siege).GetSiegeLocation()))
                return;
            NewAssault(siege, Rnd.Get(60, 900)); // between 1 and 15 minutes
        }
        else if (siege is ArtifactSiege)
        {
            if (artifactAssaults.ContainsKey(siege.GetSiegeLocation().GetLocationId()) || siege.GetSiegeLocation().GetRace() == SiegeRace.BALAUR)
                return;
            NewAssault(siege, Rnd.Get(10800, 172800)); // between 3 and 48 hours
        }
    }

    public void OnSiegeFinish(Siege<SiegeLocation> siege)
    {
        int locId = siege.GetSiegeLocationId();
        bool isBossKilled = siege.IsBossKilled();
        if (fortressAssaults.ContainsKey(locId))
        {
            fortressAssaults.TryRemove(locId, out FortressAssault fa);
            fa.FinishAssault(isBossKilled);
            if (isBossKilled && siege.GetSiegeLocation().GetRace().Equals(SiegeRace.BALAUR))
                log.LogInformation(siege + " has been captured by Balaur assault!");
            else
                log.LogInformation(siege + " Balaur assault finished without capture!");
        }
        else if (artifactAssaults.ContainsKey(locId))
        {
            artifactAssaults.TryRemove(locId, out ArtifactAssault aa);
            aa.FinishAssault(isBossKilled);
            if (isBossKilled && siege.GetSiegeLocation().GetRace().Equals(SiegeRace.BALAUR))
                log.LogInformation(siege + " has been captured by Balaur assault!");
            else
                log.LogInformation(siege + " Balaur assault finished without capture!");
        }
    }

    private bool CalculateFortressAssault(FortressLocation fortress)
    {
        if (fortress.GetRace() == SiegeRace.BALAUR || !fortress.IsVulnerable())
            return false;

        bool isBalaurea = fortress.GetWorldId() == 210050000 || fortress.GetWorldId() == 220070000;

        if (fortressAssaults.ContainsKey(fortress.GetLocationId()))
            return false;

        int count = 0;
        foreach (FortressAssault fa in fortressAssaults.Values)
        {
            if (fa.GetWorldId() == fortress.GetWorldId())
                count++;
        }
        if (count >= (isBalaurea ? 1 : 2)) // Allow only 2 Balaur attacks per map, 1 per Balaurea map
            return false;

        float influence = fortress.GetRace() == SiegeRace.ASMODIANS ? Influence.GetInstance().GetAsmodianInfluenceRate()
            : Influence.GetInstance().GetElyosInfluenceRate();

        return Rnd.Chance() < influence * 100f * SiegeConfig.BALAUR_ASSAULT_RATE;
    }

    public bool StartAssault(int location, int delay)
    {
        Siege<SiegeLocation> siege = SiegeService.GetInstance().GetSiege(location);
        if (siege == null || fortressAssaults.ContainsKey(location) || artifactAssaults.ContainsKey(location))
        {
            return false;
        }
        NewAssault(siege, delay);
        return true;
    }

    private void NewAssault(Siege<SiegeLocation> siege, int delay)
    {
        if (siege is FortressSiege fortressSiege)
        {
            FortressAssault assault = new FortressAssault(fortressSiege);
            assault.StartAssault(delay);
            fortressAssaults[siege.GetSiegeLocationId()] = assault;
        }
        else if (siege is ArtifactSiege artifactSiege)
        {
            ArtifactAssault assault = new ArtifactAssault(artifactSiege);
            assault.StartAssault(delay);
            artifactAssaults[siege.GetSiegeLocationId()] = assault;
        }
        else
        {
            throw new ArgumentException("Unsupported fortress siege type: " + siege.GetType().Name);
        }
        if (LoggingConfig.LOG_SIEGE)
            log.LogInformation("Scheduled assault of " + siege + " in " + delay + " seconds");
    }

    public void SpawnDredgion(int spawnId)
    {
        AssembledNpcTemplate template = DataManager.ASSEMBLED_NPC_DATA.GetAssembledNpcTemplate(spawnId);
        List<AssembledNpcPart> assembledParts = new List<AssembledNpcPart>();
        foreach (var part in template.GetAssembledNpcPartTemplates())
            assembledParts.Add(new AssembledNpcPart(IDFactory.GetInstance().NextId(), part));

        AssembledNpc npc = new AssembledNpc(template.GetRouteId(), template.GetMapId(), template.GetLiveTime(), assembledParts);
        World.GetInstance().ForEachPlayer(p =>
        {
            PacketSendUtility.SendPacket(p, new SM_NPC_ASSEMBLER(npc));
            PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_ABYSS_CARRIER_SPAWN());
        });
    }

    /// <summary>
    /// Returns the FortressAssault object or null if none is active
    /// </summary>
    public FortressAssault GetFortressAssaultBySiegeId(int siegeId)
    {
        return fortressAssaults.GetValueOrDefault(siegeId);
    }

    private static class SingletonHolder
    {
        internal static readonly BalaurAssaultService INSTANCE = new BalaurAssaultService();
    }
}
