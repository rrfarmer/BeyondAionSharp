using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Commons.Nio;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;
using ItemService = Aion.GameServer.Services.Items.ItemService;
using TeleportService = Aion.GameServer.Services.Teleport.TeleportService;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/crucible/EmpyreanCrucibleInstance (@author xTz, Luzien, w4terbomb) : CrucibleInstance.
/// Upstream 1af948baf matched the stage flow, the bosses and the bonus stages to the retail AI patterns and a live run.
/// </summary>
[InstanceID(300300000)]
public class EmpyreanCrucibleInstance : CrucibleInstance
{
    private const string FIGHTER_AI = "empyrean_fighter";
    private const string SUPPLY_AI = "empyrean_manduri_supply_device";
    private readonly List<Npc> npcs = new List<Npc>();
    private int stageKills;
    // Java ConcurrentHashMap.newKeySet() -> the keys of a concurrent dictionary; TryAdd is its add-if-absent.
    private readonly ConcurrentDictionary<int, byte> stage1Advanced = new();
    private readonly ConcurrentDictionary<int, ScheduledTask> stage1Timers = new();
    private readonly ConcurrentDictionary<int, byte> bossGreeted = new();
    private readonly ConcurrentDictionary<int, byte> inWorld = new(); // entered and not left, logged out ones included
    private byte stage;
    private bool isDoneStage6Round2, isDoneStage6Round1, isKrallSet, isBonusStage2Over, gameOver;
    private long lastSerfShout;
    private ScheduledTask? dragonTimeoutTask, bonusStage4Task, bonusStage6Task, bonusStage9Task, golemTask;

