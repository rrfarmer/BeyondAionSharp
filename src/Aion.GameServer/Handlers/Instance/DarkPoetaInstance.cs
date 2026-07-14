using System;
using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/DarkPoetaInstance (Hilgert, xTz, Tiger, Ritsu, Estrayl) : GeneralInstanceHandler. @InstanceID(300040000). DarkPoetaScore scoreboard (SM_INSTANCE_SCORE + DarkPoetaScoreWriter); onDie point reward + generator/boss webs; checkRank time-gated rank bosses; calculatePointsReward npc-rating/race/special table + abyss-rank bonus; onOpenDoor/onStart timer; onGather; leaveInstance; onEndCastSkill kobold bomb wall. 1:1.</summary>
[InstanceID(300040000)]
public class DarkPoetaInstance : GeneralInstanceHandler
{
    private readonly List<int> excludedNpcs = new();
    private int killedGenerators;
    private DarkPoetaScore instanceReward;
    private ScheduledTask instanceTimer;
    private long startTime;

    public DarkPoetaInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnDie(Npc npc)
    {
        Creature master = npc.GetMaster();
        if (master is Player)
            return;

        int npcId = npc.GetNpcId();
        int points = CalculatePointsReward(npc);
        if (instanceReward.GetInstanceProgressionType().IsStartProgress() && !excludedNpcs.Contains(npcId))
        {
            instanceReward.AddNpcKill();
            instanceReward.AddPoints(points);
            SendPacket(npc, points);
        }
        switch (npcId)
        {
            case 214895: // Main Power Generator
            case 214896: // Auxiliary Power Generator
            case 214897: // Emergency Generator
                if (Interlocked.Increment(ref killedGenerators) == 3)
                    Spawn(214904, 275.34537f, 323.02072f, 130.9302f, (byte)52);
                break;
            case 214904: // Brigade General Anuhart
                instanceReward.SetInstanceProgressionType(InstanceProgressionType.END_PROGRESS);
                instanceReward.SetRank(CheckRank(instanceReward.GetPoints()));
                SendPacket(null, 0);
                break;
            case 215280: // Tahabata Pyrelord
            case 215281: // Calindi Flamelord
            case 215282: // Vanuka Infernus
            case 215283: // Asaratu Bloodshade
            case 215284: // Chramati Firetail
                Spawn(730211, 1171.9467f, 1223.2805f, 145.43983f, (byte)16); // Exit
                break;
        }
    }

