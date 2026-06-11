using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Panesterra;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Spawnengine;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;
using static Aion.GameServer.Services.Panesterra.Ahserion.PanesterraFaction;

namespace Aion.GameServer.Services.Panesterra.Ahserion;

/// <summary>Java parity: services/panesterra/ahserion/AhserionRaid (Yeats, Neon, Estrayl). Singleton orchestrating the Ahserion's Flight raid: start/stop, cleanUp (clear instance 400030000), startInstanceTimer (30s fixed-rate progress with staged spawns/door-open/alarms - anonymous stateful Runnable -> nested ProgressRunnable), spawnRaid/spawnStage, corridor-shield destruction -> team elimination + consolation reward, boss-killed handling (winner + 15min cleanup). AtomicBoolean; Future->ScheduledTask; List.of->new List; static-import enum->using static; values()->Enum.GetValues; instanceof X x->is X x; method-ref this::stop->ct-lambda; faction.ordinal()->(int)faction; eliminatedFaction nullable. SpawnEngine/WalkManager/AhserionsFlightSpawnTemplate red-tolerated.</summary>
public class AhserionRaid
{
    private readonly List<PanesterraFaction> factions = new List<PanesterraFaction> { BELUS, ASPIDA, ATANATOS, DISILLON };
    private readonly AtomicBoolean isStarted = new AtomicBoolean();
    private PanesterraTeam winner;
    private ScheduledTask progressTask;

    public static AhserionRaid GetInstance()
    {
        return SingletonHolder.instance;
    }

    public void Start()
    {
        if (isStarted.CompareAndSet(false, true))
        {
            SpawnRaid();
            StartInstanceTimer();
        }
    }

    public void Stop()
    {
        if (!isStarted.CompareAndSet(true, false))
            return;
        winner = null;
        CancelProgressTask();
        CleanUp();
    }

    private void CleanUp()
    {
        foreach (VisibleObject obj in World.GetInstance().GetWorldMap(400030000).GetMainWorldMapInstance())
        {
            if (obj is Player player)
            {
                if (!player.IsStaff())
                    TeleportService.MoveToBindLocation(player);
            }
            else if (obj is StaticDoor door)
            {
                door.SetOpen(false);
            }
            else if (obj is Npc)
            {
                obj.GetController().Delete();
            }
        }
    }

    private void StartInstanceTimer()
    {
        progressTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRate(new ProgressRunnable(this), 30000, 30000);
    }

    private void CheckForIllegalMovement()
    {
        ForEachTeam(team =>
        {
            WorldPosition startPosition = team.GetStartPosition();
            team.ForEachMember(player =>
            {
                if (player.GetPosition().GetMapId() == 400030000
                    && !PositionUtil.IsInRange(player, startPosition.GetX(), startPosition.GetY(), startPosition.GetZ(), 81f))
                {
                    AuditLogger.Log(player, "bugged himself through the " + team.GetFaction() + " start door");
                    team.MovePlayerToStartPosition(player);
                }
            });
        });
    }

    private void SpawnRaid()
    {
        // spawn Barricades & Tank Fleets
        SpawnStage(0, BALAUR);
        SpawnStage(180, BALAUR);
        SpawnStage(181, BALAUR);
        SpawnStage(182, BALAUR);
        SpawnStage(183, BALAUR);

        // spawn flags & cannons for all registered teams
        ForEachTeam(team =>
        {
            SpawnStage(0, team.GetFaction());
            SpawnStage(1, team.GetFaction());
        });

        // spawn white flags for not existing teams
        if (PanesterraService.GetInstance().GetTeam(BELUS) == null)
        {
            SpawnTemplate template = SpawnEngine.NewSingleTimeSpawn(400030000, 804106, 282.73f, 289.1f, 687.38f, (byte)1);
            SpawnEngine.SpawnObject(template, 1);
        }
        if (PanesterraService.GetInstance().GetTeam(ASPIDA) == null)
        {
            SpawnTemplate template = SpawnEngine.NewSingleTimeSpawn(400030000, 804108, 282.49f, 739.62f, 689.66f, (byte)1);
            SpawnEngine.SpawnObject(template, 1);
        }
        if (PanesterraService.GetInstance().GetTeam(ATANATOS) == null)
        {
            SpawnTemplate template = SpawnEngine.NewSingleTimeSpawn(400030000, 804110, 734.06f, 740.75f, 681.16f, (byte)1);
            SpawnEngine.SpawnObject(template, 1);
        }
        if (PanesterraService.GetInstance().GetTeam(DISILLON) == null)
        {
            SpawnTemplate template = SpawnEngine.NewSingleTimeSpawn(400030000, 804112, 738.58f, 286.02f, 680.71f, (byte)1);
            SpawnEngine.SpawnObject(template, 1);
        }
    }