    public EmpyreanCrucibleInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        base.OnInstanceCreate();
        stage = 0;
        Sp(799567, 345.25107f, 349.40176f, 96.09097f, (byte)0);
        Sp(799573, 384.51f, 352.61078f, 96.747635f, (byte)83);
    }

    public override void OnEnterInstance(Player player)
    {
        bool isNew = !instanceScore.ContainsPlayer(player.GetObjectId());
        base.OnEnterInstance(player);
        inWorld[player.GetObjectId()] = 0;
        CruciblePlayerReward playerReward = GetPlayerReward(player.GetObjectId());
        if (gameOver)
        {
            DoReward(player);
        }
        else if (stage > 0)
        {
            if (isNew) // a late joiner waits until the arbiter lets them in
            {
                playerReward.SetPlayerDefeated(true);
                MoveToReadyRoom(player);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ENTERED_BIRTHAREA_IDARENA());
            }
            else
            {
                PortToStartPosition(player);
            }
        }
        BroadcastScore();
        PacketSendUtility.SendPacket(player, new SM_INSTANCE_STAGE_INFO(2, stageType.GetId(), stageType.GetType_()));
    }

    public override bool CanEnter(Player player)
    {
        CruciblePlayerReward reward = GetPlayerReward(player.GetObjectId());
        if (reward != null && reward.IsRewarded())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_REENTER_INSTANCE_IDARENA());
            return false;
        }
        return true;
    }

    private void AddPointsAndSendPacket(int points, Npc npc)
    {
        instance.ForEachPlayer(player =>
        {
            if (player.IsOnline())
            {
                CruciblePlayerReward playerReward = GetPlayerReward(player.GetObjectId());
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(npc.GetObjectTemplate().GetL10n()!, points));
                if (!playerReward.IsRewarded())
                    playerReward.AddPoints(points);
            }
        });
        BroadcastScore();
    }

    private void BroadcastScore()
    {
        if (isInstanceDestroyed)
            return;
        HashSet<int> present = instance.GetPlayersInside().Select(p => p.GetObjectId()).ToHashSet();
        instance.ForEachPlayer(p => PacketSendUtility.SendPacket(p, new SM_INSTANCE_SCORE(instance.GetMapId(), new ScoreWriter(instanceScore, present))));
    }

    private void SetStage(StageType type, int time)
    {
        this.stageType = type;
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            instance.ForEachPlayer(player => PacketSendUtility.SendPacket(player, new SM_INSTANCE_STAGE_INFO(2, type.GetId(), type.GetType_())));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(time));
        BroadcastScore();
    }

    public override void OnDie(Npc npc)
    {
        lock (npcs)
        {
            npcs.Remove(npc);
        }
        int points = npc.GetNpcId() switch
        {
            217511 or 217512 or 217513 or 217514 => 55,
            217518 => 110,
            217515 or 217516 or 217517 => 120,
            217522 => 250,
            217560 or 217561 => 260,
            217557 or 217558 or 217559 => 280,
            217494 or 217497 or 217499 or 217504 or 217507 or 217508 or 217519 or 217520 or 217521 or 217529 or 217531 or 217545 or 217547 or 217548 or 217549 or 217550 or 217569 => 300,
            217562 => 340,
            217530 or 217564 or 217565 => 350,
            217495 or 217498 or 217502 or 217505 or 217506 or 217536 or 217563 or 217566 => 400,
            217654 => 455,
            217523 or 217524 or 217525 or 217526 => 475,
            217568 => 480,
            217480 or 217489 or 217496 or 217503 or 217532 or 217533 or 217534 or 217535 or 217653 => 500,
            217652 => 590,
            217477 or 217486 => 600,
            217651 => 695,
            217570 => 700,
            217600 or 217601 or 217602 => 1090,
            217483 or 217492 => 1100,
            217478 or 217487 => 1200,
            217482 or 217491 => 1300,
            217603 or 217604 or 217605 or 217606 => 1390,
            217500 or 217509 => 1500,
            217552 => 1900,
            217543 or 217553 => 2000,
            217567 => 2100,
            217554 => 2400,
            217578 or 217582 => 2900,
            217579 or 217583 => 3100,
            217527 or 217528 => 3250,
            217572 => 3370,
            217581 or 217585 => 3400,
            217580 or 217584 => 3800,
            217588 or 217589 => 4000,
            217590 => 4200,
            217596 or 217597 => 4250,
            217484 or 217493 or 217591 => 5000,
            217501 or 217510 => 5500,
            217592 => 6000,
            217594 => 6200,
            217595 => 7300,
            217544 or 217556 => 7800,
            217573 => 8900,
            217598 => 9400,
            217586 or 217587 => 11500,
            217593 => 17000,
            217607 => 17800,
            217599 => 25900,
            217608 => 192500,
            217609 => 204400,
            _ => 0,
        };
        if (points != 0)
        {
            AddPointsAndSendPacket(points, npc);
        }
        switch (npc.GetNpcId())
        {
            case 217477:
            case 217478:
            case 217480:
            case 217482:
            case 217483:
            case 217486:
            case 217487:
            case 217489:
            case 217491:
            case 217492:
                CancelStage1Timer(npc);
                AdvanceStage1(npc);
                break;
            case 217484:
            case 217493:
            {
                PacketSendUtility.BroadcastMessage(npc, npc.GetNpcId() == 217484 ? 1500206 : 1500246);
                SetStage(StageType.PASS_GROUP_STAGE_1, 0);
                int boxId = GetRegisteredTeamRace() == Race.ASMODIANS ? 217756 : 217734; // S1_ResurBox1Dark_55_n / S1_ResurBox1_55_n
                Sp(boxId, 342.65106f, 357.4013f, 96.09094f, (byte)0, 1000);
                Sp(boxId, 349.07376f, 357.5898f, 96.090965f, (byte)0, 1000);
                Sp(boxId, 345.69272f, 359.40958f, 96.09094f, (byte)0, 1000);
                Sp(boxId, 342.59192f, 353.73386f, 96.090965f, (byte)0, 1000);
                Sp(boxId, 342.6043f, 360.8932f, 96.09093f, (byte)0, 1000);
                Sp(boxId, 345.69318f, 355.56677f, 96.09094f, (byte)0, 1000);
                SpawnArenaRecordKeeper(799568); // IDArena_2nd_MC_Start
                break;
            }
            case 217494:
            case 217495:
            case 217496:
            case 217497:
            case 217498:
            case 217499:
            case 217500:
            case 217502:
            case 217503:
            case 217504:
            case 217505:
            case 217506:
            case 217507:
            case 217508:
            case 217509:
                switch (Interlocked.Increment(ref stageKills))
                {
                    case 3: StartStage2Round(2); break;
                    case 7: StartStage2Round(3); break;
                    case 8: StartStage2Round(4); break;
                    case 13: StartStage2Round(5); break;
                }
                break;
            case 217510:
            case 217501:
                SetStage(StageType.PASS_GROUP_STAGE_2, 0);
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    StartBonusStage2();
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(8000));
                break;
            case 217737:
                PacketSendUtility.BroadcastMessage(npc, 342353);
                EndBonusStage2();
                break;
            case 217511:
            case 217512:
            case 217513:
            case 217514:
                if (AllDead(217511, 217512, 217513, 217514))
                {
                    SetStage(StageType.START_STAGE_3_ROUND_2, 3000);
                    Sp(217515, 336.32092f, 345.0251f, 96.090935f, (byte)0, 8000); // S3_ElementalEarth_2nd_55_Ae
                    Sp(217516, 347.16144f, 361.89084f, 96.09093f, (byte)0, 8000); // S3_ElementalFire_2nd_55_Ae
                    Sp(217517, 354.132f, 337.14255f, 96.09089f, (byte)0, 8000); // S3_ElementalWater_2nd_55_Ae
                    Sp(217518, 352.77557f, 360.97845f, 96.09091f, (byte)0, 8000); // S3_ElementalAir_2nd_55_Ae
                    Sp(217515, 349.16327f, 335.63864f, 96.09095f, (byte)0, 8000); // S3_ElementalEarth_2nd_55_Ae
                    Sp(217516, 350.0108f, 342.09482f, 96.090935f, (byte)0, 8000); // S3_ElementalFire_2nd_55_Ae
                    Sp(217517, 353.7888f, 354.4324f, 96.091064f, (byte)0, 8000); // S3_ElementalWater_2nd_55_Ae
                    Sp(217518, 340.2231f, 351.10208f, 96.09098f, (byte)0, 8000); // S3_ElementalAir_2nd_55_Ae
                    Sp(217515, 348.87338f, 354.90146f, 96.09096f, (byte)0, 8000); // S3_ElementalEarth_2nd_55_Ae
                    Sp(217516, 334.60898f, 352.01447f, 96.09095f, (byte)0, 8000); // S3_ElementalFire_2nd_55_Ae
                    Sp(217517, 341.23633f, 344.55603f, 96.09096f, (byte)0, 8000); // S3_ElementalWater_2nd_55_Ae
                    Sp(217518, 354.66513f, 343.31537f, 96.091095f, (byte)0, 8000); // S3_ElementalAir_2nd_55_Ae
                }
                break;
            case 217515:
            case 217516:
            case 217517:
            case 217518:
                if (AllDead(217515, 217516, 217517, 217518))
                {
                    SetStage(StageType.START_STAGE_3_ROUND_3, 3000);
                    Sp(217519, 351.08026f, 341.61298f, 96.090935f, (byte)0, 8000); // S3_ElementalEarth_3rd_55_Ae
                    Sp(217521, 333.4532f, 354.7357f, 96.09094f, (byte)0, 8000); // S3_ElementalWater_3rd_55_Ae
                    Sp(217522, 342.1805f, 360.534f, 96.09092f, (byte)0, 8000); // S3_ElementalAir_3rd_55_Ae
                    Sp(217520, 334.2686f, 342.60797f, 96.09091f, (byte)0, 8000); // S3_ElementalFire_3rd_55_Ae
                    Sp(217522, 350.34537f, 356.18558f, 96.09094f, (byte)0, 8000); // S3_ElementalAir_3rd_55_Ae
                    Sp(217520, 343.7485f, 336.2869f, 96.09092f, (byte)0, 8000); // S3_ElementalFire_3rd_55_Ae
                }
                break;
            case 217519:
            case 217520:
            case 217521:
            case 217522:
                if (AllDead(217519, 217520, 217521, 217522))
                {
                    SetStage(StageType.START_STAGE_3_ROUND_4, 3000);
                    Sp(217524, 349.66446f, 341.4752f, 96.090965f, (byte)0, 8000); // S3_ElementalFire_4th_55_Ae
                    Sp(217525, 338.32742f, 356.29636f, 96.090935f, (byte)0, 8000); // S3_ElementalWater_4th_55_Ae
                    Sp(217526, 349.31473f, 358.43762f, 96.09096f, (byte)0, 8000); // S3_ElementalAir_4th_55_Ae
                    Sp(217523, 338.73138f, 342.35876f, 96.09094f, (byte)0, 8000); // S3_ElementalEarth_4th_55_Ae
                }
                break;
            case 217523:
            case 217524:
            case 217525:
            case 217526:
                if (AllDead(217523, 217524, 217525, 217526))
                {
                    SetStage(StageType.START_STAGE_3_ROUND_5, 3000);
                    Sp(217527, 335.37524f, 346.34567f, 96.09094f, (byte)0, 8000); // S3_MagmaElemental_Boss_55_Ah
                    Sp(217528, 335.36105f, 353.16922f, 96.09094f, (byte)0, 8000); // S3_TempestElemental_Boss_55_Ah
                }
                break;
            case 217527:
            case 217528:
                if (AllDead(217527, 217528))
                {
                    SetStage(StageType.START_BONUS_STAGE_3, 8000);
                    Sp(217744, 342.45215f, 349.339f, 96.09096f, (byte)0, 8000); // S3_Elemeltal_CTRL
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S3_Elemeltal_CTRL_01(), 8000);
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        StartBonusStage3();
                        return ValueTask.CompletedTask;
                    }, TimeSpan.FromMilliseconds(40000));
                }
                break;
            case 217557:
            case 217558:
            case 217559:
            case 217560:
            case 217561:
            case 217562:
            case 217563:
            case 217564:
            case 217565:
            case 217566:
                switch (Interlocked.Increment(ref stageKills))
                {
                    case 5: StartStage4Round(2); break;
                    case 11: StartStage4Round(3); break;
                    case 16: StartStage4Round(4); break;
                }
                break;
            case 217571:
            case 217745:
            case 217746:
            case 217747:
            case 217748:
            case 205413:
            case 205414:
            case 217576:
            case 217577:
                npc.GetController().Delete();
                break;
            case 217567:
                Interlocked.Increment(ref stageKills);
                StartStage4Round(5);
                break;
            case 217651:
            case 217652:
            case 217653:
            case 217654:
                if (Interlocked.Increment(ref stageKills) == 29)
                {
                    SetStage(StageType.PASS_GROUP_STAGE_4, 1000);
                    Sp(217749, 340.59f, 349.32166f, 96.09096f, (byte)0, 8000); // S4_Draky_CTRL
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S4_Draky_CTRL_00(), 8000);
                    SetStage(StageType.START_BONUS_STAGE_4, 8000);
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        StartBonusStage4();
                        return ValueTask.CompletedTask;
                    }, TimeSpan.FromMilliseconds(40000));
                }
                break;
            case 217545:
            case 217547:
            case 217548:
            case 217549:
            case 217550:
                if (Interlocked.Increment(ref stageKills) == 5)
                {
                    SetStage(StageType.START_STAGE_5_ROUND_2, 3000);
                    Sp(217552, 1246.0197f, 788.8341f, 358.60556f, (byte)11, 8000); // S4_ShulackBartenderNmd_55_Ah
                }
                break;
            case 217529:
            case 217530:
            case 217531:
            case 217532:
            case 217533:
            case 217534:
            case 217535:
            case 217536:
            case 217543:
                switch (Interlocked.Increment(ref stageKills))
                {
                    case 5: StartLehparRound(2); break;
                    case 10: StartLehparRound(3); break;
                    case 11: StartLehparRound(4); break;
                    case 16: StartLehparRound(5); break;
                }
                break;
            case 217552:
                SetStage(StageType.START_STAGE_5_ROUND_3, 3000);
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    SpawnLever(700527);
                    SpawnLever(700528);
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(1000));
                Sp(217553, "empyrean_manduri_giant", 1259.4706f, 812.30505f, 358.6056f, (byte)30, 8000); // S4_ManduriGiantNmd_55_Ah
                break;
            case 217553:
                DeleteAliveNpcs(700527, 700528, 281128, 281129);
                Sp(281111, 1246.4855f, 796.90735f, 358.6056f, (byte)0, 1000); // BIDShulack_EngineerNozzleD_45_An
                Sp(281110, 1259.5508f, 784.5548f, 358.60562f, (byte)0, 1000); // BIDShulack_EngineerNozzleC_45_An
                Sp(281112, 1276.6561f, 812.5499f, 358.60565f, (byte)0, 1000); // BIDShulack_EngineerNozzleE_45_An
                Sp(281322, 1243.2113f, 813.0927f, 358.60565f, (byte)0, 1000); // BIDShulack_EngineerNozzleH_45_An
                Sp(281109, 1272.9266f, 797.1055f, 358.60562f, (byte)0, 1000); // BIDShulack_EngineerNozzleB_45_An
                Sp(281113, 1275.894f, 780.51544f, 358.60565f, (byte)0, 1000); // BIDShulack_EngineerNozzleF_45_An
                Sp(281108, 1260.003f, 810.555f, 358.6056f, (byte)0, 1000); // BIDShulack_EngineerNozzleA_45_An
                Sp(281114, 1244.3293f, 780.4284f, 358.60562f, (byte)0, 1000); // BIDShulack_EngineerNozzleG_45_An
                SetStage(StageType.START_STAGE_5_ROUND_4, 3000);
                Sp(217554, 1243.1877f, 796.79553f, 358.6056f, (byte)0, 8000); // S4_ShulackEngineerNmd_55_Ah
                break;
            case 217554:
                DeleteAliveNpcs(281108, 281109, 281110, 281111, 281112, 281113, 281114, 281322);
                SetStage(StageType.START_STAGE_5_ROUND_5, 3000);
                Sp(217556, "empyrean_shulack_captain", 1259.8387f, 785.6266f, 358.60562f, (byte)30, 8000); // S4_ShulackCaptainNmd_55_Ah
                Sp(281191, 1261.8f, 804.5f, 358.7f, (byte)0, 8000); // BIDShulack_CaptainStigmaStoneA_45_n
                Sp(281192, 1267.6f, 793.9f, 358.7f, (byte)0, 8000); // BIDShulack_CaptainStigmaStoneB_45_n
                Sp(281193, 1257.4f, 787.9f, 358.7f, (byte)0, 8000); // BIDShulack_CaptainStigmaStoneC_45_n
                Sp(281194, 1251.3f, 798.6f, 358.7f, (byte)0, 8000); // BIDShulack_CaptainStigmaStoneD_45_n
                Sp(281180, "no_dmg_no_action", 1259.5123f, 796.5463f, 360.0908f, (byte)0, 1000); // BIDShulack_CaptainStigma_45_n
                Sp(205499, 1259.5135f, 796.5465f, 361.4129f, (byte)0, 1000); // IDArena_5S_Stigma
                break;
            case 217544:
            case 217556:
                DeleteAliveNpcs(281191, 281192, 281193, 281194, 281180, 205499);
                SetStage(StageType.PASS_GROUP_STAGE_5, 0);
                Sp(205339, 1260.1465f, 795.07495f, 358.60562f, (byte)30, 1000); // IDArena_5th_MC_End
                break;
            case 217568:
                Sp(205413, npc.GetX(), npc.GetY(), npc.GetZ(), (byte)npc.GetHeading()); // S6_NPC_Graveknight_55_Ae
                npc.GetController().Delete();
                if (stageType == StageType.START_STAGE_6_ROUND_1 && isDoneStage6Round1 && AllDead(217568))
                {
                    SetStage(StageType.START_STAGE_6_ROUND_2, 3000);
                    Sp(217570, 1629.4642f, 154.8044f, 126f, (byte)30, 8000); // S6_Zombie_55_Ae
                    Sp(217569, 1643.7776f, 161.63562f, 126f, (byte)46, 8000); // S6_LichEquip_55_Ae
                    Sp(217569, 1639.7843f, 142.09268f, 126f, (byte)40, 8000); // S6_LichEquip_55_Ae
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        Sp(217569, 1614.6377f, 164.04999f, 126.00113f, (byte)3); // S6_LichEquip_55_Ae
                        Sp(217569, 1625.8965f, 135.62509f, 126f, (byte)30); // S6_LichEquip_55_Ae
                        isDoneStage6Round2 = true;
                        return ValueTask.CompletedTask;
                    }, TimeSpan.FromMilliseconds(18000));
                }
                break;
            case 217570:
                for (int i = 0; i < 5; i++)
                {
                    Npc? worm = Sp(217571, npc.GetX() + Rnd.Get(-2, 2), npc.GetY() + Rnd.Get(-2, 2), npc.GetZ(), (byte)npc.GetHeading()); // S6_Clodworm_55_Ae
                    if (worm != null)
                        ThreadPoolManager.GetInstance().Schedule(_ =>
                        {
                            worm.GetController().Delete();
                            return ValueTask.CompletedTask;
                        }, TimeSpan.FromMilliseconds(30000));
                }
                break;
            case 217569:
                Sp(205414, npc.GetX(), npc.GetY(), npc.GetZ(), (byte)npc.GetHeading());
                npc.GetController().Delete();
                if (stageType == StageType.START_STAGE_6_ROUND_2 && isDoneStage6Round2 && AllDead(217569, 217570))
                {
                    SetStage(StageType.START_STAGE_6_ROUND_3, 3000);
                    Sp(217572, 1629.5837f, 138.38435f, 126f, (byte)30, 8000); // S6_DeathNight_Live_55_Ah
                    Sp(217569, 1635.01535f, 150.01535f, 126f, (byte)45, 8000); // S6_LichEquip_55_Ae
                    Sp(217569, 1638.3817f, 152.84074f, 126f, (byte)45, 8000); // S6_LichEquip_55_Ae
                    Sp(217568, 1622.8409f, 173.1985f, 126f, (byte)90, 20000); // S5_Graveknight_55_Ae
                }
                break;
            case 217572:
                SetStage(StageType.START_STAGE_6_ROUND_4, 3000);
                Sp(217573, npc.GetX(), npc.GetY(), npc.GetZ(), (byte)npc.GetHeading()); // S6_Deathknight_Ghost_55_Ah_01
                break;
            case 217573:
                SetStage(StageType.PASS_GROUP_STAGE_6, 0);
                Sp(217736, 1589.2f, 149.5f, 128.8f, (byte)0, 1000); // S6_ResurBox1_55_n
                Sp(217736, 1590.1f, 153.6f, 128.8f, (byte)0, 1000); // S6_ResurBox1_55_n
                Sp(217750, "empyrean_ghost_arminos", 1624.1908f, 155.16148f, 126f, (byte)0, 8000); // S6_Ghost_55_Ah
                SetStage(StageType.START_BONUS_STAGE_6, 8000);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S6_Ghost_55_Ah_01(), 8000);
                break;
            case 217750:
                PacketSendUtility.BroadcastMessage(npc, 1500259);
                if (bonusStage6Task != null)
                    bonusStage6Task.Cancel(false);
                DeleteAliveNpcs(217751, 217752);
                Sp(205340, 1625.08f, 159.15f, 126f, (byte)70, 1000); // IDArena_6th_MC_End
                break;
            case 217582:
            case 217578:
            {
                SetStage(StageType.START_STAGE_7_ROUND_2, 3000);
                int npcId = GetRegisteredTeamRace() == Race.ASMODIANS ? 217583 : 217579; // S7_MasterD_Sc_55_Ah / S7_MasterL_Sc_55_Ah
                Sp(npcId, "ranger_preceptor", 1794.81f, 779.53925f, 469.35016f, (byte)40, 8000);
                break;
            }
            case 217579:
            case 217583:
            {
                SetStage(StageType.START_STAGE_7_ROUND_3, 3000);
                int npcId = GetRegisteredTeamRace() == Race.ASMODIANS ? 217584 : 217580; // S7_MasterD_Ma_55_Ah / S7_MasterL_Ma_55_Ah
                Sp(npcId, "mage_preceptor", 1775.6254f, 811.43225f, 469.35022f, (byte)100, 8000);
                break;
            }
            case 217580:
            case 217584:
            {
                SetStage(StageType.START_STAGE_7_ROUND_4, 3000);
                int npcId = GetRegisteredTeamRace() == Race.ASMODIANS ? 217585 : 217581; // S7_MasterD_Pr_55_Ah / S7_MasterL_Pr_55_Ah
                Sp(npcId, "priest_preceptor", 1775.716f, 779.630f, 469.564f, (byte)20, 8000);
                break;
            }
            case 217581:
            case 217585:
            {
                SetStage(StageType.START_STAGE_7_ROUND_5, 3000);
                int npcId = GetRegisteredTeamRace() == Race.ASMODIANS ? 217587 : 217586; // S7_Stigma_MasterD_Boss_55_Ah / S7_Stigma_MasterL_Boss_55_Ah
                Sp(npcId, "stigma_boss_preceptor", 1773.194f, 796.537f, 469.350f, (byte)0, 8000);
                break;
            }
            case 217586:
            case 217587:
            {
                SetStage(StageType.PASS_GROUP_STAGE_7, 0);
                int bookBoxId = GetRegisteredTeamRace() == Race.ASMODIANS ? 217759 : 217753; // S7_BookBoxDark_55_n / S7_BookBox_55_n
                Sp(bookBoxId, "noaction", 1782.881f, 800.114f, 469.420f, (byte)0, 1000);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S7_BookBox_01(), 1000);
                Sp(205341, 1781.610f, 796.920f, 469.350f, (byte)0, 1000); // IDArena_7th_MC_End
                break;
            }
            case 217588:
            case 217589:
                SetStage(StageType.START_STAGE_8_ROUND_2, 3000);
                Sp(217590, 1764.377f, 1761.510f, 303.695f, (byte)0, 8000); // S8_Seiren_Queen_55_Ah
                break;
            case 217590:
                SetStage(StageType.START_STAGE_8_ROUND_3, 3000);
                Sp(217591, 1776.946f, 1749.255f, 303.696f, (byte)30, 8000); // S8_AkanNamed_50_Ah
                break;
            case 217591:
                SetStage(StageType.START_STAGE_8_ROUND_4, 3000);
                Sp(217592, 1790.693f, 1761.911f, 303.877f, (byte)60, 8000); // S8_TestResult_55_Ah
                break;
            case 217592:
            {
                SetStage(StageType.START_STAGE_8_ROUND_5, 3000);
                Npc? perfectMan = Sp(280790, npc.GetX(), npc.GetY(), npc.GetZ(), (byte)npc.GetHeading()); // BLF3_NM_PerfectManSum_45_Ae
                if (perfectMan != null)
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        perfectMan.GetController().Delete();
                        return ValueTask.CompletedTask;
                    }, TimeSpan.FromMilliseconds(120000));
                Sp(217593, 1777.065f, 1763.706f, 303.695f, (byte)90, 8000); // S8_TestResult_Boss_55_Ah
                break;
            }
            case 217593:
                SetStage(StageType.PASS_GROUP_STAGE_8, 0);
                Sp(205342, 1776.757f, 1764.624f, 303.695f, (byte)90, 1000); // IDArena_8th_MC_End
                break;
            case 217594:
                SetStage(StageType.START_STAGE_9_ROUND_2, 3000);
                Sp(217595, 1322.311f, 1741.508f, 316.349f, (byte)65, 8000); // S9_Condor_Gi_55_Ah
                break;
            case 217595:
                SetStage(StageType.START_STAGE_9_ROUND_3, 3000);
                Sp(217596, 1308.038f, 1729.718f, 315.996f, (byte)36, 8000); // S9_Manduri_Gi_55_Ah
                Sp(217597, 1302.290f, 1745.471f, 316.092f, (byte)96, 8000); // S9_Frillfaimam_Gi_55_Ah
                break;
            case 217596:
            case 217597:
            {
                Npc counterpart = GetNpc(npc.GetNpcId() == 217596 ? 217597 : 217596);
                if (counterpart != null && !counterpart.IsDead())
                    counterpart.QueueSkill(19624, 10, -1, NpcSkillTargetAttribute.ME);
                if (AllDead(217596, 217597))
                {
                    SetStage(StageType.START_STAGE_9_ROUND_4, 3000);
                    Sp(217598, 1323.1051f, 1717.7927f, 317.1f, (byte)45, 8000); // S9_Shellizard_Gi_55_Ah
                }
                break;
            }
            case 217598:
                SetStage(StageType.START_STAGE_9_ROUND_5, 3000);
                Sp(217599, 1304.6428f, 1733.9614f, 316.5f, (byte)3, 8000); // S9_Chimera_Boss_55_Ah
                break;
            case 217599:
                SetStage(StageType.PASS_GROUP_STAGE_9, 0);
                StartBonusStage9();
                break;
            case 217754:
                EndBonusStage9();
                break;
            case 217600:
            case 217601:
            case 217602:
                if (Interlocked.Increment(ref stageKills) == 10)
                {
                    SetStage(StageType.START_STAGE_10_ROUND_2, 3000);
                    Sp(217604, 1769.898f, 1303.915f, 394.3f, (byte)77, 8000); // S10_Drakan_Sc_High_55_Ae
                    Sp(217605, 1773.268f, 1302.647f, 394.3f, (byte)77, 8000); // S10_Drakan_Ma_High_55_Ae
                    Sp(217606, 1776.197f, 1299.988f, 394.3f, (byte)77, 8000); // S10_Drakan_Cl_High_55_Ae
                    Sp(217603, 1778.467f, 1298.172f, 394.3f, (byte)77, 24000); // S10_Drakan_Fi_High_55_Ae
                    Sp(217603, 1765.897f, 1305.908f, 394.3f, (byte)77, 24000); // S10_Drakan_Fi_High_55_Ae
                    Sp(217604, 1771.398f, 1306.403f, 394.3f, (byte)77, 70000); // S10_Drakan_Sc_High_55_Ae
                    Sp(217605, 1774.632f, 1305.220f, 394.3f, (byte)77, 70000); // S10_Drakan_Ma_High_55_Ae
                    Sp(217606, 1777.682f, 1303.205f, 394.3f, (byte)77, 70000); // S10_Drakan_Cl_High_55_Ae
                    Sp(217603, 1767.865f, 1308.109f, 394.3f, (byte)77, 82000); // S10_Drakan_Fi_High_55_Ae
                    Sp(217603, 1780.228f, 1300.834f, 394.3f, (byte)77, 82000); // S10_Drakan_Fi_High_55_Ae
                }
                break;
            case 217603:
            case 217604:
            case 217605:
            case 217606:
                if (Interlocked.Increment(ref stageKills) == 20)
                {
                    SetStage(StageType.START_STAGE_10_ROUND_3, 3000);
                    Sp(217607, FIGHTER_AI, 1765.4668f, 1287.6345f, 394.237f, (byte)76, 8000); // S10_WaterGolem_55_Ah
                }
                break;
            case 700439:
            {
                Npc golem = GetNpc(217607);
                if (golem != null && !golem.IsDead())
                {
                    golem.GetEffectController().RemoveEffect(18110); // Cancel Torrent of the summoned aquar
                    if (Rnd.Chance() < 30)
                    {
                        if (golem.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED) is Creature second)
                            golem.GetAggroList().AddHate(second, 500);
                    }
                    else
                    {
                        golem.QueueSkill(16684, 41);
                    }
                }
                break;
            }
            case 217607:
                if (golemTask != null)
                    golemTask.Cancel(false);
                DeleteAliveNpcs(700439);
                SetStage(StageType.START_STAGE_10_ROUND_4, 3000);
                Sp(217608, 1771.9846f, 1301.1503f, 394.237f, (byte)76, 8000); // S10_Dragon_55_Ah
                break;
            case 217608:
                if (dragonTimeoutTask != null)
                    dragonTimeoutTask.Cancel(false);
                SetStage(StageType.START_STAGE_10_ROUND_5, 3000);
                Sp(217609, "vanktrist", 1767.3652f, 1294.0907f, 394.3f, (byte)3, 8000); // SFinal_Immotal_Boss_55_Ah
                break;
            case 217609:
                SetStage(StageType.PASS_GROUP_STAGE_10, 0);
                Sp(205344, 1766.2f, 1292.0f, 394.23755f, (byte)75, 1000); // IDArena_10th_MC_End
                break;
        }
    }

    public override void OnAggro(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 217477:
            case 217478:
            case 217480:
            case 217483:
            case 217486:
            case 217487:
            case 217489:
            case 217492: // the priests only advance the round by dying
                stage1Timers.GetOrAdd(npc.GetObjectId(), _ => ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    AdvanceStage1(npc);
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(40000)));
                break;
            case 217484:
                if (bossGreeted.TryAdd(npc.GetObjectId(), 0))
                    PacketSendUtility.BroadcastMessage(npc, 1500205);
                break;
            case 217493:
                if (bossGreeted.TryAdd(npc.GetObjectId(), 0))
                    PacketSendUtility.BroadcastMessage(npc, 1500245);
                break;
            case 217740:
            case 217741:
            case 217742:
            case 217743: // S3_Elemeltal_Earth/Warter/Fire/Wind_55_Ah
                if (bossGreeted.TryAdd(npc.GetObjectId(), 0))
                    PacketSendUtility.BroadcastMessage(npc, 342394 + npc.GetNpcId() - 217740);
                break;
            case 217750:
                StartBonusStage6(npc);
                break;
            case 217751: // a blood ward goes off as soon as someone steps on it
            {
                Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 19643, 46, npc);
                if (skill != null)
                    skill.UseNoAnimationSkill();
                break;
            }
            case 217752:
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    npc.GetController().Delete();
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(15000));
                break;
            case 217607:
                StartGolemSwitches(npc);
                break;
        }
        if (npc.GetNpcId() == 217608 && dragonTimeoutTask == null) // S10_Dragon_55_Ah
            dragonTimeoutTask = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (npc.IsDead() || !npc.IsSpawned())
                    return ValueTask.CompletedTask;
                PacketSendUtility.BroadcastMessage(npc, 341867);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_S_RANK_BATTLE_END());
                npc.QueueSkill(19679, 41);
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    npc.GetController().Delete();
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(3000));
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(600000));
    }

    public override void OnBackHome(Npc npc)
    {
        CancelStage1Timer(npc);
        bossGreeted.TryRemove(npc.GetObjectId(), out _);
    }

    public override void OnSpawn(VisibleObject obj)
    {
        if (!(obj is Npc npc))
            return;
        switch (npc.GetNpcId())
        {
            case 217751:
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    npc.GetController().Delete();
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(20000));
                break;
            case 217588:
            case 217589:
                ThreadPoolManager.GetInstance().Schedule(_ => // Kromede waits under Blessing of Rock
                {
                    Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 17052, 28, npc);
                    if (skill != null && npc.IsSpawned() && !npc.IsDead())
                        skill.UseSkill();
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(1000));
                break;
            case 217752:
            {
                long now = SystemClock.CurrentMillis();
                if (now - lastSerfShout > 2000) // one shout per summoning
                {
                    lastSerfShout = now;
                    Npc arminos = GetNpc(217750);
                    if (arminos != null)
                        PacketSendUtility.BroadcastMessage(arminos, 1500257);
                }
                break;
            }
        }
    }

    public override void OnEndCastSkill(Skill skill)
    {
        if (skill.GetSkillId() == 19643 && skill.GetEffector() is Npc npc && npc.GetNpcId() == 217751)
            npc.GetController().Delete();
    }

    private void CancelStage1Timer(Npc npc)
    {
        if (stage1Timers.TryRemove(npc.GetObjectId(), out ScheduledTask? timer) && timer != null)
            timer.Cancel(false);
    }

    private void StartBonusStage2()
    {
        isBonusStage2Over = false;
        Sp(217737, "empyrean_bonus_saam", 334.49496f, 349.2322f, 96.090935f, (byte)0); // S2_SAAM1_55_n
        SetStage(StageType.START_BONUS_STAGE_2, 0);
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S2_SAAM_CTRL_01());
        ScheduleWhileAlive(217737, () => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S2_Saam1_01()), 40000);
        ScheduleWhileAlive(217737, () => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S2_Saam1_02()), 60000);
        ScheduleWhileAlive(217737, () => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S2_Saam1_03()), 65000);
        ScheduleWhileAlive(217737, () =>
        {
            PacketSendUtility.BroadcastMessage(GetNpc(217737), 342353);
            EndBonusStage2();
        }, 70000);
    }

    private void EndBonusStage2()
    {
        lock (this)
        {
            if (isBonusStage2Over)
                return;
            isBonusStage2Over = true;
        }
        DeleteAliveNpcs(217737, 217738);
        SpawnArenaRecordKeeper(799569); // IDArena_3rd_MC_Start
    }

    private void StartBonusStage3()
    {
        Sp(217742, FIGHTER_AI, 360.76f, 349.42f, 96.1f, (byte)0); // S3_Elemeltal_Fire_55_Ah
        Sp(217743, FIGHTER_AI, 346.27f, 363.35f, 96.1f, (byte)11); // S3_Elemeltal_Wind_55_Ah
        Sp(217740, FIGHTER_AI, 332.12f, 349.22f, 96.1f, (byte)0); // S3_Elemeltal_Earth_55_Ah
        Sp(217741, FIGHTER_AI, 346.42f, 335.1f, 96.1f, (byte)87); // S3_Elemeltal_Warter_55_Ah
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S3_Bonus_01(), 30000);
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S3_Bonus_02(), 50000);
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S3_Bonus_03(), 55000);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            DeleteAliveNpcs(217740, 217741, 217742, 217743);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(60000));
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            DeleteAliveNpcs(217744);
            SpawnArenaRecordKeeper(205331); // IDArena_4th_MC_Start
            Sp(217735, 378.9331f, 346.74878f, 96.74762f, (byte)0, 1000); // S3_ResurBox1_55_n
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(63000));
    }

    private void StartBonusStage4()
    {
        Sp(217778, 346.2f, 366.85f, 96.55f, (byte)1); // S4_Draky_CTRL_Gate
        int released = 0, epicRolls = 0;
        AtomicBoolean epicReleased = new AtomicBoolean();
        bonusStage4Task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            int n = Interlocked.Increment(ref released);
            int npcId;
            if (Rnd.Chance() < 25 && Interlocked.Increment(ref epicRolls) >= 3 && epicReleased.CompareAndSet(false, true))
                npcId = 217748; // S4_Draky_Epic_55_Ae
            else if (Rnd.Chance() < 20)
                npcId = 217745; // S4_Draky_Fast_55_An
            else if (Rnd.Chance() < 40)
                npcId = 217746; // S4_Draky_Mid_55_An
            else
                npcId = 217747; // S4_Draky_slow_55_An
            Sp(npcId, 346.2f, 366.85f, 96.09f, (byte)60);
            switch (n)
            {
                case 20:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S4_Bonus_01());
                    break;
                case 26:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S4_Bonus_02());
                    break;
                case 28:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S4_Bonus_03());
                    break;
                case 30:
                {
                    bonusStage4Task.Cancel(false);
                    DeleteAliveNpcs(217745, 217746, 217747, 217748, 217778);
                    Npc arminos = GetNpc(217749);
                    if (arminos != null)
                        PacketSendUtility.BroadcastMessage(arminos, 1500255);
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        DeleteAliveNpcs(217749);
                        Sp(205338, 345.25f, 349.24f, 96.09097f, (byte)0, 1000); // IDArena_4th_MC_End
                        return ValueTask.CompletedTask;
                    }, TimeSpan.FromMilliseconds(8000));
                    break;
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(3000), TimeSpan.FromMilliseconds(3000));
    }

    private void StartBonusStage6(Npc arminos)
    {
        if (bonusStage6Task != null)
            return;
        PacketSendUtility.BroadcastMessage(arminos, 1500256);
        ScheduleWhileAlive(217750, () => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S6_Bonus_01()), 30000);
        ScheduleWhileAlive(217750, () => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S6_Bonus_02()), 50000);
        ScheduleWhileAlive(217750, () => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S6_Bonus_03()), 55000);
        bonusStage6Task = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (arminos.IsDead())
                return ValueTask.CompletedTask;
            PacketSendUtility.BroadcastMessage(arminos, 1500258);
            arminos.GetController().Delete();
            DeleteAliveNpcs(217751, 217752);
            Sp(205340, 1625.08f, 159.15f, 126f, (byte)70, 1000); // IDArena_6th_MC_End
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(60000));
    }

    private void StartBonusStage9()
    {
        Sp(217754, "empyrean_dusk_draky", 1308.9f, 1765.3f, 316.1f, (byte)60, 8000); // S9_DuskDraky_55_Ah
        SetStage(StageType.START_BONUS_STAGE_9, 8000);
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S9_DuskDraky_55_Ah_01(), 8000);
        ScheduleWhileAlive(217754, () => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S9_Bonus_01()), 38000);
        ScheduleWhileAlive(217754, () => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S9_Bonus_02()), 58000);
        ScheduleWhileAlive(217754, () => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S9_Bonus_03()), 63000);
        bonusStage9Task = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!AllDead(217754))
            {
                DeleteAliveNpcs(217754);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S9_DuskDraky_55_Ah_02());
            }
            EndBonusStage9();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(68000));
    }

    private void EndBonusStage9()
    {
        lock (this)
        {
            if (bonusStage9Task == null)
                return;
            bonusStage9Task.Cancel(false);
            bonusStage9Task = null;
        }
        Sp(205343, 1309.6390f, 1732.6423f, 316.5f, (byte)6, 1000); // IDArena_9th_MC_End
    }

    private void StartStage4Round(int round)
    {
        switch (round)
        {
            case 2:
                SetStage(StageType.START_STAGE_4_ROUND_2, 3000);
                Sp(217560, 334.61942f, 334.80353f, 96.090904f, (byte)15, 8000); // S5_Naga_Fi_55_Ae
                Sp(217559, 330.53665f, 349.23523f, 96.09093f, (byte)0, 8000); // S5_Lizard_Pr_55_Ae
                Sp(217562, 334.89508f, 363.78442f, 96.090904f, (byte)105, 8000); // S5_Naga_Wi_55_Ae
                Sp(217557, 357.24625f, 338.30093f, 96.09104f, (byte)45, 48000); // S5_Lizard_Fi_55_Ae
                Sp(217561, 365.109f, 349.1218f, 96.09114f, (byte)60, 48000); // S5_Naga_Ch_55_Ae
                Sp(217558, 357.20663f, 359.28714f, 96.091064f, (byte)75, 48000); // S5_Lizard_As_55_Ae
                break;
            case 3:
                SetStage(StageType.START_STAGE_4_ROUND_3, 3000);
                Sp(217563, 339.70975f, 333.54272f, 96.090904f, (byte)20, 8000); // S5_Drakan_Fi_55_Ae
                Sp(217564, 342.92892f, 333.43994f, 96.09092f, (byte)18, 8000); // S5_Drakan_As_55_Ae
                Sp(217565, 341.55396f, 330.70847f, 96.09093f, (byte)23, 15000); // S5_Drakan_Wi_55_Ae
                Sp(217566, 362.87164f, 357.87164f, 96.091125f, (byte)73, 48000); // S5_Drakan_Pr_55_Ae
                Sp(217563, 359.1135f, 359.6953f, 96.091125f, (byte)80, 48000); // S5_Drakan_Fi_55_Ae
                break;
            case 4:
                SetStage(StageType.START_STAGE_4_ROUND_4, 3000);
                Sp(217567, 345.73895f, 349.49786f, 96.09097f, (byte)0, 8000); // S5_Drakan_Fi_Boss_55_Ah
                break;
            case 5:
                SetStage(StageType.START_STAGE_4_ROUND_5, 3000);
                Sp(217653, 327.76917f, 349.26215f, 96.09092f, (byte)0, 8000); // S5_Drop_Drakan_Wi_55_Ae
                Sp(217651, 364.8972f, 349.25653f, 96.09114f, (byte)60, 21000); // S5_Drop_Drakan_Fi_55_Ae
                Sp(217652, 361.1795f, 339.99252f, 96.09112f, (byte)50, 37000); // S5_Drop_Drakan_As_55_Ae
                Sp(217653, 354.4119f, 333.6749f, 96.09091f, (byte)40, 56000); // S5_Drop_Drakan_Wi_55_Ae
                Sp(217651, 331.61502f, 358.4374f, 96.09091f, (byte)110, 70000); // S5_Drop_Drakan_Fi_55_Ae
                Sp(217652, 338.38858f, 364.91507f, 96.090904f, (byte)100, 85000); // S5_Drop_Drakan_As_55_Ae
                Sp(217651, 346.39847f, 368.19427f, 96.090904f, (byte)90, 102000); // S5_Drop_Drakan_Fi_55_Ae
                Sp(217652, 353.92606f, 364.92636f, 96.090904f, (byte)80, 114000); // S5_Drop_Drakan_As_55_Ae
                Sp(217653, 361.13452f, 358.90424f, 96.091156f, (byte)70, 133000); // S5_Drop_Drakan_Wi_55_Ae
                Sp(217652, 346.34402f, 329.9449f, 96.09091f, (byte)30, 145000); // S5_Drop_Drakan_As_55_Ae
                Sp(217654, 338.7f, 333.3f, 96.09091f, (byte)20, 161000); // S5_Drop_Drakan_Pr_55_Ae
                Sp(217653, 331.53894f, 339.8832f, 96.09091f, (byte)10, 178000); // S5_Drop_Drakan_Wi_55_Ae
                break;
        }
    }

    private void StartStage2Round(int round)
    {
        StageType type = round switch
        {
            2 => StageType.START_STAGE_2_ROUND_2,
            3 => StageType.START_STAGE_2_ROUND_3,
            4 => StageType.START_STAGE_2_ROUND_4,
            _ => StageType.START_STAGE_2_ROUND_5,
        };
        SetStage(type, 3000);
        bool k = isKrallSet;
        switch (round)
        {
            case 2:
                Sp(k ? 217495 : 217504, 328.908f, 351.618f, 96.09093f, (byte)0, 8000); // S2_BrownieMSc_55_Ae / S2_RatmanScout_55_Ae
                Sp(k ? 217497 : 217508, 329.019f, 343.793f, 96.09093f, (byte)0, 8000); // S2_KrallWarrior_55_Ae / S2_LycanFighter_55_Ae
                Sp(k ? 217499 : 217507, 329.285f, 355.231f, 96.09093f, (byte)0, 8000); // S2_KrallScout_55_Ae / S2_LycanRanger_55_Ae
                Sp(k ? 217494 : 217502, 328.784f, 348.774f, 96.09093f, (byte)0, 8000); // S2_BrownieFWa_55_Ae / S2_RatmanWarrior_55_Ae
                break;
            case 3:
                Sp(k ? 217500 : 217509, 332.243f, 349.493f, 96.09093f, (byte)0, 8000); // S2_KrallScout_MidBoss_55_Ah / S2_LycanRanger_Named_55_Ah
                break;
            case 4:
                Sp(k ? 217499 : 217505, 341.951f, 334.777f, 96.09093f, (byte)0, 8000); // S2_KrallScout_55_Ae / S2_LycanShaman_55_Ae
                Sp(k ? 217497 : 217508, 344.178f, 334.425f, 96.09093f, (byte)0, 8000); // S2_KrallWarrior_55_Ae / S2_LycanFighter_55_Ae
                Sp(k ? 217498 : 217508, 334.068f, 339.844f, 96.09093f, (byte)0, 30000); // S2_KrallShaman_55_Ae / S2_LycanFighter_55_Ae
                Sp(k ? 217497 : 217506, 342.124f, 364.492f, 96.09093f, (byte)0, 35000); // S2_KrallWarrior_55_Ae / S2_LycanPriest_55_Ae
                Sp(k ? 217498 : 217507, 344.495f, 365.144f, 96.09093f, (byte)0, 35000); // S2_KrallShaman_55_Ae / S2_LycanRanger_55_Ae
                break;
            case 5:
                Sp(k ? 217501 : 217510, 332.004f, 349.559f, 96.09093f, (byte)0, 8000); // S2_KrallWarrior_Boss_55_Ah / S2_LycanWizard_Boss_55_Ah
                break;
        }
    }

    private void StartLehparRound(int round)
    {
        StageType type = round switch
        {
            2 => StageType.START_STAGE_5_ROUND_2,
            3 => StageType.START_STAGE_5_ROUND_3,
            4 => StageType.START_STAGE_5_ROUND_4,
            _ => StageType.START_STAGE_5_ROUND_5,
        };
        SetStage(type, 3000);
        switch (round)
        {
            case 2:
                Sp(217530, 1244.810f, 796.650f, 358.6056f, (byte)0, 8000); // S4_hippolizard_work_55_Ae
                Sp(217535, 1246.020f, 788.834f, 358.6056f, (byte)12, 8000); // S4_LehparL_Ra_55_Ae
                Sp(217530, 1255.182f, 783.183f, 358.6056f, (byte)27, 8000); // S4_hippolizard_work_55_Ae
                Sp(217531, 1244.836f, 791.915f, 358.6056f, (byte)2, 38000); // S4_LehparL_Fi_55_Ae
                Sp(217536, 1248.894f, 786.183f, 358.6056f, (byte)20, 38000); // S4_LehparL_Pr_55_Ae
                break;
            case 3:
                Sp(217543, 1259.471f, 812.305f, 358.6056f, (byte)30, 8000); // S4_Lehpar_Fi_MidBoss_55_Ah
                break;
            case 4:
                Sp(217533, 1258.720f, 792.232f, 358.6056f, (byte)27, 8000); // S4_LehparL_As_55_Ae
                Sp(217534, 1243.188f, 796.796f, 358.6056f, (byte)0, 8000); // S4_LehparL_Wi_55_Ae
                Sp(217535, 1272.299f, 797.088f, 358.6056f, (byte)60, 8000); // S4_LehparL_Ra_55_Ae
                Sp(217536, 1268.029f, 796.001f, 358.6056f, (byte)60, 38000); // S4_LehparL_Pr_55_Ae
                Sp(217532, 1245.805f, 801.722f, 358.6056f, (byte)0, 38000); // S4_LehparL_Kn_55_Ae
                break;
            case 5:
                Sp(217544, "lehpar_captain", 1259.839f, 785.627f, 358.6056f, (byte)30, 8000); // S4_Lehpar_Fi_Boss_55_Ah
                break;
        }
    }

    /// <summary>
    /// Ends the game once no group member in this world fights any more. Players who logged out inside still count.
    /// Departs from retail: retail runs this check before removing the leaving player, so the last fighter leaving the instance without
    /// the training exit button still counts as alive, and whoever waits in the resurrection area gets no result until they leave
    /// themselves. We leave the departing player out. To match retail, pass null on leave as well.
    /// </summary>
    private void CheckAllDefeated(Player? leaving)
    {
        foreach (int objectId in inWorld.Keys)
        {
            if (leaving != null && leaving.GetObjectId() == objectId)
                continue;
            CruciblePlayerReward reward = GetPlayerReward(objectId);
            if (reward != null && !reward.IsPlayerDefeated() && !reward.IsRewarded())
                return;
        }
        GameOver();
    }

    private void GameOver()
    {
        lock (this)
        {
            if (gameOver)
                return;
            gameOver = true;
        }
        instanceScore.SetInstanceProgressionType(InstanceProgressionType.END_PROGRESS);
        foreach (Player player in instance.GetPlayersInside().Where(p => p.IsOnline()).ToList())
            DoReward(player);
        BroadcastScore();
    }

    public override void DoReward(Player player)
    {
        CruciblePlayerReward playerReward = GetPlayerReward(player.GetObjectId());
        if (playerReward == null || playerReward.IsRewarded())
            return;
        int reward = (int)(0.01f * playerReward.GetPoints() + 350);
        playerReward.SetRewarded();
        playerReward.SetInsignia(reward);
        ItemService.AddItem(player, 186000130, reward);
    }

    public override void OnInstanceDestroy()
    {
        base.OnInstanceDestroy();
        npcs.Clear();
    }

    public override bool OnDie(Player player, Creature lastAttacker)
    {
        base.OnDie(player, lastAttacker);
        GetPlayerReward(player.GetObjectId()).SetPlayerDefeated(true);
        CheckAllDefeated(null);
        BroadcastScore();
        return true;
    }

    public override bool OnReviveEvent(Player player)
    {
        base.OnReviveEvent(player);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_MOVE_BIRTHAREA_ME_IDARENA());
        PacketSendUtility.BroadcastPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_MOVE_BIRTHAREA_FRIENDLY_IDARENA(player.GetName()));
        MoveToReadyRoom(player);
        return true;
    }

    private Race GetRegisteredTeamRace()
    {
        if (instance.GetRegisteredTeam() != null)
        {
            return instance.GetRegisteredTeam().GetRace();
        }
        if (instance.GetPlayersInside().Count > 0)
        {
            return instance.GetPlayersInside()[0].GetRace();
        }
        return Race.ELYOS;
    }

    public override void OnChangeStage(StageType type)
    {
        switch (type)
        {
            case StageType.START_STAGE_1_ELEVATOR:
            {
                SetStage(type, 0);
                SetStage(StageType.START_STAGE_1_ROUND_1, 3000);
                stage = 1;
                Race race = GetRegisteredTeamRace();
                Interlocked.Exchange(ref stageKills, 0);
                int npcId1 = race == Race.ASMODIANS ? 217486 : 217477; // S1_TempleD_Kn_55_Ae / S1_TempleL_Kn_55_Ae
                int npcId2 = race == Race.ASMODIANS ? 217489 : 217480; // S1_TempleD_Wi_55_Ae / S1_TempleL_Wi_55_Ae
                Sp(npcId1, FIGHTER_AI, 327.73657f, 347.96228f, 96.09092f, (byte)0, 8000);
                Sp(npcId2, FIGHTER_AI, 327.81943f, 350.948f, 96.09093f, (byte)0, 8000);
                break;
            }
            case StageType.START_STAGE_2_ELEVATOR:
                SetStage(type, 0);
                DeleteAliveNpcs(217734, 217756);
                stage = 2;
                SetStage(StageType.START_STAGE_2_ROUND_1, 3000);
                isKrallSet = Rnd.NextBoolean();
                Interlocked.Exchange(ref stageKills, 0);
                Sp(isKrallSet ? 217496 : 217503, 325.712f, 352.810f, 96.09093f, (byte)0, 8000); // S2_BrownieMWo_55_Ae / S2_RatmanGatherer_55_Ae
                Sp(isKrallSet ? 217495 : 217504, 325.061f, 349.478f, 96.09093f, (byte)0, 8000); // S2_BrownieMSc_55_Ae / S2_RatmanScout_55_Ae
                Sp(isKrallSet ? 217494 : 217502, 325.787f, 346.073f, 96.09093f, (byte)0, 8000); // S2_BrownieFWa_55_Ae / S2_RatmanWarrior_55_Ae
                break;
            case StageType.START_STAGE_3_ELEVATOR:
                SetStage(type, 0);
                SetStage(StageType.START_STAGE_3_ROUND_1, 3000);
                stage = 3;
                Sp(217512, 344.23056f, 347.89594f, 96.09096f, (byte)0, 8000); // S3_ElementalFire_1st_55_Ae
                Sp(217513, 341.09082f, 337.95187f, 96.09097f, (byte)0, 8000); // S3_ElementalWater_1st_55_Ae
                Sp(217512, 342.06656f, 361.16135f, 96.090935f, (byte)0, 8000); // S3_ElementalFire_1st_55_Ae
                Sp(217511, 356.75006f, 335.27487f, 96.09096f, (byte)0, 8000); // S3_ElementalEarth_1st_55_Ae
                Sp(217514, 345.4355f, 365.05215f, 96.09093f, (byte)0, 8000); // S3_ElementalAir_1st_55_Ae
                Sp(217512, 352.8222f, 358.33463f, 96.09092f, (byte)0, 8000); // S3_ElementalFire_1st_55_Ae
                Sp(217513, 342.32755f, 365.00473f, 96.09093f, (byte)0, 8000); // S3_ElementalWater_1st_55_Ae
                Sp(217514, 356.19113f, 362.22543f, 96.090965f, (byte)0, 8000); // S3_ElementalAir_1st_55_Ae
                Sp(217511, 344.25127f, 334.1194f, 96.090935f, (byte)0, 8000); // S3_ElementalEarth_1st_55_Ae
                Sp(217511, 344.07086f, 346.8839f, 96.09092f, (byte)0, 8000); // S3_ElementalEarth_1st_55_Ae
                Sp(217514, 334.01746f, 350.76382f, 96.090935f, (byte)0, 8000); // S3_ElementalAir_1st_55_Ae
                Sp(217513, 344.49155f, 351.73932f, 96.09093f, (byte)0, 8000); // S3_ElementalWater_1st_55_Ae
                Sp(217513, 353.0832f, 362.178f, 96.09092f, (byte)0, 8000); // S3_ElementalWater_1st_55_Ae
                Sp(217511, 356.24454f, 358.34552f, 96.09103f, (byte)0, 8000); // S3_ElementalEarth_1st_55_Ae
                Sp(217512, 330.64853f, 346.87302f, 96.09091f, (byte)0, 8000); // S3_ElementalFire_1st_55_Ae
                Sp(217512, 353.32773f, 335.26398f, 96.09092f, (byte)0, 8000); // S3_ElementalFire_1st_55_Ae
                Sp(217514, 356.69666f, 339.1548f, 96.09103f, (byte)0, 8000); // S3_ElementalAir_1st_55_Ae
                Sp(217511, 347.6529f, 347.90683f, 96.09098f, (byte)0, 8000); // S3_ElementalEarth_1st_55_Ae
                Sp(217514, 347.5995f, 351.78674f, 96.09099f, (byte)0, 8000); // S3_ElementalAir_1st_55_Ae
                Sp(217512, 340.82983f, 334.1085f, 96.09093f, (byte)0, 8000); // S3_ElementalFire_1st_55_Ae
                Sp(217514, 344.19876f, 337.9993f, 96.09094f, (byte)0, 8000); // S3_ElementalAir_1st_55_Ae
                Sp(217513, 353.5887f, 339.10763f, 96.09092f, (byte)0, 8000); // S3_ElementalWater_1st_55_Ae
                Sp(217511, 345.4889f, 361.17224f, 96.090935f, (byte)0, 8000); // S3_ElementalEarth_1st_55_Ae
                Sp(217513, 330.90952f, 350.7164f, 96.09093f, (byte)0, 8000); // S3_ElementalWater_1st_55_Ae
                break;
            case StageType.START_STAGE_4_ELEVATOR:
                SetStage(type, 0);
                DeleteAliveNpcs(217735);
                SetStage(StageType.START_STAGE_4_ROUND_1, 3000);
                stage = 4;
                Interlocked.Exchange(ref stageKills, 0);
                Sp(217557, 328.88104f, 349.55392f, 96.090904f, (byte)0, 8000); // S5_Lizard_Fi_55_Ae
                Sp(217559, 328.38922f, 342.39066f, 96.09091f, (byte)5, 8000); // S5_Lizard_Pr_55_Ae
                Sp(217557, 333.17947f, 336.4504f, 96.090904f, (byte)8, 8000); // S5_Lizard_Fi_55_Ae
                Sp(217558, 330.27792f, 339.2779f, 96.09093f, (byte)7, 38000); // S5_Lizard_As_55_Ae
                Sp(217558, 328.08972f, 346.3553f, 96.090904f, (byte)2, 38000); // S5_Lizard_As_55_Ae
                break;
            case StageType.START_STAGE_5_ROUND_1:
            {
                SetStage(type, 3000);
                Interlocked.Exchange(ref stageKills, 0);
                bool isLehparSet = Rnd.Get(1, 10) <= 3;
                if (isLehparSet)
                {
                    Sp(217529, 1257.307f, 778.350f, 358.6056f, (byte)30, 8000); // S4_Pretor_55_Ae
                    Sp(217529, 1260.138f, 778.846f, 358.6056f, (byte)30, 8000); // S4_Pretor_55_Ae
                    Sp(217529, 1263.199f, 778.413f, 358.6056f, (byte)30, 8000); // S4_Pretor_55_Ae
                    Sp(217529, 1254.261f, 778.382f, 358.6056f, (byte)30, 38000); // S4_Pretor_55_Ae
                    Sp(217529, 1266.293f, 778.325f, 358.6056f, (byte)30, 38000); // S4_Pretor_55_Ae
                }
                else
                {
                    Sp(217549, 1263.1987f, 778.4129f, 358.6056f, (byte)30, 8000); // S4_ShulackWi_55_Ae
                    Sp(217548, 1260.1381f, 778.84644f, 358.60562f, (byte)30, 8000); // S4_ShulackRa_55_Ae
                    Sp(217547, 1257.3065f, 778.35016f, 358.60562f, (byte)30, 8000); // S4_ShulackAs_55_Ae
                    Sp(217550, 1266.293f, 778.3254f, 358.60574f, (byte)30, 38000); // S4_ShulackPr_55_Ae
                    Sp(217545, 1254.261f, 778.3817f, 358.6056f, (byte)30, 38000); // S4_ShulackFi_55_Ae
                }
                break;
            }
            case StageType.START_STAGE_6_ROUND_1:
                SetStage(type, 3000);
                Sp(217568, 1636.7102f, 166.87984f, 126f, (byte)60, 8000); // S5_Graveknight_55_Ae
                Sp(217568, 1636.6416f, 164.15344f, 126f, (byte)60, 8000); // S5_Graveknight_55_Ae
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    Sp(217568, 1638.7107f, 165.40533f, 126f, (byte)60); // S5_Graveknight_55_Ae
                    Sp(217568, 1638.6783f, 162.67389f, 126f, (byte)60); // S5_Graveknight_55_Ae
                    isDoneStage6Round1 = true;
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(18000));
                break;
            case StageType.START_STAGE_6_ROUND_5:
                SetStage(type, 0);
                break;
            case StageType.START_STAGE_7_ROUND_1:
            {
                SetStage(type, 3000);
                int npcId = GetRegisteredTeamRace() == Race.ASMODIANS ? 217582 : 217578; // S7_MasterD_Wa_55_Ah / S7_MasterL_Wa_55_Ah
                Sp(npcId, "warrior_preceptor", 1794.9f, 812.0f, 469.35f, (byte)80, 8000);
                break;
            }
            case StageType.START_STAGE_8_ROUND_1:
                SetStage(type, 3000);
                Sp(Rnd.NextBoolean() ? 217588 : 217589, 1776.578f, 1773.231f, 303.695f, (byte)90, 8000); // S8_Cromede_55_Ah, S8_Cromede_Hard_55_Ah
                break;
            case StageType.START_STAGE_9_ROUND_1:
                SetStage(type, 3000);
                Sp(217594, 1274.890f, 1730.676f, 318.194f, (byte)3, 8000); // S9_Potcrab_Gi_55_Ah
                break;
            case StageType.START_STAGE_10_ROUND_1:
                SetStage(type, 3000);
                Interlocked.Exchange(ref stageKills, 0);
                Sp(217600, 1769.255f, 1305.830f, 394.3f, (byte)77, 8000); // S10_Naga_Fi_High_55_Ae
                Sp(217602, 1773.790f, 1302.550f, 394.3f, (byte)77, 8000); // S10_Naga_Pr_High_55_Ae
                Sp(217601, 1777.328f, 1298.115f, 394.3f, (byte)77, 8000); // S10_Naga_Ma_High_55_Ae
                Sp(217600, 1781.384f, 1295.471f, 394.3f, (byte)77, 38000); // S10_Naga_Fi_High_55_Ae
                Sp(217600, 1764.249f, 1306.207f, 394.3f, (byte)77, 38000); // S10_Naga_Fi_High_55_Ae
                Sp(217602, 1772.114f, 1299.004f, 394.3f, (byte)77, 70000); // S10_Naga_Pr_High_55_Ae
                Sp(217601, 1775.701f, 1295.118f, 394.3f, (byte)77, 70000); // S10_Naga_Ma_High_55_Ae
                Sp(217600, 1767.737f, 1300.925f, 394.3f, (byte)77, 70000); // S10_Naga_Fi_High_55_Ae
                Sp(217600, 1763.040f, 1303.508f, 394.3f, (byte)77, 90000); // S10_Naga_Fi_High_55_Ae
                Sp(217600, 1780.297f, 1292.267f, 394.3f, (byte)77, 90000); // S10_Naga_Fi_High_55_Ae
                break;
            case StageType.START_STAGE_5:
                stage = 5;
                SendScene(200000);
                Sp(205426, 1256.2872f, 834.28986f, 358.60565f, (byte)103); // IDArena_5th_Resurrect_NPC
                Sp(205332, 1260.1292f, 795.06964f, 358.60562f, (byte)30); // IDArena_5th_MC_Start
                MoveGroupToStage();
                SpawnPickups();
                SetStage(type, 1000);
                break;
            case StageType.START_STAGE_6:
                stage = 6;
                SendScene(300000);
                Sp(205427, 1594.4756f, 145.26898f, 128.67778f, (byte)16); // IDArena_6th_Resurrect_NPC
                Sp(205333, 1625.1771f, 159.15244f, 126f, (byte)70); // IDArena_6th_MC_Start
                MoveGroupToStage();
                SpawnPickups();
                SetStage(type, 1000);
                break;
            case StageType.START_STAGE_7:
                stage = 7;
                SendScene(400000);
                Sp(205428, 1820.39f, 800.81805f, 470.1394f, (byte)86); // IDArena_7th_Resurrect_NPC
                Sp(205334, 1781.6106f, 796.9224f, 469.35016f, (byte)0); // IDArena_7th_MC_Start
                MoveGroupToStage();
                SpawnPickups();
                SetStage(type, 1000);
                break;
            case StageType.START_STAGE_8:
                stage = 8;
                SendScene(500000);
                Sp(205429, 1780.103f, 1723.458f, 304.039f, (byte)53); // IDArena_8th_Resurrect_NPC
                Sp(205335, 1776.759f, 1764.705f, 303.695f, (byte)90); // IDArena_8th_MC_Start
                MoveGroupToStage();
                SpawnPickups();
                SetStage(type, 1000);
                break;
            case StageType.START_STAGE_9:
                stage = 9;
                SendScene(600000);
                Sp(205430, 1359.375f, 1758.057f, 319.625f, (byte)90); // IDArena_9th_Resurrect_NPC
                Sp(205336, 1309.309f, 1732.540f, 315.782f, (byte)7); // IDArena_9th_MC_Start
                MoveGroupToStage();
                SpawnPickups();
                SetStage(type, 1000);
                break;
            case StageType.START_STAGE_10:
                stage = 10;
                SendScene(700000);
                Sp(205431, 1750.81f, 1252.47f, 394.2378f, (byte)45); // IDArena_10th_Resurrect_NPC
                Sp(205337, "empyreanrecordkeeper", 1766.2f, 1292.0f, 394.23755f, (byte)75, 1000); // IDArena_10th_MC_Start
                MoveGroupToStage();
                SpawnPickups();
                SetStage(type, 1000);
                break;
            case StageType.PASS_GROUP_STAGE_10:
                GameOver();
                break;
        }
    }

    private void AdvanceStage1(Npc npc)
    {
        if (!stage1Advanced.TryAdd(npc.GetObjectId(), 0))
            return;
        switch (Interlocked.Increment(ref stageKills))
        {
            case 2: StartStage1Round(2); break;
            case 3: StartStage1Round(3); break;
            case 4: StartStage1Round(4); break;
            case 5: StartStage1Round(5); break;
        }
    }

    private void StartStage1Round(int round)
    {
        bool asmo = GetRegisteredTeamRace() == Race.ASMODIANS;
        switch (round)
        {
            case 2:
                SetStage(StageType.START_STAGE_1_ROUND_2, 3000);
                Sp(asmo ? 217492 : 217483, FIGHTER_AI, 332.7714f, 358.48206f, 96.09092f, (byte)107, 8000); // S1_TempleD_Ch_55_Ae / S1_TempleL_Ch_55_Ae
                break;
            case 3:
                SetStage(StageType.START_STAGE_1_ROUND_3, 3000);
                Sp(asmo ? 217487 : 217478, FIGHTER_AI, 334.844f, 339.92618f, 96.09094f, (byte)18, 8000); // S1_TempleD_As_55_Ae / S1_TempleL_As_55_Ae
                break;
            case 4:
                SetStage(StageType.START_STAGE_1_ROUND_4, 3000);
                Sp(asmo ? 217491 : 217482, FIGHTER_AI, 341.03156f, 361.04315f, 96.09093f, (byte)90, 8000); // S1_TempleD_Pr_55_Ae / S1_TempleL_Pr_55_Ae
                break;
            case 5:
                SetStage(StageType.START_STAGE_1_ROUND_5, 3000);
                Sp(asmo ? 217493 : 217484, FIGHTER_AI, 332.093f, 349.36847f, 96.090935f, (byte)0, 8000); // S1_TempleD_Fi_Boss_55_Ah / S1_TempleL_Fi_Boss_55_Ah
                break;
        }
    }

    private bool AllDead(params int[] npcIds)
    {
        return instance.GetNpcs(npcIds).All(npc => npc.IsDead());
    }

    private void ScheduleWhileAlive(int npcId, Action action, int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!AllDead(npcId))
                action();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delay));
    }

    private void SpawnArenaRecordKeeper(int npcId)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Sp(npcId, 345.25f, 349.24f, 96.09097f, (byte)0);
            MoveGroupToStage();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(1000));
    }

    private void SendScene(int sceneStatus)
    {
        instance.ForEachPlayer(player => PacketSendUtility.SendPacket(player, new SM_INSTANCE_STAGE_INFO(2, sceneStatus & 0xFFFF, (int)((uint)sceneStatus >> 16))));
    }

    private void MoveGroupToStage()
    {
        foreach (Player playerInside in instance.GetPlayersInside().ToList())
            if (playerInside.IsOnline())
                PortToStartPosition(playerInside);
    }

    /// <summary>Fighters go to the arena of the current stage, everyone else to its waiting room.</summary>
    public override void PortToStartPosition(Player player)
    {
        CruciblePlayerReward reward = GetPlayerReward(player.GetObjectId());
        if (reward == null)
            return;
        if (reward.IsPlayerDefeated() || reward.IsRewarded())
            MoveToReadyRoom(player);
        else
            MoveToStageStart(player);
        BroadcastScore();
    }

    private void MoveToStageStart(Player player)
    {
        switch (stage)
        {
            case 1:
            case 2:
            case 3:
            case 4:
                Teleport(player, 354.79f, 349.35f, 96.09097f, (byte)60);
                break;
            case 5: Teleport(player, 1260.15f, 812.34f, 358.6056f, (byte)90); break;
            case 6: Teleport(player, 1616.02f, 154.44f, 126f, (byte)10); break;
            case 7: Teleport(player, 1793.92f, 796.92f, 469.36542f, (byte)60); break;
            case 8: Teleport(player, 1776.79f, 1755.59f, 303.69553f, (byte)30); break;
            case 9: Teleport(player, 1320.45f, 1738.48f, 316.1746f, (byte)67); break;
            case 10: Teleport(player, 1762.43f, 1284.99f, 394.23764f, (byte)15); break;
        }
    }

    private void MoveToReadyRoom(Player player)
    {
        switch (stage)
        {
            case 1:
            case 2:
            case 3:
            case 4:
                Teleport(player, 383.54f, 349.19f, 96.74763f, (byte)23);
                break;
            case 5: Teleport(player, 1260.14f, 832.06f, 358.60562f, (byte)43); break;
            case 6: Teleport(player, 1596.60f, 148.47f, 128.81355f, (byte)77); break;
            case 7: Teleport(player, 1819.54f, 796.74f, 470.18304f, (byte)27); break;
            case 8: Teleport(player, 1776.38f, 1724.55f, 304.039f, (byte)113); break;
            case 9: Teleport(player, 1359.50f, 1751.80f, 319.59406f, (byte)30); break;
            case 10: Teleport(player, 1748.26f, 1254.75f, 394.2378f, (byte)105); break;
        }
    }

    /// <summary>
    /// On each move to the next stage, the previous arena and waiting room get sensors and assistant record keepers that send stragglers
    /// after the group. They stay until the following move.
    /// </summary>
    private void SpawnPickups()
    {
        DeleteAliveNpcs(205506, 205507, 205508, 205509, 205510, 205511, 205512, 205513, 205514, 205515, 205516, 205517, 206185, 206186, 206187,
            206188, 206189, 206190, 206191, 206192, 206193, 206194, 206195, 206196);
        string ai = "empyrean_straggler_pickup";
        switch (stage)
        {
            case 5:
                Sp(206185, ai, 355.12f, 349.36f, 96.09097f, (byte)0, 10000);
                Sp(206191, ai, 384.09f, 349.47f, 96.74763f, (byte)0, 10000);
                Sp(205506, ai, 345.33f, 353.24f, 96.09097f, (byte)0, 15000);
                Sp(205512, ai, 384.10f, 349.06f, 96.74763f, (byte)60, 15000);
                break;
            case 6:
                Sp(206186, ai, 1261.01f, 812.38f, 358.6056f, (byte)0, 10000);
                Sp(206192, ai, 1261.07f, 831.68f, 358.6056f, (byte)0, 10000);
                Sp(205507, ai, 1256.14f, 795.12f, 358.6056f, (byte)30, 15000);
                Sp(205513, ai, 1261.11f, 835.70f, 358.6056f, (byte)90, 15000);
                break;
            case 7:
                Sp(206187, ai, 1615.20f, 153.83f, 126f, (byte)0, 10000);
                Sp(206193, ai, 1595.82f, 146.91f, 128.81355f, (byte)0, 10000);
                Sp(205508, ai, 1626.77f, 155.78f, 126f, (byte)70, 15000);
                Sp(205514, ai, 1590.20f, 147.73f, 128.81355f, (byte)7, 15000);
                break;
            case 8:
                Sp(206188, ai, 1793.57f, 797.87f, 469.35f, (byte)0, 10000);
                Sp(206194, ai, 1819.39f, 796.95f, 470.18304f, (byte)0, 10000);
                Sp(205509, ai, 1781.74f, 800.80f, 469.35f, (byte)0, 15000);
                Sp(205515, ai, 1822.52f, 797.05f, 470.18304f, (byte)60, 15000);
                break;
            case 9:
                Sp(206189, ai, 1777.44f, 1754.37f, 303.695f, (byte)0, 10000);
                Sp(206195, ai, 1776.32f, 1724.09f, 304.039f, (byte)0, 10000);
                Sp(205510, ai, 1780.47f, 1764.70f, 303.695f, (byte)90, 15000);
                Sp(205516, ai, 1776.04f, 1721.40f, 304.039f, (byte)30, 15000);
                break;
            case 10:
                Sp(206190, ai, 1321.10f, 1738.41f, 316.1746f, (byte)0, 10000);
                Sp(206196, ai, 1358.80f, 1750.57f, 319.59406f, (byte)0, 10000);
                Sp(205511, ai, 1308.18f, 1736.28f, 315.782f, (byte)7, 15000);
                Sp(205517, ai, 1363.10f, 1753.32f, 319.59406f, (byte)67, 15000);
                break;
        }
    }

    private void SpawnLever(int npcId)
    {
        if (AllDead(217553))
            return;
        switch (npcId)
        {
            case 700527: // IDshulackship_Lever_A
                switch (Rnd.Get(1, 3))
                {
                    case 1: Sp(npcId, SUPPLY_AI, 1253.31f, 789.38f, 358.60562f, (byte)119); break;
                    case 2: Sp(npcId, SUPPLY_AI, 1253.66f, 804.91f, 358.60562f, (byte)119); break;
                    default: Sp(npcId, SUPPLY_AI, 1269.83f, 796.32f, 358.60562f, (byte)119); break;
                }
                break;
            case 700528: // IDshulackship_Lever_B
                switch (Rnd.Get(1, 4))
                {
                    case 1: Sp(npcId, SUPPLY_AI, 1248.94f, 800.99f, 358.6056f, (byte)16); break;
                    case 2: Sp(npcId, SUPPLY_AI, 1268.03f, 789.64f, 358.6056f, (byte)16); break;
                    case 3: Sp(npcId, SUPPLY_AI, 1264.92f, 804.84f, 358.6056f, (byte)16); break;
                    default: Sp(npcId, SUPPLY_AI, 1260.20f, 800.15f, 358.6056f, (byte)16); break;
                }
                break;
        }
    }

    private void StartGolemSwitches(Npc golem)
    {
        if (golemTask != null)
            return;
        golemTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (golem.IsDead() || !golem.IsSpawned())
                return ValueTask.CompletedTask;
            float roll = Rnd.Chance();
            Npc? booster; // BIDLF1_WaterGolem1_Switch1
            if (roll < 30)
                booster = Sp(700439, "noaction", 1788f, 1283f, 394.237f, (byte)38);
            else if (roll < 51)
                booster = Sp(700439, "noaction", 1747f, 1304f, 394.237f, (byte)15);
            else
                booster = Sp(700439, "noaction", 1754f, 1267f, 394.237f, (byte)27);
            if (booster != null)
            {
                Skill mirror = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(booster, 18553, 41, booster);
                if (mirror != null)
                    mirror.UseNoAnimationSkill();
            }
            golem.QueueSkill(18110, 41);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (!golem.IsDead())
                    golem.QueueSkill(18112, 41, 0, NpcSkillTargetAttribute.RANDOM);
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(10000));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(10000), TimeSpan.FromMilliseconds(30000));
    }

    public override void LeaveInstance(Player player)
    {
        TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    public override void OnLeaveInstance(Player player)
    {
        base.OnLeaveInstance(player);
        CheckAllDefeated(player);
        inWorld.TryRemove(player.GetObjectId(), out _);
    }

    public override void OnStopTraining(Player player)
    {
        DoReward(player);
        BroadcastScore();
        instance.ForEachPlayer(p => PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_MSG_FRIENDLY_LEAVE_IDARENA(player.GetName())));
    }

    private void Sp(int npcId, float x, float y, float z, byte h, int time)
    {
        Sp(npcId, null, x, y, z, h, time);
    }

    private void Sp(int npcId, string? aiName, float x, float y, float z, byte h, int time)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Sp(npcId, aiName, x, y, z, h);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(time));
    }

    private Npc? Sp(int npcId, float x, float y, float z, byte h)
    {
        return Sp(npcId, null, x, y, z, h);
    }

    private Npc? Sp(int npcId, string? aiName, float x, float y, float z, byte h)
    {
        Npc? npc = null;
        if (!isInstanceDestroyed)
        {
            npc = (Npc)Spawn(npcId, x, y, z, h, aiName);
            lock (npcs)
            {
                npcs.Add(npc);
            }
        }
        return npc;
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 217734:
            case 217735:
            case 217736:
            case 217756:
                ItemService.AddItem(player, 186000124, 1);
                npc.GetController().Delete();
                break;
            case 700527:
            case 700528: // two players pulled: feed or water comes out, the device comes back elsewhere while the giant lives
                Sp(npc.GetNpcId() == 700527 ? 281128 : 281129, "empyrean_manduri_food", npc.GetX(), npc.GetY(), npc.GetZ(), (byte)npc.GetHeading());
                npc.GetController().Delete();
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    SpawnLever(npc.GetNpcId());
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(Rnd.Get(5000, 6000)));
                break;
        }
    }

    public override void OnDropRegistered(Npc npc, int winnerObj)
    {
        HashSet<DropItem> dropItems = DropRegistrationService.GetInstance().GetCurrentDropMap()[npc.GetObjectId()];
        int npcId = npc.GetNpcId();
        int itemId = 0;
        switch (npcId)
        {
            case 217737: // King Saam
                dropItems.Clear();
                itemId = 188051349;
                break;
            case 217738:
                dropItems.Clear();
                itemId = Rnd.Chance() < 10 ? 188051349 : 162000107;
                break;
            case 217740:
            case 217741:
            case 217742:
            case 217743:
                dropItems.Clear();
                itemId = Rnd.Chance() < 20 ? npcId switch
                {
                    217740 => 125002593,
                    217741 => 125002595,
                    217742 => 125002592,
                    _ => 125002594,
                } : 162000108;
                break;
            case 217748: // epic drakie
                dropItems.Clear();
                itemId = 186000096;
                break;
            case 217750:
                dropItems.Clear();
                itemId = Rnd.Chance() < 27 ? Rnd.Get(1, 9) switch
                {
                    1 => 101700911,
                    2 => 100201003,
                    3 => 100900869,
                    4 => 100100874,
                    5 => 100500886,
                    6 => 101300836,
                    7 => 100600944,
                    8 => 100001135,
                    _ => 101500895,
                } : 162000109;
                break;
            case 217753:
            case 217759:
                dropItems.Add(DropRegistrationService.GetInstance().RegDropItem(2, 0, npcId, 186000124, 1)); // worthiness ticket
                if (Rnd.Chance() < 20)
                {
                    Race race = GetRegisteredTeamRace();
                    itemId = Rnd.Get(1, 4) switch
                    {
                        1 => 169500935,
                        2 => 169500933,
                        3 => race == Race.ELYOS ? 169500947 : 169500951,
                        _ => race == Race.ELYOS ? 169500927 : 169500931,
                    };
                }
                break;
            case 217754:
            {
                dropItems.Clear();
                int roll = Rnd.Get(1, 100);
                itemId = roll <= 20 ? 190000067 : roll <= 40 ? 160003573 : 164000160;
                break;
            }
        }
        if (itemId != 0)
            dropItems.Add(DropRegistrationService.GetInstance().RegDropItem(1, 0, npcId, itemId, 1));
    }

    private class ScoreWriter : InstanceScoreWriter<InstanceScore<CruciblePlayerReward>>
    {
        private readonly HashSet<int> present;

        internal ScoreWriter(InstanceScore<CruciblePlayerReward> score, HashSet<int> present)
            : base(score)
        {
            this.present = present;
        }

        public override void WriteMe(ByteBuffer buf)
        {
            int playerCount = 0;
            foreach (CruciblePlayerReward reward in instanceScore.GetPlayerRewards())
            {
                if (playerCount == 6 || !present.Contains(reward.GetOwnerId()))
                    continue;
                WriteD(buf, reward.GetOwnerId());
                WriteD(buf, reward.GetPoints());
                WriteD(buf, reward.IsRewarded() ? 3 : reward.IsPlayerDefeated() ? 2 : 1);
                WriteD(buf, reward.GetInsignia());
                playerCount++;
            }
            if (playerCount < 6)
                WriteB(buf, new byte[16 * (6 - playerCount)]);
        }
    }
}
