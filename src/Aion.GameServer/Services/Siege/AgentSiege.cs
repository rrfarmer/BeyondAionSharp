using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/AgentSiege (Estrayl, Sykra) extends Siege&lt;AgentLocation&gt;. Levinshor agent (Mastarius/Veille) siege: timed delayStart (recursive 60s task ramps startProgress to 10 -> announce/quest/spawn), onSiegeFinish (capture base 6113 for winner + rewards), broadcastAgentSpawn, distributeQuest (zone-gated quest start), spawnSiegeNpcs (BALAUR/SIEGE boss init), initNpc (race-keyed assignment), AP accrual. schedule(Runnable,ms)->Schedule ct-lambda (return interrupts->return CompletedTask); switch-on-SiegeRace; ZoneName.get->Get. AgentLocation/SiegeNpc/SiegeSpawnTemplate/BaseOccupier/ZoneName red-tolerated.</summary>
public class AgentSiege : Siege<AgentLocation>
{
    private byte startProgress = 1;
    private SiegeNpc masta, veille;
    private SiegeRace? winner; // Java enum field defaults to null -> nullable

    public AgentSiege(AgentLocation siegeLocation)
        : base(siegeLocation)
    {
    }

    protected override void OnSiegeStart()
    {
        PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_GODELITE_TIME_01());
        GetSiegeLocation().SetVulnerable(true);
        DelayStart();
    }

    private void DelayStart()
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            startProgress++;
            if (startProgress == 5)
            {
                PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_GODELITE_TIME_02());
            }
            else if (startProgress >= 10)
            {
                BroadcastAgentSpawn();
                DistributeQuest();
                SpawnSiegeNpcs(); // Should initialize Agents and their flags
                return ValueTask.CompletedTask; // Interrupts the task
            }
            DelayStart();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(60000));
    }

    protected override void OnSiegeFinish()
    {
        PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_GODELITE_TIME_03());
        GetSiegeLocation().SetVulnerable(false);
        DespawnSiegeNpcs();
        if (winner == null)
            return;
        BaseOccupier winnerType = winner == SiegeRace.ELYOS ? BaseOccupier.ELYOS : BaseOccupier.ASMODIANS;
        BaseService.GetInstance().Capture(6113, winnerType);
        SiegeRace looser = winner == SiegeRace.ELYOS ? SiegeRace.ASMODIANS : SiegeRace.ELYOS;
        SendRewardsToParticipants(GetSiegeCounter().GetRaceCounter(winner.Value), SiegeResult.OCCUPY);
        SendRewardsToParticipants(GetSiegeCounter().GetRaceCounter(looser), SiegeResult.FAIL);
    }

    private void BroadcastAgentSpawn()
    {
        WorldMapInstance levinshorWorldInstance = Aion.GameServer.World.World.GetInstance().GetWorldMap(600100000).GetMainWorldMapInstance();
        if (levinshorWorldInstance != null)
            PacketSendUtility.BroadcastToMap(levinshorWorldInstance, SM_SYSTEM_MESSAGE.STR_MSG_LDF4_Advance_GodElite());
    }

    private void DistributeQuest()
    {
        foreach (Player player in Aion.GameServer.World.World.GetInstance().GetWorldMap(600100000).GetMainWorldMapInstance().GetPlayersInside())
        {
            if (player.IsInsideZone(ZoneName.Get("DRAGON_LORDS_SHRINE_600100000")) || player.IsInsideZone(ZoneName.Get("FLAMEBERTH_DOWNS_600100000")))
            {
                int questId = player.GetRace() == Race.ELYOS ? 13744 : 23744;
                QuestState qs = player.GetQuestStateList().GetQuestState(questId);
                if (qs == null || qs.IsStartable())
                    QuestService.StartQuest(new QuestEnv(null, player, questId));
            }
        }
    }

    public void SpawnSiegeNpcs()
    {
        List<SpawnGroup> siegeSpawns = DataManager.SPAWNS_DATA.GetSiegeSpawnsByLocId(GetSiegeLocationId());
        if (siegeSpawns == null)
            return;
        foreach (SpawnGroup group in siegeSpawns)
        {
            foreach (SpawnTemplate template in group.GetSpawnTemplates())
            {
                SiegeSpawnTemplate siegetemplate = (SiegeSpawnTemplate)template;
                if (siegetemplate.GetSiegeRace() == SiegeRace.BALAUR && siegetemplate.GetSiegeModType() == SiegeModType.SIEGE)
                {
                    SiegeNpc npc = (SiegeNpc)Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(siegetemplate, 1);
                    if (npc.GetSpawn().GetHandlerType() == SpawnHandlerType.BOSS)
                        InitNpc(npc);
                }
            }
        }
    }

    public void DespawnSiegeNpcs()
    {
        ICollection<SiegeNpc> npcs = Aion.GameServer.World.World.GetInstance().GetLocalSiegeNpcs(GetSiegeLocationId());
        foreach (SiegeNpc npc in npcs)
        {
            if (npc != null)
                npc.GetController().DeleteIfAliveOrCancelRespawn();
        }
    }

    private void InitNpc(SiegeNpc target)
    {
        switch (target.GetRace())
        {
            case Race.GHENCHMAN_LIGHT:
                if (veille != null)
                    throw new SiegeException("Tried to init Veille twice!");
                veille = target;
                break;
            case Race.GHENCHMAN_DARK:
                if (masta != null)
                    throw new SiegeException("Tried to init Mastarius twice!");
                masta = target;
                break;
            default:
                throw new SiegeException("Tried to init a npc with not supported TemplateType " + target.GetNpcTemplateType() + " for agent fight!");
        }
    }

    public void SetWinnerRace(SiegeRace race)
    {
        winner = race;
    }

    public override bool IsEndless()
    {
        return false;
    }

    public override void OnAbyssPointsAdded(Player player, int abyssPoints)
    {
        if (startProgress >= 10 && GetSiegeLocation().IsVulnerable()
                && (player.IsInsideZone(ZoneName.Get("FLAMEBERTH_DOWNS_600100000")) || player.IsInsideZone(ZoneName.Get("DRAGON_LORDS_SHRINE_600100000"))))
            GetSiegeCounter().AddAbyssPoints(player, abyssPoints);
    }
}