    public void SpawnStage(int stage, PanesterraFaction faction)
    {
        PanesterraTeam team = PanesterraService.GetInstance().GetTeam(faction);
        if (faction != BALAUR && (team == null || team.IsEliminated()))
            return;

        List<SpawnGroup> ahserionSpawns = DataManager.SPAWNS_DATA.GetAhserionSpawnByTeamId((int)faction);
        if (ahserionSpawns == null)
            return;

        foreach (SpawnGroup grp in ahserionSpawns)
        {
            foreach (SpawnTemplate template in grp.GetSpawnTemplates())
            {
                AhserionsFlightSpawnTemplate ahserionTemplate = (AhserionsFlightSpawnTemplate)template;
                if (ahserionTemplate.GetStage() == stage)
                {
                    Npc npc = (Npc)SpawnEngine.SpawnObject(ahserionTemplate, 1);
                    WalkManager.StartWalking((NpcAI)npc.GetAi());
                }
            }
        }
    }

    public void HandleCorridorShieldDestruction(int npcId)
    {
        if (!isStarted.Get())
            return;

        PanesterraFaction? eliminatedFaction = null;
        SpawnTemplate template = null;

        switch (npcId)
        {
            case 297306:
                eliminatedFaction = BELUS;
                template = SpawnEngine.NewSingleTimeSpawn(400030000, 804106, 282.73f, 289.1f, 687.38f, (byte)1);
                break;
            case 297307:
                eliminatedFaction = ASPIDA;
                template = SpawnEngine.NewSingleTimeSpawn(400030000, 804108, 282.49f, 739.62f, 689.66f, (byte)1);
                break;
            case 297308:
                eliminatedFaction = ATANATOS;
                template = SpawnEngine.NewSingleTimeSpawn(400030000, 804110, 734.06f, 740.75f, 681.16f, (byte)1);
                break;
            case 297309:
                eliminatedFaction = DISILLON;
                template = SpawnEngine.NewSingleTimeSpawn(400030000, 804112, 738.58f, 286.02f, 680.71f, (byte)1);
                break;
        }

        PanesterraTeam eliminatedTeam = PanesterraService.GetInstance().HandleTeamElimination(eliminatedFaction.Value);
        if (eliminatedTeam != null)
            SendConsolationReward(eliminatedTeam);
        DeleteNpcs(eliminatedFaction.Value, npcId + 1);
        SpawnEngine.SpawnObject(template, 1);
    }

    private void SendConsolationReward(PanesterraTeam eliminatedTeam)
    {
        eliminatedTeam.ForEachMember(p =>
        {
            SystemMailService.SendMail("Assault Forces", p.GetName(), "Raid Announcement", "We lost.", 186000409, 100, 0, LetterType.NORMAL);
        });
    }