    private int GetTime()
    {
        int current = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime);
        return instanceReward.GetInstanceProgressionType() switch
        {
            InstanceProgressionType.PREPARING => 120000 - current,
            InstanceProgressionType.START_PROGRESS or InstanceProgressionType.END_PROGRESS => 14400000 - current,
            _ => 0,
        };
    }

    private void SendPacket(Npc npc, int points)
    {
        if (npc != null)
            PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(npc.GetObjectTemplate().GetL10n(), points));
        PacketSendUtility.BroadcastToMap(instance, new SM_INSTANCE_SCORE(instance.GetMapId(), new DarkPoetaScoreWriter(instanceReward), GetTime()));
    }

    private int CheckRank(int totalPoints)
    {
        int timeRemain = GetTime();
        int rank = 8;
        if (timeRemain > 7200000 && totalPoints >= 17817)
        {
            Spawn(215280, 1189f, 1244f, 141f, (byte)76);
            rank = 1;
        }
        else if (timeRemain > 5400000 && totalPoints >= 15219)
        {
            Spawn(215281, 1189f, 1244f, 141f, (byte)76);
            rank = 2;
        }
        else if (timeRemain > 3600000 && totalPoints > 10913)
        {
            Spawn(215282, 1189f, 1244f, 141f, (byte)76);
            rank = 3;
        }
        else if (timeRemain > 1800000 && totalPoints > 6656)
        {
            Spawn(215283, 1189f, 1244f, 141f, (byte)76);
            rank = 4;
        }
        else if (timeRemain > 1)
        {
            Spawn(215284, 1189f, 1244f, 141f, (byte)76);
            rank = 5;
        }
        SchedulePortalDespawn((Npc)Spawn(700478, 298.24423f, 316.21954f, 133.29759f, (byte)56));
        return rank;
    }

    private void SchedulePortalDespawn(Npc portal)
    {
        ThreadPoolManager.GetInstance().Schedule(() => portal.GetController().Delete(), 180000L);
    }

    private int CalculatePointsReward(Npc npc)
    {
        int pointsReward = 0;

        // Usually calculated by npcRank
        switch (npc.GetObjectTemplate().GetRating())
        {
            case NpcRating.HERO:
                switch (npc.GetObjectTemplate().GetHpGauge())
                {
                    case 21:
                        pointsReward = 786;
                        break;
                    default:
                        pointsReward = 300;
                        break;
                }
                break;
            default:
                // NpcTemplate initializes race to Race.NONE in Java and C# mirrors that default.
                switch (npc.GetObjectTemplate().GetRace().GetRaceId())
                {
                    case 22: // UNDEAD
                        pointsReward = 12;
                        break;
                    case 9: // BROWNIE
                        pointsReward = 18;
                        break;
                    case 6: // LIZARDMAN
                        pointsReward = 24;
                        break;
                    case 8: // NAGA
                    case 18: // DRAGON
                    case 24: // MAGICALnpc
                        pointsReward = 30;
                        break;
                    default:
                        if (npc.GetNpcId() != 281178)
                            pointsReward = 11;
                        break;
                }
                break;
        }

        // Special npcs
        switch (npc.GetNpcId())
        {
            // Drana
            case 700520:
                pointsReward = 52;
                break;
            // Walls
            case 700517:
            case 700518:
            case 700556:
            case 700558:
                pointsReward = 156;
                break;
            // Mutated Fungie
            case 214885:
                pointsReward = 21;
                break;
            // Named1
            case 214841:
                pointsReward = -209;
                break;
            case 281116:
                pointsReward = 1241;
                break;
            case 215431:
                pointsReward = 208;
                break;
            // Named2
            case 215429:
            case 215430:
                pointsReward = 190;
                break;
            case 214842:
            case 215432:
                pointsReward = 357;
                break;
            // Named3
            case 214871:
            case 215386:
            case 215428:
                pointsReward = 204;
                break;
            // Marabata
            case 214849:
            case 214850:
            case 214851:
                pointsReward = 319;
                break;
            // Generators
            case 214895:
            case 214896:
                pointsReward = 377;
                break;
            case 214897:
                pointsReward = 330;
                break;
            // Atmach
            case 214843:
                pointsReward = 456;
                break;
            // Boss
            case 214864:
            case 214880:
            case 214894:
            case 215387:
            case 215388:
            case 215389:
                pointsReward = 789;
                break;
            case 214904:
                pointsReward = 954;
                break;
        }
        TemporaryPlayerTeam team = (TemporaryPlayerTeam)instance.GetRegisteredTeam();
        if (team != null)
        {
            if (team.GetLeaderObject().GetAbyssRank().GetRank().GetId() >= AbyssRankEnum.STAR1_OFFICER.GetId())
                pointsReward = JavaMath.Round(pointsReward * 1.1f);
        }
        return pointsReward;
    }

    private void OnStart(bool manually)
    {
        instanceReward.SetInstanceProgressionType(InstanceProgressionType.START_PROGRESS);
        startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SendPacket(null, 0);
        if (!manually)
            instance.ForEachDoor(d => d.SetOpen(true));
    }

    public override void OnEnterInstance(Player player)
    {
        SendPacket(null, 0);
    }

    public override void OnOpenDoor(int doorId)
    {
        if (doorId == 33)
        {
            if (instanceTimer != null && !instanceTimer.IsCancelled)
                instanceTimer.Cancel(true);
            OnStart(true);
        }
    }

    public override void OnInstanceDestroy()
    {
        if (instanceTimer != null)
            instanceTimer.Cancel(false);
    }

    public override void OnInstanceCreate()
    {
        excludedNpcs.AddRange(new[] { 700439, 700440, 700441, 700442, 700443, 700444, 700445, 700446, 700447, 281178 });
        instanceReward = new DarkPoetaScore();
        instanceReward.SetInstanceProgressionType(InstanceProgressionType.PREPARING);
        startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        instanceTimer = ThreadPoolManager.GetInstance().Schedule(() => OnStart(false), 121000L);
    }

    public override void OnGather(Player player, Gatherable gatherable)
    {
        instanceReward.AddGather();
        SendPacket(null, 1);
    }

    public override void LeaveInstance(Player player)
    {
        if (instanceReward.GetInstanceProgressionType().IsEndProgress())
            TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    public override void OnEndCastSkill(Skill skill)
    {
        if (skill.GetSkillId() != 18130) // Kobold Bomb Effect
            return;
        Npc camouflageStoneWall = GetNpc(700516);
        if (camouflageStoneWall != null && PositionUtil.IsInRange(skill.GetEffector(), camouflageStoneWall, 10))
            camouflageStoneWall.GetController().Die();
    }
}
