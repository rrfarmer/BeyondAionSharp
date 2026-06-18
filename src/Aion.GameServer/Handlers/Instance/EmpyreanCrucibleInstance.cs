using System.Collections.Generic;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;
using ItemService = Aion.GameServer.Services.Items.ItemService;
using TeleportService = Aion.GameServer.Services.Teleport.TeleportService;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/crucible/EmpyreanCrucibleInstance (xTz, Luzien) : CrucibleInstance. 1:1.
/// </summary>
[InstanceID(300300000)]
public class EmpyreanCrucibleInstance : CrucibleInstance
{
    private readonly List<Npc> npcs = new List<Npc>();
    private readonly List<EmpyreanStage> empyreanStage = new List<EmpyreanStage>();
    private byte stage;
    private bool isDoneStage4, isDoneStage6Round2, isDoneStage6Round1;

    public EmpyreanCrucibleInstance(WorldMapInstance instance) : base(instance)
    {
    }

    private class EmpyreanStage
    {
        internal readonly List<Npc> stageNpcs;

        internal EmpyreanStage(List<Npc> npcs)
        {
            this.stageNpcs = npcs;
        }

        internal bool ContainsNpcs(WorldMapInstance instance)
        {
            foreach (Npc npc in instance.GetNpcs())
            {
                foreach (Npc stageNpc in stageNpcs)
                {
                    if (npc.Equals(stageNpc))
                        return true;
                }
            }
            return false;
        }
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
        base.OnEnterInstance(player); // creates player reward
        if (stage > 0)
        {
            CruciblePlayerReward playerReward = GetPlayerReward(player.GetObjectId());
            if (isNew)
            {
                MoveToReadyRoom(player); // send player to team and wait for end of the stage
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ENTERED_BIRTHAREA_IDARENA());
            }
            else if (playerReward.IsPlayerLeave())
            {
                LeaveInstance(player);
                return;
            }
            else if (playerReward.IsRewarded())
            {
                DoReward(player);
                return;
            }
        }
        PacketSendUtility.SendPacket(player, new SM_INSTANCE_SCORE(instance.GetMapId(), new CrucibleScoreWriter(instanceScore)));
        PacketSendUtility.SendPacket(player, new SM_INSTANCE_STAGE_INFO(2, stageType.GetId(), stageType.GetType_()));
    }

    private void AddPointsAndSendPacket(int points, Npc npc)
    {
        instance.ForEachPlayer(player =>
        {
            if (player.IsOnline())
            {
                CruciblePlayerReward playerReward = GetPlayerReward(player.GetObjectId());
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(npc.GetObjectTemplate().GetL10n(), points));
                if (!playerReward.IsRewarded())
                {
                    playerReward.AddPoints(points);
                }
                PacketSendUtility.SendPacket(player, new SM_INSTANCE_SCORE(instance.GetMapId(), new CrucibleScoreWriter(instanceScore)));
            }
        });
    }

    private void SetStage(StageType type, int time)
    {
        this.stageType = type;
        ThreadPoolManager.GetInstance().Schedule(
            () => instance.ForEachPlayer(player => PacketSendUtility.SendPacket(player, new SM_INSTANCE_STAGE_INFO(2, type.GetId(), type.GetType_()))),
            (long)time);
    }

    public override void OnDie(Npc npc)
    {
        lock (npcs)
        {
            npcs.Remove(npc);
        }
        EmpyreanStage es = GetEmpyreanStage(npc);
        int point = 0;
        switch (npc.GetNpcId())
        {
            case 217511:
            case 217512:
            case 217513:
            case 217514:
                point += 55;
                break;
            case 217518:
                point += 110;
                break;
            case 217515:
            case 217516:
            case 217517:
                point += 120;
                break;
            case 217522:
                point += 250;
                break;
            case 217560:
            case 217561:
                point += 260;
                break;
            case 217557:
            case 217559:
            case 217558:
                point += 280;
                break;
            case 217519:
            case 217520:
            case 217521:
            case 217504:
            case 217507:
            case 217508:
            case 217547:
            case 217548:
            case 217549:
            case 217550:
            case 217545:
            case 217569:
                point += 300;
                break;
            case 217565:
                point += 350;
                break;
            case 217562:
                point += 360;
                break;
            case 217502:
            case 217505:
            case 217563:
            case 217566:
                point += 400;
                break;
            case 217654:
                point += 455;
                break;
            case 217523:
            case 217524:
            case 217525:
            case 217526:
                point += 475;
                break;
            case 217568:
                point += 480;
                break;
            case 217503:
            case 217489:
            case 217653:
                point += 500;
                break;
            case 217486:
            case 217564:
                point += 600;
                break;
            case 217652:
                point += 685;
                break;
            case 217570:
                point += 700;
                break;
            case 217492:
                point += 1100;
                break;
            case 217487:
                point += 1200;
                break;
            case 217491:
                point += 1300;
                break;
            case 217509:
                point += 1500;
                break;
            case 217552:
                point += 1900;
                break;
            case 217553:
                point += 2000;
                break;
            case 217567:
                point += 2100;
                break;
            case 217554:
                point += 2400;
                break;
            case 217578:
            case 217582:
                point += 2900;
                break;
            case 217579:
            case 217583:
                point += 3100;
                break;
            case 217527:
            case 217528:
                point += 3250;
                break;
            case 217572:
                point += 3370;
                break;
            case 217585:
                point += 3400;
                break;
            case 217580:
            case 217584:
                point += 3800;
                break;
            case 217588:
                point += 4000;
                break;
            case 217590:
                point += 4200;
                break;
            case 217597: // TODO: confirm
            case 217596:
                point += 4250;
                break;
            case 217493:
                point += 5000;
                break;
            case 217510:
            case 217501:
                point += 5500;
                break;
            case 217591:
                point += 6000;
                break;
            case 217594:
                point += 6200;
                break;
            case 217595:
                point += 7300;
                break;
            case 217556:
                point += 7800;
                break;
            case 217573:
                point += 8900;
                break;
            case 217587:
                point += 11500;
                break;
            case 217593:
                point += 17000;
                break;
        }

        if (point != 0)
        {
            AddPointsAndSendPacket(point, npc);
        }
        switch (npc.GetNpcId())
        {
            case 217486:
                npc.GetController().Delete();
                if (stageType == StageType.START_STAGE_1_ROUND_1 && GetNpc(217489) == null)
                {
                    StartStage1Round2();
                }
                break;
            case 217489:
                npc.GetController().Delete();
                if (stageType == StageType.START_STAGE_1_ROUND_1 && GetNpc(217486) == null)
                {
                    StartStage1Round2();
                }
                break;
            case 217492:
                npc.GetController().Delete();
                if (stageType == StageType.START_STAGE_1_ROUND_2)
                {
                    StartStage1Round3();
                }
                break;
            case 217487:
                npc.GetController().Delete();
                if (stageType == StageType.START_STAGE_1_ROUND_3)
                {
                    StartStage1Round4();
                }
                break;
            case 217491:
                npc.GetController().Delete();
                if (stageType == StageType.START_STAGE_1_ROUND_4)
                {
                    StartStage1Round5();
                }
                break;
            case 217493:
                SetStage(StageType.PASS_GROUP_STAGE_1, 0);
                Sp(217756, 342.65106f, 357.4013f, 96.09094f, (byte)0);
                Sp(217756, 349.07376f, 357.5898f, 96.090965f, (byte)0);
                Sp(217756, 345.69272f, 359.40958f, 96.09094f, (byte)0);
                Sp(217756, 342.59192f, 353.73386f, 96.090965f, (byte)0);
                Sp(217756, 342.6043f, 360.8932f, 96.09093f, (byte)0);
                Sp(217756, 345.69318f, 355.56677f, 96.09094f, (byte)0);
                Sp(799568, 345.25f, 349.24f, 96.09097f, (byte)0);
                npc.GetController().Delete();
                break;
            case 217502:
                npc.GetController().Delete();
                switch (stageType)
                {
                    case StageType.START_STAGE_2_ROUND_1:
                        if (GetNpc(217503) == null && GetNpc(217504) == null)
                        {
                            StartStage2Round2();
                        }
                        break;
                    case StageType.START_STAGE_2_ROUND_2:
                        if (GetNpc(217508) == null && GetNpc(217507) == null && GetNpc(217504) == null)
                        {
                            StartStage2Round3();
                        }
                        break;
                }
                break;
            case 217503:
                npc.GetController().Delete();
                if (stageType == StageType.START_STAGE_2_ROUND_1 && GetNpc(217502) == null && GetNpc(217504) == null)
                {
                    StartStage2Round2();
                }
                break;
            case 217504:
                npc.GetController().Delete();
                switch (stageType)
                {
                    case StageType.START_STAGE_2_ROUND_1:
                        if (GetNpc(217502) == null && GetNpc(217503) == null)
                        {
                            StartStage2Round2();
                        }
                        break;
                    case StageType.START_STAGE_2_ROUND_2:
                        if (GetNpc(217502) == null && GetNpc(217507) == null && GetNpc(217508) == null)
                        {
                            StartStage2Round3();
                        }
                        break;
                }
                break;
            case 217507:
                npc.GetController().Delete();
                switch (stageType)
                {
                    case StageType.START_STAGE_2_ROUND_2:
                        if (GetNpc(217502) == null && GetNpc(217504) == null && GetNpc(217508) == null)
                        {
                            StartStage2Round3();
                        }
                        break;
                }
                if (es != null && !es.ContainsNpcs(instance))
                {
                    StartStage2Round5();
                }
                break;
            case 217508:
                npc.GetController().Delete();
                if (es != null)
                {
                    return;
                }
                switch (stageType)
                {
                    case StageType.START_STAGE_2_ROUND_2:
                        if (GetNpc(217502) == null && GetNpc(217504) == null && GetNpc(217507) == null)
                        {
                            StartStage2Round3();
                        }
                        break;
                    case StageType.START_STAGE_2_ROUND_4:
                        if (GetNpc(217505) == null)
                        {
                            StartStage4Round4_1();
                        }
                        break;
                }
                break;
            case 217509:
                npc.GetController().Delete();
                SetStage(StageType.START_STAGE_2_ROUND_4, 2000);
                Sp(217505, 341.95056f, 334.77692f, 96.09093f, (byte)0, 6000);
                Sp(217508, 344.17813f, 334.42462f, 96.090935f, (byte)0, 6000);
                break;
            case 217505:
                npc.GetController().Delete();
                if (GetNpc(217508) == null)
                {
                    StartStage4Round4_1();
                }
                break;
            case 217506:
                npc.GetController().Delete();
                if (es != null && !es.ContainsNpcs(instance))
                {
                    StartStage2Round5();
                }
                break;
            case 217510:
            case 217501:
                npc.GetController().Delete();
                SetStage(StageType.PASS_GROUP_STAGE_2, 0);
                ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    Sp(217737, 334.49496f, 349.2322f, 96.090935f, (byte)0);
                    SetStage(StageType.START_BONUS_STAGE_2, 0);

                    ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        if (GetNpc(217737) != null)
                        {
                            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S2_Saam1_01());
                            ThreadPoolManager.GetInstance().Schedule(() =>
                            {
                                if (GetNpc(217737) != null)
                                {
                                    DeleteAliveNpcs(217737);
                                    Sp(799569, 345.25f, 349.24f, 96.09097f, (byte)0);
                                }
                            }, 30000L);
                        }
                    }, 30000L);
                }, 8000L);
                break;
            case 217737:
                npc.GetController().Delete();
                Sp(799569, 345.25f, 349.24f, 96.09097f, (byte)0);
                break;
            case 217511:
            case 217512:
            case 217513:
            case 217514:
                npc.GetController().Delete();
                if (GetNpcs(217511).Count == 0 && GetNpcs(217512).Count == 0 && GetNpcs(217513).Count == 0 && GetNpcs(217514).Count == 0)
                {
                    SetStage(StageType.START_STAGE_3_ROUND_2, 2000);
                    Sp(217515, 336.32092f, 345.0251f, 96.090935f, (byte)0, 6000);
                    Sp(217516, 347.16144f, 361.89084f, 96.09093f, (byte)0, 6000);
                    Sp(217518, 352.77557f, 360.97845f, 96.09091f, (byte)0, 6000);
                    Sp(217518, 340.2231f, 351.10208f, 96.09098f, (byte)0, 6000);
                    Sp(217517, 354.132f, 337.14255f, 96.09089f, (byte)0, 6000);
                    Sp(217517, 353.7888f, 354.4324f, 96.091064f, (byte)0, 6000);
                    Sp(217516, 350.0108f, 342.09482f, 96.090935f, (byte)0, 6000);
                    Sp(217515, 349.16327f, 335.63864f, 96.09095f, (byte)0, 6000);
                    Sp(217517, 341.23633f, 344.55603f, 96.09096f, (byte)0, 6000);
                    Sp(217518, 354.66513f, 343.31537f, 96.091095f, (byte)0, 6000);
                    Sp(217516, 334.60898f, 352.01447f, 96.09095f, (byte)0, 6000);
                    Sp(217515, 348.87338f, 354.90146f, 96.09096f, (byte)0, 6000);
                }
                break;
            case 217515:
            case 217516:
            case 217517:
            case 217518:
                npc.GetController().Delete();
                if (GetNpcs(217515).Count == 0 && GetNpcs(217516).Count == 0 && GetNpcs(217517).Count == 0 && GetNpcs(217518).Count == 0)
                {
                    SetStage(StageType.START_STAGE_3_ROUND_3, 2000);
                    Sp(217519, 351.08026f, 341.61298f, 96.090935f, (byte)0, 6000);
                    Sp(217521, 333.4532f, 354.7357f, 96.09094f, (byte)0, 6000);
                    Sp(217522, 342.1805f, 360.534f, 96.09092f, (byte)0, 6000);
                    Sp(217520, 334.2686f, 342.60797f, 96.09091f, (byte)0, 6000);
                    Sp(217522, 350.34537f, 356.18558f, 96.09094f, (byte)0, 6000);
                    Sp(217520, 343.7485f, 336.2869f, 96.09092f, (byte)0, 6000);
                }
                break;
            case 217519:
            case 217520:
            case 217521:
            case 217522:
                npc.GetController().Delete();
                if (GetNpcs(217519).Count == 0 && GetNpcs(217520).Count == 0 && GetNpcs(217521).Count == 0 && GetNpcs(217522).Count == 0)
                {
                    SetStage(StageType.START_STAGE_3_ROUND_4, 2000);
                    Sp(217524, 349.66446f, 341.4752f, 96.090965f, (byte)0, 6000);
                    Sp(217525, 338.32742f, 356.29636f, 96.090935f, (byte)0, 6000);
                    Sp(217526, 349.31473f, 358.43762f, 96.09096f, (byte)0, 6000);
                    Sp(217523, 338.73138f, 342.35876f, 96.09094f, (byte)0, 6000);
                }
                break;
            case 217523:
            case 217524:
            case 217525:
            case 217526:
                npc.GetController().Delete();
                if (GetNpcs(217523).Count == 0 && GetNpcs(217524).Count == 0 && GetNpcs(217525).Count == 0 && GetNpcs(217526).Count == 0)
                {
                    SetStage(StageType.START_STAGE_3_ROUND_5, 2000);
                    Sp(217527, 335.37524f, 346.34567f, 96.09094f, (byte)0, 6000);
                    Sp(217528, 335.36105f, 353.16922f, 96.09094f, (byte)0, 6000);
                }
                break;
            case 217527:
            case 217528:
                npc.GetController().Delete();
                if (GetNpcs(217527).Count == 0 && GetNpcs(217528).Count == 0)
                {
                    SetStage(StageType.START_BONUS_STAGE_3, 7000);
                    Sp(217744, 342.45215f, 349.339f, 96.09096f, (byte)0, 7000);
                    ThreadPoolManager.GetInstance().Schedule(StartBonusStage3, 39000L);
                }
                break;
            case 217557:
            case 217559:
            case 217562:
                npc.GetController().Delete();
                switch (stageType)
                {
                    case StageType.START_STAGE_4_ROUND_1:
                        if (GetNpcs(217557).Count == 0 && GetNpcs(217559).Count == 0)
                        {
                            Sp(217558, 330.27792f, 339.2779f, 96.09093f, (byte)6);
                            Sp(217558, 328.08972f, 346.3553f, 96.090904f, (byte)1);
                        }
                        break;
                    case StageType.START_STAGE_4_ROUND_2:
                        if (es != null && !es.ContainsNpcs(instance))
                        {
                            StartStage4Round3();
                        }
                        break;
                }
                break;
            case 217558:
            case 217561:
                npc.GetController().Delete();
                switch (stageType)
                {
                    case StageType.START_STAGE_4_ROUND_1:
                        if (GetNpcs(217558).Count == 0)
                        {
                            SetStage(StageType.START_STAGE_4_ROUND_2, 2000);
                            Sp(217559, 330.53665f, 349.23523f, 96.09093f, (byte)0, 6000);
                            Sp(217562, 334.89508f, 363.78442f, 96.090904f, (byte)105, 6000);
                            Sp(217560, 334.61942f, 334.80353f, 96.090904f, (byte)15, 6000);
                            ThreadPoolManager.GetInstance().Schedule(() =>
                            {
                                List<Npc> round = new List<Npc>();
                                round.Add(Sp(217557, 357.24625f, 338.30093f, 96.09104f, (byte)65));
                                round.Add(Sp(217558, 357.20663f, 359.28714f, 96.091064f, (byte)75));
                                round.Add(Sp(217561, 365.109f, 349.1218f, 96.09114f, (byte)60));
                                empyreanStage.Add(new EmpyreanStage(round));
                            }, 47000L);
                        }
                        break;
                    case StageType.START_STAGE_4_ROUND_2:
                        if (es != null && !es.ContainsNpcs(instance))
                        {
                            StartStage4Round3();
                        }
                        break;
                }
                break;
            case 217563:
            case 217566:
                npc.GetController().Delete();
                if (es != null && !es.ContainsNpcs(instance))
                {
                    SetStage(StageType.START_STAGE_4_ROUND_4, 2000);
                    Sp(217567, 345.73895f, 349.49786f, 96.09097f, (byte)0, 6000);
                }
                break;
            case 217564:
            case 217565:
            case 217560:
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
                npc.GetController().Delete();
                SetStage(StageType.START_STAGE_4_ROUND_5, 2000);
                Sp(217653, 327.76917f, 349.26215f, 96.09092f, (byte)0, 6000);
                Sp(217651, 364.8972f, 349.25653f, 96.09114f, (byte)60, 18000);
                Sp(217652, 361.1795f, 339.99252f, 96.09112f, (byte)50, 35000);
                Sp(217653, 354.4119f, 333.6749f, 96.09091f, (byte)40, 54000);
                Sp(217651, 331.61502f, 358.4374f, 96.09091f, (byte)110, 69000);
                Sp(217652, 338.38858f, 364.91507f, 96.090904f, (byte)100, 83000);
                Sp(217651, 346.39847f, 368.19427f, 96.090904f, (byte)90, 99000);
                Sp(217652, 353.92606f, 364.92636f, 96.090904f, (byte)80, 110000);
                Sp(217653, 361.13452f, 358.90424f, 96.091156f, (byte)65, 130000);
                Sp(217652, 346.34402f, 329.9449f, 96.09091f, (byte)30, 142000);
                ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    Sp(217653, 331.53894f, 339.8832f, 96.09091f, (byte)10);
                    isDoneStage4 = true;
                }, 174000L);
                break;
            case 217651:
            case 217652:
            case 217653:
                npc.GetController().Delete();
                if (isDoneStage4 && GetNpcs(217651).Count == 0 && GetNpcs(217652).Count == 0 && GetNpcs(217653).Count == 0)
                {
                    SetStage(StageType.PASS_GROUP_STAGE_4, 0);
                    Sp(217749, 340.59f, 349.32166f, 96.09096f, (byte)0, 6000);
                    SetStage(StageType.START_BONUS_STAGE_4, 6000);
                    ThreadPoolManager.GetInstance().Schedule(StartBonusStage4, 33000L);
                }
                break;
            case 217547:
            case 217548:
            case 217549:
                npc.GetController().Delete();
                if (GetNpcs(217547).Count == 0 && GetNpcs(217548).Count == 0 && GetNpcs(217549).Count == 0)
                {
                    Sp(217550, 1266.293f, 778.3254f, 358.60574f, (byte)30, 4000);
                    Sp(217545, 1254.261f, 778.3817f, 358.6056f, (byte)30, 4000);
                }
                break;
            case 217550:
            case 217545:
                npc.GetController().Delete();
                if (GetNpcs(217550).Count == 0 && GetNpcs(217545).Count == 0)
                {
                    SetStage(StageType.START_STAGE_5_ROUND_2, 2000);
                    Sp(217552, 1246.0197f, 788.8341f, 358.60556f, (byte)11, 6000);
                }
                break;
            case 217552:
                npc.GetController().Delete();
                SetStage(StageType.START_STAGE_5_ROUND_3, 2000);
                Sp(700527, 1253.3123f, 789.38385f, 358.60562f, (byte)119);
                Sp(700528, 1260.2015f, 800.14886f, 358.6056f, (byte)16);
                Sp(217553, 1259.4706f, 812.30505f, 358.6056f, (byte)30, 6000);
                break;
            case 217553:
                npc.GetController().Delete();
                DeleteAliveNpcs(700527, 700528);
                Sp(281111, 1246.4855f, 796.90735f, 358.6056f, (byte)0);
                Sp(281110, 1259.5508f, 784.5548f, 358.60562f, (byte)0);
                Sp(281112, 1276.6561f, 812.5499f, 358.60565f, (byte)0);
                Sp(281322, 1243.2113f, 813.0927f, 358.60565f, (byte)0);
                Sp(281109, 1272.9266f, 797.1055f, 358.60562f, (byte)0);
                Sp(281113, 1275.894f, 780.51544f, 358.60565f, (byte)0);
                Sp(281108, 1260.003f, 810.555f, 358.6056f, (byte)0);
                Sp(281114, 1244.3293f, 780.4284f, 358.60562f, (byte)0);
                SetStage(StageType.START_STAGE_5_ROUND_4, 2000);
                Sp(217554, 1243.1877f, 796.79553f, 358.6056f, (byte)0, 6000);
                break;
            case 217554:
                npc.GetController().Delete();
                DeleteAliveNpcs(281108, 281109, 281110, 281111, 281112, 281113, 281114, 281322);
                SetStage(StageType.START_STAGE_5_ROUND_5, 2000);
                Sp(217556, 1259.8387f, 785.6266f, 358.60562f, (byte)30, 6000);
                Sp(281191, 1261.8f, 804.5f, 358.7f, (byte)0, 6000);
                Sp(281192, 1267.6f, 793.9f, 358.7f, (byte)0, 6000);
                Sp(281193, 1257.4f, 787.9f, 358.7f, (byte)0, 6000);
                Sp(281194, 1251.3f, 798.6f, 358.7f, (byte)0, 6000);
                break;
            case 217556:
                npc.GetController().Delete();
                DeleteAliveNpcs(281191, 281192, 281193, 281194);
                SetStage(StageType.PASS_GROUP_STAGE_5, 0);
                Sp(205339, 1260.1465f, 795.07495f, 358.60562f, (byte)30);
                break;
            case 217568:
                Sp(205413, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                npc.GetController().Delete();
                if (isDoneStage6Round1 && GetNpcs(217568).Count == 0)
                {
                    SetStage(StageType.START_STAGE_6_ROUND_2, 2000);
                    Sp(217570, 1629.4642f, 154.8044f, 126f, (byte)30, 6000);
                    Sp(217569, 1643.7776f, 161.63562f, 126f, (byte)46, 6000);
                    Sp(217569, 1639.7843f, 142.09268f, 126f, (byte)40, 6000);
                    ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        Sp(217569, 1614.6377f, 164.04999f, 126.00113f, (byte)3);
                        Sp(217569, 1625.8965f, 135.62509f, 126f, (byte)30);
                        isDoneStage6Round2 = true;
                    }, 12000L);
                }
                break;
            case 217570:
                for (int i = 0; i < 5; i++)
                    Sp(211984, npc.GetX() + Rnd.Get(-2, 2), npc.GetY() + Rnd.Get(-2, 2), npc.GetZ(), npc.GetHeading());
                break;
            case 217569:
                Sp(205414, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                npc.GetController().Delete();
                if (stageType == StageType.START_STAGE_6_ROUND_2 && isDoneStage6Round2 && GetNpcs(217569).Count == 0 && GetNpcs(217570).Count == 0)
                {
                    SetStage(StageType.START_STAGE_6_ROUND_3, 2000);
                    Sp(217572, 1629.5837f, 138.38435f, 126f, (byte)30, 9000);
                    Sp(217569, 1635.01535f, 150.01535f, 126f, (byte)45, 6000);
                    Sp(217569, 1638.3817f, 152.84074f, 126f, (byte)45, 6000);
                }
                break;
            case 217572:
                npc.GetController().Delete();
                Sp(217573, 1634.7891f, 141.99077f, 126f, (byte)0);
                SetStage(StageType.START_STAGE_6_ROUND_4, 2000);
                break;
            case 217573:
                npc.GetController().Delete();
                SetStage(StageType.PASS_GROUP_STAGE_6, 0);
                // sendMsg(1400977);
                // TODO: 2 boxes ready room!
                Sp(217750, 1624.1908f, 155.16148f, 126f, (byte)0, 8000);
                SetStage(StageType.START_BONUS_STAGE_6, 8000);
                break;
            case 217750:
                Sp(205340, 1625.08f, 159.15f, 126f, (byte)0);
                break;
            case 217582:
            case 217578:
                SetStage(StageType.START_STAGE_7_ROUND_2, 2000);
                Sp(217579, 1794.81f, 779.53925f, 469.35016f, (byte)40, 6000);
                // sp(217583, 1794.81f, 779.53925f, 469.35016f, (byte) 40, 6000);
                break;
            case 217579:
            case 217583:
                SetStage(StageType.START_STAGE_7_ROUND_3, 2000);
                Sp(217580, 1775.6254f, 811.43225f, 469.35022f, (byte)100, 6000);
                // sp(217584, 1775.6254f, 811.43225f, 469.35022f, (byte) 100, 6000);
                break;
            case 217580:
            case 217584:
                SetStage(StageType.START_STAGE_7_ROUND_4, 2000);
                Sp(217581, 1775.716f, 779.630f, 469.564f, (byte)20, 6000);
                // sp(217585, 1775.716f, 779.630f, 469.564f, (byte) 20, 6000);
                break;
            case 217581:
            case 217585:
                SetStage(StageType.START_STAGE_7_ROUND_5, 2000);
                Sp(217586, 1773.194f, 796.537f, 469.350f, (byte)0, 6000);
                // sp(217587, 1773.194f, 796.537f, 469.350f, (byte) 0, 6000);
                break;
            case 217586:
            case 217587:
                SetStage(StageType.PASS_GROUP_STAGE_7, 0);
                Sp(217753, 1782.881f, 800.114f, 469.420f, (byte)0, 2000);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S7_BookBox_01());
                Sp(205341, 1781.610f, 796.920f, 469.350f, (byte)0, 2000);
                break;
            case 217588:
                SetStage(StageType.START_STAGE_8_ROUND_2, 2000);
                Sp(217590, 1764.377f, 1761.510f, 303.695f, (byte)0, 6000);
                break;
            case 217590:
                SetStage(StageType.START_STAGE_8_ROUND_3, 2000);
                Sp(217591, 1776.946f, 1749.255f, 303.696f, (byte)30, 6000);
                break;
            case 217591:
                SetStage(StageType.START_STAGE_8_ROUND_4, 2000);
                Sp(217592, 1790.693f, 1761.911f, 303.877f, (byte)60, 6000);
                break;
            case 217592:
                SetStage(StageType.START_STAGE_8_ROUND_5, 2000);
                Sp(280790, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                Sp(217593, 1777.065f, 1763.706f, 303.695f, (byte)90, 6000);
                break;
            case 217593:
                SetStage(StageType.PASS_GROUP_STAGE_8, 0);
                Sp(205342, 1776.757f, 1764.624f, 303.695f, (byte)90);
                break;
            case 217594:
                SetStage(StageType.START_STAGE_9_ROUND_2, 2000);
                Sp(217595, 1322.311f, 1741.508f, 316.349f, (byte)65, 6000);
                break;
            case 217595:
                SetStage(StageType.START_STAGE_9_ROUND_3, 2000);
                Sp(217596, 1308.038f, 1729.718f, 315.996f, (byte)36, 6000);
                Sp(217597, 1302.290f, 1745.471f, 316.092f, (byte)96, 6000);
                break;
            case 217596:
            case 217597:
                Npc counterpart = GetNpc(npc.GetNpcId() == 217596 ? 217597 : 217596);
                if (counterpart != null && !counterpart.IsDead())
                {
                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(counterpart, 19624, 10, counterpart).UseNoAnimationSkill();
                }
                npc.GetController().Delete();
                if (GetNpcs(217596).Count == 0 && GetNpcs(217597).Count == 0)
                {
                    // sendEventPacket(StageType.START_STAGE_9_ROUND_4, 2000);
                    RewardGroup(); // Finish for now, TODO: continue
                }
                break;
        }
    }

    private void StartBonusStage3()
    {
        Sp(217742, 360.76f, 349.42f, 96.1f, (byte)0);
        Sp(217743, 346.27f, 363.35f, 96.1f, (byte)11);
        Sp(217740, 332.12f, 349.22f, 96.1f, (byte)0);
        Sp(217741, 346.42f, 335.1f, 96.1f, (byte)87);

        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S3_Bonus_01()); // 30 sec
            ThreadPoolManager.GetInstance().Schedule(() =>
            {
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S3_Bonus_02()); // 10 sec
                ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_S3_Bonus_03()); // 5 sec
                    ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        DeleteAliveNpcs(217740, 217741, 217742, 217743, 217744);
                        Sp(205331, 345.25f, 349.24f, 96.09097f, (byte)0);
                        Sp(217735, 378.9331f, 346.74878f, 96.74762f, (byte)0);
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_S3_ResurBox1_01()); // A Ticket Box appeared
                    }, 5000L);
                }, 5000L);
            }, 20000L);
        }, 30000L);
    }

    private void StartBonusStage4()
    {
        Sp(217778, 346.2f, 366.85f, 96.55f, (byte)1);
        Sp(217747, 346.2204f, 367.52002f, 96.090904f, (byte)60, 3000);
        Sp(217747, 346.2204f, 367.52002f, 96.090904f, (byte)60, 6000);
        Sp(217748, 346.65222f, 366.3634f, 96.09092f, (byte)60, 9000);
        Sp(217748, 346.65222f, 366.3634f, 96.09092f, (byte)60, 12000);
        Sp(217748, 345.7578f, 366.7986f, 96.09094f, (byte)60, 15000);
        Sp(217748, 345.863f, 367.471f, 96.09094f, (byte)60, 18000);
        Sp(217747, 346.9996f, 366.3978f, 96.09092f, (byte)60, 21000);
        Sp(217746, 345.7872f, 366.3056f, 96.09092f, (byte)60, 24000);
        Sp(217745, 346.4504f, 367.8004f, 96.09094f, (byte)60, 27000);
        Sp(217747, 346.75043f, 367.467f, 96.09094f, (byte)60, 30000);
        Sp(217747, 346.535f, 367.3128f, 96.090904f, (byte)60, 33000);
        Sp(217747, 345.8452f, 367.2468f, 96.09092f, (byte)60, 36000);
        Sp(217746, 345.428f, 366.2954f, 96.09093f, (byte)60, 39000);
        Sp(217747, 346.71082f, 366.7156f, 96.090904f, (byte)60, 42000);
        Sp(217747, 346.38782f, 366.1606f, 96.09093f, (byte)60, 45000);
        Sp(217747, 345.36002f, 366.0284f, 96.09093f, (byte)60, 48000);
        Sp(217747, 345.5378f, 366.1876f, 96.09092f, (byte)60, 51000);
        Sp(217747, 346.5176f, 365.8592f, 96.09092f, (byte)60, 54000);
        Sp(217745, 345.8434f, 367.8082f, 96.090904f, (byte)60, 57000);
        Sp(217747, 345.297f, 366.3014f, 96.09093f, (byte)60, 60000);
        // 30 sec
        Sp(217747, 346.0346f, 367.2426f, 96.090904f, (byte)60, 63000);
        Sp(217747, 345.52863f, 366.62622f, 96.09092f, (byte)60, 66000);
        Sp(217745, 345.80862f, 366.9388f, 96.09092f, (byte)60, 69000);
        Sp(217747, 346.393f, 366.9766f, 96.090904f, (byte)60, 72000);
        Sp(217746, 345.5726f, 366.3462f, 96.09092f, (byte)60, 75000);
        Sp(217745, 345.2004f, 366.36902f, 96.09092f, (byte)60, 78000);
        Sp(217746, 346.2528f, 365.9208f, 96.09093f, (byte)60, 81000);
        Sp(217747, 346.0686f, 366.9096f, 96.090904f, (byte)60, 84000);
        // 10 sec
        Sp(217746, 345.4606f, 367.14862f, 96.090904f, (byte)60, 87000);
        Sp(217747, 345.8016f, 367.7212f, 96.090904f, (byte)60, 90000);
        Sp(217747, 347.1144f, 365.875f, 96.09092f, (byte)60, 93000);
        // 5 sec
        Sp(217747, 345.3226f, 367.7414f, 96.0909f, (byte)60, 96000);
        Sp(217747, 345.4836f, 367.3886f, 96.090904f, (byte)60, 99000);
        Sp(217747, 345.80862f, 366.0682f, 96.09092f, (byte)60, 102000);
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            DeleteAliveNpcs(217745, 217746, 217747, 217748, 217749, 217778);
            Sp(205338, 345.25f, 349.24f, 96.09097f, (byte)0);
        }, 102000L);
    }

    private void StartStage4Round4_1()
    {
        List<Npc> round = new List<Npc>();
        round.Add(Sp(217508, 334.06754f, 339.84393f, 96.09091f, (byte)0));
        empyreanStage.Add(new EmpyreanStage(round));
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            List<Npc> round1 = new List<Npc>();
            round1.Add(Sp(217506, 342.12405f, 364.4922f, 96.09093f, (byte)0));
            round1.Add(Sp(217507, 344.4953f, 365.14444f, 96.09092f, (byte)0));
            empyreanStage.Add(new EmpyreanStage(round1));
        }, 5000L);
    }

    private void StartStage4Round3()
    {
        SetStage(StageType.START_STAGE_4_ROUND_3, 2000);
        Sp(217563, 339.70975f, 333.54272f, 96.090904f, (byte)20, 6000);
        Sp(217564, 342.92892f, 333.43994f, 96.09092f, (byte)18, 6000);
        Sp(217565, 341.55396f, 330.70847f, 96.09093f, (byte)23, 16000);
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            List<Npc> round = new List<Npc>();
            round.Add(Sp(217566, 362.87164f, 357.87164f, 96.091125f, (byte)73));
            round.Add(Sp(217563, 359.1135f, 359.6953f, 96.091125f, (byte)80));
            empyreanStage.Add(new EmpyreanStage(round));
        }, 43000L);
    }

    private void StartStage2Round2()
    {
        SetStage(StageType.START_STAGE_2_ROUND_2, 2000);
        Sp(217502, 328.78433f, 348.77353f, 96.09092f, (byte)0, 6000);
        Sp(217508, 329.01874f, 343.79257f, 96.09092f, (byte)0, 6000);
        Sp(217507, 329.2849f, 355.2314f, 96.090935f, (byte)0, 6000);
        Sp(217504, 328.90808f, 351.6184f, 96.09092f, (byte)0, 6000);
    }

    private void StartStage2Round3()
    {
        SetStage(StageType.START_STAGE_2_ROUND_3, 2000);
        Sp(217509, 332.24298f, 349.49286f, 96.090935f, (byte)0, 6000);
    }

    private void StartStage2Round5()
    {
        SetStage(StageType.START_STAGE_2_ROUND_5, 2000);
        Sp(Rnd.Get(1, 2) == 1 ? 217510 : 217501, 332.0035f, 349.55893f, 96.09093f, (byte)0, 6000);
    }

    private void RewardGroup()
    {
        foreach (Player p in instance.GetPlayersInside())
        {
            DoReward(p);
        }
    }

    public override void DoReward(Player player)
    {
        CruciblePlayerReward playerReward = GetPlayerReward(player.GetObjectId());
        if (playerReward == null)
        {
            return;
        }
        float reward = 0.01f * playerReward.GetPoints() + 350;
        if (!playerReward.IsRewarded())
        {
            playerReward.SetRewarded();
            playerReward.SetInsignia((int)reward);
            ItemService.AddItem(player, 186000130, (int)reward);
        }
        else
        {
            TeleportService.TeleportTo(player, mapId, player.GetInstanceId(), 381.41684f, 346.78162f, 96.74763f, (byte)43);
        }
        instanceScore.SetInstanceProgressionType(InstanceProgressionType.END_PROGRESS);
        PacketSendUtility.SendPacket(player, new SM_INSTANCE_SCORE(instance.GetMapId(), new CrucibleScoreWriter(instanceScore)));
    }

    public override void OnInstanceDestroy()
    {
        base.OnInstanceDestroy();
        npcs.Clear();
        empyreanStage.Clear();
    }

    public override bool OnDie(Player player, Creature lastAttacker)
    {
        base.OnDie(player, lastAttacker);
        GetPlayerReward(player.GetObjectId()).SetPlayerDefeated(true);

        bool defeat = true;
        foreach (Player p in instance.GetPlayersInside())
        {
            if (!GetPlayerReward(p.GetObjectId()).IsPlayerDefeated())
            {
                defeat = false;
                break;
            }
        }

        if (defeat)
            RewardGroup();

        return true;
    }

    public override bool OnReviveEvent(Player player)
    {
        base.OnReviveEvent(player);
        MoveToReadyRoom(player);
        instance.ForEachPlayer(p =>
        {
            if (player.Equals(p))
            {
                PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_MOVE_BIRTHAREA_ME_IDARENA());
            }
            else
                PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_MOVE_BIRTHAREA_FRIENDLY_IDARENA(player.GetName()));
        });
        return true;
    }

    private EmpyreanStage GetEmpyreanStage(Npc npc)
    {
        foreach (EmpyreanStage es in empyreanStage)
        {
            if (es.stageNpcs.Contains(npc))
            {
                return es;
            }
        }
        return null;
    }

    private bool IsSpawn(List<int> round)
    {
        foreach (Npc n in npcs)
        {
            if (round.Contains(n.GetNpcId()))
            {
                return false;
            }
        }
        return true;
    }

    public override void OnChangeStage(StageType type)
    {
        switch (type)
        {
            case StageType.START_STAGE_1_ELEVATOR:
                SetStage(type, 0);
                SetStage(StageType.START_STAGE_1_ROUND_1, 5000);
                stage = 1;
                List<int> round = new List<int>();
                round.Add(217486);
                round.Add(217489);
                Sp(217486, 327.73657f, 347.96228f, 96.09092f, (byte)0, 9000);
                Sp(217489, 327.81943f, 350.948f, 96.09093f, (byte)0, 9000);
                ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    if (!IsSpawn(round))
                    {
                        StartStage1Round2();
                    }
                    round.Clear();
                }, 62000L);
                break;
            case StageType.START_STAGE_2_ELEVATOR:
                SetStage(type, 0);
                DeleteAliveNpcs(217756);
                stage = 2;
                SetStage(StageType.START_STAGE_2_ROUND_1, 5000);
                Sp(217503, 325.71194f, 352.81027f, 96.09092f, (byte)0, 9000);
                Sp(217502, 325.78696f, 346.07263f, 96.090904f, (byte)0, 9000);
                Sp(217504, 325.06122f, 349.4784f, 96.090904f, (byte)0, 9000);
                break;
            case StageType.START_STAGE_3_ELEVATOR:
                SetStage(type, 0);
                SetStage(StageType.START_STAGE_3_ROUND_1, 5000);
                stage = 3;
                Sp(217512, 344.23056f, 347.89594f, 96.09096f, (byte)0, 9000);
                Sp(217513, 341.09082f, 337.95187f, 96.09097f, (byte)0, 9000);
                Sp(217512, 342.06656f, 361.16135f, 96.090935f, (byte)0, 9000);
                Sp(217511, 356.75006f, 335.27487f, 96.09096f, (byte)0, 9000);
                Sp(217514, 345.4355f, 365.05215f, 96.09093f, (byte)0, 9000);
                Sp(217512, 352.8222f, 358.33463f, 96.09092f, (byte)0, 9000);
                Sp(217513, 342.32755f, 365.00473f, 96.09093f, (byte)0, 9000);
                Sp(217514, 356.19113f, 362.22543f, 96.090965f, (byte)0, 9000);
                Sp(217511, 344.25127f, 334.1194f, 96.090935f, (byte)0, 9000);
                Sp(217511, 344.07086f, 346.8839f, 96.09092f, (byte)0, 9000);
                Sp(217514, 334.01746f, 350.76382f, 96.090935f, (byte)0, 9000);
                Sp(217513, 344.49155f, 351.73932f, 96.09093f, (byte)0, 9000);
                Sp(217513, 353.0832f, 362.178f, 96.09092f, (byte)0, 9000);
                Sp(217511, 356.24454f, 358.34552f, 96.09103f, (byte)0, 9000);
                Sp(217512, 330.64853f, 346.87302f, 96.09091f, (byte)0, 9000);
                Sp(217512, 353.32773f, 335.26398f, 96.09092f, (byte)0, 9000);
                Sp(217514, 356.69666f, 339.1548f, 96.09103f, (byte)0, 9000);
                Sp(217511, 347.6529f, 347.90683f, 96.09098f, (byte)0, 9000);
                Sp(217514, 347.5995f, 351.78674f, 96.09099f, (byte)0, 9000);
                Sp(217512, 340.82983f, 334.1085f, 96.09093f, (byte)0, 9000);
                Sp(217514, 344.19876f, 337.9993f, 96.09094f, (byte)0, 9000);
                Sp(217513, 353.5887f, 339.10763f, 96.09092f, (byte)0, 9000);
                Sp(217511, 345.4889f, 361.17224f, 96.090935f, (byte)0, 9000);
                Sp(217513, 330.90952f, 350.7164f, 96.09093f, (byte)0, 9000);
                break;
            case StageType.START_STAGE_4_ELEVATOR:
                SetStage(type, 0);
                DeleteAliveNpcs(217735);
                SetStage(StageType.START_STAGE_4_ROUND_1, 5000);
                stage = 4;
                Sp(217557, 328.88104f, 349.55392f, 96.090904f, (byte)0, 9000);
                Sp(217559, 328.38922f, 342.39066f, 96.09091f, (byte)5, 9000);
                Sp(217557, 333.17947f, 336.4504f, 96.090904f, (byte)8, 9000);
                break;
            case StageType.START_STAGE_5_ROUND_1:
                SetStage(type, 2000);
                Sp(217549, 1263.1987f, 778.4129f, 358.6056f, (byte)30, 6000);
                Sp(217548, 1260.1381f, 778.84644f, 358.60562f, (byte)30, 6000);
                Sp(217547, 1257.3065f, 778.35016f, 358.60562f, (byte)30, 6000);
                break;
            case StageType.START_STAGE_6_ROUND_1:
                SetStage(type, 2000);
                Sp(217568, 1636.7102f, 166.87984f, 126f, (byte)60, 6000);
                Sp(217568, 1619.4432f, 153.83188f, 126f, (byte)60, 6000);
                Sp(217568, 1636.6416f, 164.15344f, 126f, (byte)60, 6000);
                ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    Sp(217568, 1638.7107f, 165.40533f, 126f, (byte)60);
                    Sp(217568, 1638.6783f, 162.67389f, 126f, (byte)60);
                    isDoneStage6Round1 = true;
                }, 12000L);
                break;
            case StageType.START_STAGE_6_ROUND_5:
                SetStage(type, 0);
                break;
            case StageType.START_STAGE_7_ROUND_1:
                SetStage(type, 2000);
                Sp(217578, 1784.848f, 806.7728f, 469.82202f, (byte)0, 6000); // elyos
                // sp(217582, 1794.908f, 811.9936f, 469.3501f, (byte) 80, 6000); //asmo, need sniff
                break;
            case StageType.START_STAGE_8_ROUND_1:
                SetStage(type, 2000);
                Sp(217588, 1776.578f, 1773.231f, 303.695f, (byte)90, 6000);
                break;
            case StageType.START_STAGE_9_ROUND_1:
                SetStage(type, 2000);
                Sp(217594, 1274.890f, 1730.676f, 318.194f, (byte)3, 6000);
                break;
            case StageType.START_STAGE_5:
                stage = 5;
                Sp(205426, 1256.2872f, 834.28986f, 358.60565f, (byte)103);
                Sp(205332, 1260.1292f, 795.06964f, 358.60562f, (byte)30);
                Teleport(1260.15f, 812.34f, 358.6056f, (byte)90);
                SetStage(type, 1000);
                break;
            case StageType.START_STAGE_6:
                stage = 6;
                Sp(205427, 1594.4756f, 145.26898f, 128.67778f, (byte)16);
                Sp(205333, 1625.1771f, 159.15244f, 126f, (byte)70);
                Teleport(1616.0248f, 154.43837f, 126f, (byte)10);
                SetStage(type, 1000);
                break;
            case StageType.START_STAGE_7:
                stage = 7;
                Sp(205428, 1820.39f, 800.81805f, 470.1394f, (byte)86);
                Sp(205334, 1781.6106f, 796.9224f, 469.35016f, (byte)0);
                Teleport(1793.9233f, 796.92f, 469.36542f, (byte)60);
                SetStage(type, 1000);
                break;
            case StageType.START_STAGE_8:
                stage = 8;
                Sp(205335, 1776.759f, 1764.705f, 303.695f, (byte)90);
                Sp(205429, 1780.103f, 1723.458f, 304.039f, (byte)53);
                Teleport(1776.4169f, 1749.9952f, 303.69553f, (byte)0); // get retail
                SetStage(type, 1000);
                break;
            case StageType.START_STAGE_9:
                stage = 9;
                Sp(205430, 1359.375f, 1758.057f, 319.625f, (byte)90);
                Sp(205336, 1309.309f, 1732.540f, 315.782f, (byte)7);
                Teleport(1320.4513f, 1738.4838f, 316.1746f, (byte)66);
                SetStage(type, 1000);
                break;
        }
    }

    private void StartStage1Round2()
    {
        SetStage(StageType.START_STAGE_1_ROUND_2, 2000);
        List<int> round = new List<int>();
        round.Add(217492);
        Sp(217492, 332.7714f, 358.48206f, 96.09092f, (byte)106, 6000);
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!IsSpawn(round))
            {
                StartStage1Round3();
            }
            round.Clear();
        }, 59000L);
    }

    private void StartStage1Round3()
    {
        SetStage(StageType.START_STAGE_1_ROUND_3, 2000);
        List<int> round = new List<int>();
        round.Add(217487);
        Sp(217487, 334.844f, 339.92618f, 96.09094f, (byte)18, 6000);
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!IsSpawn(round))
            {
                StartStage1Round4();
            }
            round.Clear();
        }, 63000L);
    }

    private void StartStage1Round4()
    {
        SetStage(StageType.START_STAGE_1_ROUND_4, 2000);
        List<int> round = new List<int>();
        round.Add(217491);
        Sp(217491, 341.03156f, 361.04315f, 96.09093f, (byte)90, 6000);
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!IsSpawn(round))
            {
                StartStage1Round5();
            }
            round.Clear();
        }, 167000L);
    }

    private void StartStage1Round5()
    {
        SetStage(StageType.START_STAGE_1_ROUND_5, 2000);
        Sp(217493, 332.093f, 349.36847f, 96.090935f, (byte)0, 8000);
    }

    private void Teleport(float x, float y, float z, byte h)
    {
        foreach (Player playerInside in instance.GetPlayersInside())
        {
            if (playerInside.IsOnline())
            {
                if (!GetPlayerReward(playerInside.GetObjectId()).IsPlayerDefeated())
                {
                    Teleport(playerInside, x, y, z, h);
                }
                else
                    MoveToReadyRoom(playerInside);
            }
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
                Teleport(player, 381.41684f, 346.78162f, 96.74763f, (byte)43);
                break;
            case 5:
                Teleport(player, 1260.9495f, 832.87317f, 358.60562f, (byte)92);
                break;
            case 6:
                Teleport(player, 1592.8813f, 149.78166f, 128.81355f, (byte)117);
                break;
            case 7:
                Teleport(player, 1820.8805f, 795.80914f, 470.18304f, (byte)51);
                break;
            case 8:
                Teleport(player, 1780.103f, 1723.458f, 304.039f, (byte)53); // get retail
                break;
            case 9:
                Teleport(player, 1359.5046f, 1751.7952f, 319.59406f, (byte)30);
                break;
            case 10:
                // todo
                break;
        }
    }

    public override void LeaveInstance(Player player)
    {
        TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    public override void OnLeaveInstance(Player player)
    {
        base.OnLeaveInstance(player);
        CruciblePlayerReward reward = GetPlayerReward(player.GetObjectId());
        if (reward != null)
        {
            reward.SetPlayerLeave();
        }
    }

    public override void OnStopTraining(Player player)
    {
        DoReward(player);
    }

    private void Sp(int npcId, float x, float y, float z, byte h, int time)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed)
            {
                lock (npcs)
                {
                    npcs.Add((Npc)Spawn(npcId, x, y, z, h));
                }
            }
        }, (long)time);
    }

    private Npc Sp(int npcId, float x, float y, float z, byte h)
    {
        Npc npc = null;
        if (!isInstanceDestroyed)
        {
            npc = (Npc)Spawn(npcId, x, y, z, h);
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
            case 217756:
            case 217735:
                ItemService.AddItem(player, 186000124, 1);
                PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1401009));
                npc.GetController().Delete();
                break;
        }
    }

    public override void OnDropRegistered(Npc npc, int winnerObj)
    {
        ISet<DropItem> dropItems = DropRegistrationService.GetInstance().GetCurrentDropMap().GetValueOrDefault(npc.GetObjectId());
        int npcId = npc.GetNpcId();
        int itemId = 0;
        switch (npcId)
        {
            case 217740:
            case 217741:
            case 217743:
            case 217742:
                dropItems.Clear();
                itemId = 162000108;
                if (Rnd.Chance() < 6)
                {
                    itemId = npc.GetNpcId() switch
                    {
                        217740 => 125002593,
                        217741 => 125002595,
                        217742 => 125002592,
                        217743 => 125002594,
                        _ => itemId,
                    };
                }
                break;
            case 217750:
                dropItems.Clear();
                itemId = 162000109;
                if (Rnd.Chance() < 6)
                {
                    itemId = Rnd.Get(1, 9) switch
                    {
                        1 => 101700911,
                        2 => 100201003,
                        3 => 100900869,
                        4 => 100100874,
                        5 => 100500886,
                        6 => 101300836,
                        7 => 100600944,
                        8 => 100001135,
                        9 => 101500895,
                        _ => itemId,
                    };
                }
                break;
            case 217753: // skillbooks
                if (Rnd.Chance() < 51)
                {
                    Race race = instance.GetRegisteredTeam().GetRace();
                    itemId = Rnd.Get(1, 4) switch
                    {
                        1 => 169500935,
                        2 => 169500933,
                        3 => race == Race.ELYOS ? 169500947 : 169500951,
                        4 => race == Race.ELYOS ? 169500927 : 169500931,
                        _ => itemId,
                    };
                }
                break;
        }
        if (itemId != 0)
            dropItems.Add(DropRegistrationService.GetInstance().RegDropItem(1, 0, npcId, itemId, 1));
    }
}