    public void HandleBossKilled(Npc ahserion, PanesterraFaction winnerFaction)
    {
        winner = PanesterraService.GetInstance().GetTeam(winnerFaction);
        if (winner == null || winner.IsEliminated())
        {
            // something went wrong, remove all players from the map
            NullLoggerFactory.Instance.CreateLogger(nameof(AhserionRaid)).LogWarning("Ahserion got killed but winnerTeam is missing or eliminated. Skipping rewards.");
            Stop();
            return;
        }
        CancelProgressTask();
        foreach (PanesterraFaction faction in factions)
        {
            if (faction != winnerFaction)
                PanesterraService.GetInstance().HandleTeamElimination(faction);
        }

        ahserion.GetPosition().GetWorldMapInstance().ForEachNpc(npc => npc.GetController().DeleteIfAliveOrCancelRespawn());
        SpawnStage(10, winnerFaction); // Quest Npc "Pasha"
        ThreadPoolManager.GetInstance().Schedule(ct => { Stop(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(900000)); // 15min
    }

    private void SendMsg(SM_SYSTEM_MESSAGE msg)
    {
        PacketSendUtility.BroadcastToMap(World.GetInstance().GetWorldMap(400030000).GetMainWorldMapInstance(), msg);
    }

    private void DeleteNpcs(PanesterraFaction eliminatedFaction, int flagToDelete)
    {
        World.GetInstance().GetWorldMap(400030000).GetMainWorldMapInstance().ForEachNpc(npc =>
        {
            if (npc.GetNpcId() == flagToDelete || (!npc.IsFlag() && (npc.GetSpawn().GetStaticId() < 180 || npc.GetSpawn().GetStaticId() > 183)))
            {
                if (!npc.IsDead() && npc.GetSpawn() is AhserionsFlightSpawnTemplate template)
                {
                    if (template.GetFaction() == eliminatedFaction)
                        npc.GetController().Delete();
                }
            }
        });
    }

    public void ForEachTeam(Action<PanesterraTeam> consumer)
    {
        foreach (PanesterraFaction faction in factions)
        {
            PanesterraTeam team = PanesterraService.GetInstance().GetTeam(faction);
            if (team != null)
                consumer(team);
        }
    }

    private void CancelProgressTask()
    {
        if (progressTask != null && !progressTask.IsCancelled())
            progressTask.Cancel(true);
    }

    public bool IsStarted()
    {
        return isStarted.Get();
    }

    private sealed class ProgressRunnable : Runnable
    {
        private readonly AhserionRaid outer;
        private int progress;

        public ProgressRunnable(AhserionRaid outer)
        {
            this.outer = outer;
        }

        public void Run()
        {
            switch (++progress)
            {
                case 2:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_01());
                    break;
                case 4:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_02());
                    break;
                case 8:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_03());
                    break;
                case 12:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_04());
                    break;
                case 16:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_05());
                    break;
                case 18:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_06());
                    break;
                case 19:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_07());
                    foreach (PanesterraFaction faction in Enum.GetValues<PanesterraFaction>())
                        outer.SpawnStage(2, faction); // spawn mobs 30s before doors are opened
                    break;
                case 20:
                    outer.CheckForIllegalMovement();
                    World.GetInstance().GetWorldMap(400030000).GetMainWorldMapInstance().ForEachDoor(door => door.SetOpen(true));
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_08());
                    break;
                case 30:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_09());
                    foreach (PanesterraFaction faction in Enum.GetValues<PanesterraFaction>())
                        outer.SpawnStage(3, faction);
                    break;
                case 40:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_10());
                    break;
                case 50:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_11());
                    break;
                case 60:
                    outer.ForEachTeam(team =>
                    {
                        if (!team.IsEliminated())
                            outer.SpawnStage(4, team.GetFaction());
                    });
                    break;
                case 130:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_13());
                    break;
                case 138:
                    outer.SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_ALARM_14());
                    break;
                case 150:
                    outer.ForEachTeam(team =>
                    {
                        if (!team.IsEliminated())
                            outer.SendConsolationReward(team);
                    });
                    outer.Stop();
                    break;
            }
        }
    }

    private static class SingletonHolder
    {
        internal static readonly AhserionRaid instance = new AhserionRaid();
    }
}
