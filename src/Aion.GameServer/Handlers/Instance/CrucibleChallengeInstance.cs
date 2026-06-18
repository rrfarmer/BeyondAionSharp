using System.Collections.Generic;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;
using ItemService = Aion.GameServer.Services.Items.ItemService;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/crucible/CrucibleChallengeInstance (xTz) : CrucibleInstance. 1:1.
/// </summary>
[InstanceID(300320000)]
public class CrucibleChallengeInstance : CrucibleInstance
{
    private int stage1Count;
    private ScheduledTask bonusTimer;
    private int spawnCount;
    private int dieCount;
    private int rewardCount;

    public CrucibleChallengeInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        base.OnInstanceCreate();
        Sp(205668, 345.77954f, 1662.6697f, 95.25f, (byte)0, 0);
        Sp(205682, 383.3434f, 1667.2977f, 97.79293f, (byte)60, 0);
    }

    public override void OnEnterInstance(Player player)
    {
        base.OnEnterInstance(player);
        if (GetPlayerReward(player.GetObjectId()).IsRewarded())
            DoReward(player);
        else
            SendPacket(null, 0);
        SendEventPacket();
    }

    private void SendPacket(string npcL10n, int points)
    {
        PacketSendUtility.BroadcastToMap(instance, new SM_INSTANCE_SCORE(instance.GetMapId(), new CrucibleScoreWriter(instanceScore)));
        if (npcL10n != null)
            PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(npcL10n, points));
    }

    private void SendEventPacket()
    {
        PacketSendUtility.BroadcastToMap(instance, new SM_INSTANCE_STAGE_INFO(2, stageType.GetId(), stageType.GetType_()));
    }

    public override void OnDie(Npc npc)
    {
        int npcId = npc.GetNpcId();
        IncreasePointsForKill(npc, npc.GetAggroList().GetMostPlayerDamage());
        switch (npcId)
        {
            case 217784: // ely
            case 217785: // asmo
                if (++stage1Count == 2)
                {
                    Sp(npcId, 342.855f, 1652.6703f, 95.63069f, (byte)0, 0);
                    Sp(npcId, 342.83942f, 1673.2863f, 95.66078f, (byte)0, 0);
                }
                else if (AllDeadOrDespawned(npcId) && ChangeStage(StageType.START_STAGE_1_ROUND_2, 2000))
                {
                    Sp(217783, 337.82263f, 1662.9073f, 95.27217f, (byte)0, 4000);
                }
                break;
            case 217783:
                if (ChangeStage(StageType.PASS_STAGE_1, 0))
                    Sp(217758, 347.24026f, 1660.2524f, 95.35922f, (byte)0, 0);
                break;
            case 217843:
            case 217845:
                if (ChangeStage(StageType.START_STAGE_3_ROUND_2, 2000))
                {
                    Sp(217848, 1309.2858f, 1732.6802f, 316.0825f, (byte)0, 1000);
                    Sp(217848, 1306.1871f, 1731.7861f, 316.25168f, (byte)0, 1000);
                    Sp(217848, 1308.4879f, 1734.4999f, 316f, (byte)0, 1000);
                    Sp(217848, 1308.67419f, 1736.2063f, 316f, (byte)0, 1000);
                    Sp(217848, 1306.9414f, 1736.5365f, 316f, (byte)0, 1000);
                    Sp(217848, 1305.2534f, 1735.1603f, 315.94586f, (byte)0, 1000);
                    Sp(217847, 1307.2786f, 1734.3274f, 316f, (byte)0, 3000);
                    ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        Npc fieryGomju = GetNpc(217847);
                        if (fieryGomju != null && Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(fieryGomju, 19817, 1, fieryGomju).UseSkill()) // Brown Bear's Roar
                            ThreadPoolManager.GetInstance().Schedule(() => GetNpcs(217848).ForEach(dracuni => dracuni.GetController().Die()), 1000L);
                    }, 4000L);
                }
                break;
            case 217847:
                Sp(205676, 1307.6722f, 1732.9865f, 316.07373f, (byte)6, 0);
                break;
            case 217788:
            case 217789:
            case 217790:
                if (ChangeStage(StageType.START_STAGE_4_ROUND_2, 2000))
                    Sp(217794, 1271f, 791.5752f, 436.63998f, (byte)0, 6000);
                break;
            case 217791:
            case 217792:
            case 217793:
                if (ChangeStage(StageType.START_STAGE_4_ROUND_2, 2000))
                    Sp(217795, 1258.8425f, 237.91522f, 405.3968f, (byte)0, 6000);
                break;
            case 217794:
            case 217795:
                if (stageType != StageType.START_BONUS_STAGE_4)
                {
                    ChangeStage(StageType.PASS_STAGE_4, 0);
                    ChangeStage(StageType.START_BONUS_STAGE_4, 4000);
                    if (npcId == 217794)
                        Sp(217820, 1266.9661f, 791.5348f, 436.64014f, (byte)0, 4000);
                    else
                        Sp(217820, 1251.1598f, 237.97736f, 405.3968f, (byte)0, 4000);
                }
                break;
            case 217786:
            case 217787:
                break; // just despawn (see below switch)
            case 217807: // ely
            case 217808: // ely
            case 217809: // ely
            case 217810: // ely
            case 217811: // asmo
            case 217812: // asmo
            case 217813: // asmo
            case 217814: // asmo
            case 217815: // ely
            case 217816: // asmo
                if (ChangeStage(StageType.START_STAGE_5_ROUND_2, 2000))
                {
                    switch (Rnd.Get(1, 3))
                    {
                        case 1:
                            Sp(217806, 332.3786f, 349.31204f, 96.090935f, (byte)0, 4000);
                            break;
                        case 2:
                            Sp(218562, 332.3786f, 349.31204f, 96.090935f, (byte)0, 4000);
                            break;
                        case 3:
                            Sp(218565, 332.3786f, 349.31204f, 96.090935f, (byte)0, 4000);
                            break;
                    }
                }
                break;
            case 217806:
            case 218562:
            case 218565:
                if (ChangeStage(StageType.PASS_STAGE_5, 0))
                    Sp(205678, 346.64798f, 349.25586f, 96.090965f, (byte)0, 0);
                break;
            case 217819:
                if (ChangeStage(StageType.PASS_STAGE_6, 0))
                {
                    ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        Player player = null;
                        if (instance.GetPlayersInside().Count != 0)
                        {
                            player = instance.GetPlayersInside()[0];
                        }
                        npc.GetController().Delete();
                        if (player != null)
                        {
                            QuestState qs = player.GetQuestStateList().GetQuestState(player.GetRace() == Race.ASMODIANS ? 28208 : 18208);
                            if (qs != null && qs.GetStatus() == QuestStatus.START)
                            {
                                if (qs.GetQuestVarById(0) == 1 || qs.GetQuestVarById(1) == 4) // 5 x Vanktrist Spacetwine killed
                                {
                                    Sp(730459, 1765.7104f, 1281.2388f, 389.11743f, (byte)0, 2000);
                                    return;
                                }
                            }
                        }
                        Sp(205679, 1765.522f, 1282.1051f, 389.11743f, (byte)0, 2000);
                    }, 4000L);
                }
                return; // return instead of break since despawn is delayed
            case 217837:
            case 217838:
                Player p = npc.GetAggroList().GetMostPlayerDamage();
                if (p != null)
                {
                    if (p.IsInsideZone(ZoneName.Get("ILLUSION_STADIUM_1_300320000")))
                    {
                        Sp(217820, 1251.1598f, 237.97736f, 405.3968f, (byte)0, 0);
                    }
                    else if (p.IsInsideZone(ZoneName.Get("ILLUSION_STADIUM_6_300320000")))
                    {
                        Sp(217820, 1266.9661f, 791.5348f, 436.64014f, (byte)0, 0);
                    }
                }
                break;
            case 217800:
            case 217801:
                if (AllDeadOrDespawned(217800, 217801))
                    StartBonusStage2();
                break;
            case 217802:
                DeleteAliveNpcs(217803);
                bonusTimer.Cancel();
                PassStage2();
                break;
            case 217797:
            case 217798:
            case 217799:
                if (AllDeadOrDespawned(217797, 217798, 217799))
                    StartBonusStage2();
                break;
            case 217841:
            case 218561:
                int spawnNpcId;
                switch (npc.GetNpcId())
                {
                    case 218561:
                        spawnNpcId = 218560;
                        break;
                    case 217841:
                        spawnNpcId = 217840;
                        break;
                    default:
                        return;
                }
                if (Rnd.NextBoolean())
                    Sp(spawnNpcId, 1299.7399f, 1733.5763f, 316.6515f, (byte)0, 0);
                else
                    Sp(spawnNpcId, 1297.627f, 1729.8733f, 316.875f, (byte)0, 0);
                break;
            case 217803:
                if (++dieCount == 5)
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_Solo_SB1_5Dead_BROADCAST());
                if (bonusTimer.IsCancelled && AllDeadOrDespawned(npcId))
                {
                    Npc poppy = GetNpc(217802);
                    if (poppy != null)
                    {
                        PassStage2();
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_Solo_SB1_Succ_BROADCAST());
                        Sp(218571, poppy.GetX(), poppy.GetY(), poppy.GetZ(), (byte)0, 0);
                        poppy.GetController().Delete();
                    }
                }
                break;
            case 217844:
            case 217842:
                if (GetNpcs(npcId).Count == 2)
                    ThreadPoolManager.GetInstance().Schedule(() => StartStage3Round1_1(npcId), 5000L);
                break;
            case 218185:
                DeleteAliveNpcs(218190, 218191);
                Sp(730460, 1759.2914f, 1786.37f, 389.11713f, (byte)96, 0);
                PacketSendUtility.BroadcastMessage(npc, 342495);
                break;
            default:
                return; // don't despawn
        }
        npc.GetController().Delete();
    }

    private void IncreasePointsForKill(Npc npc, Player killer)
    {
        if (killer == null)
            return;
        int points = 0;
        switch (npc.GetNpcId())
        {
            case 217784:
            case 217785:
                points = 120;
                break;
            case 217786:
            case 217787:
                points = 200;
                break;
            case 217797:
            case 217798:
            case 217799:
                points = 1100;
                break;
            case 217788:
            case 217789:
            case 217790:
            case 217791:
            case 217792:
            case 217793:
                points = 1300;
                break;
            case 217843:
            case 217845:
                points = 1400;
                break;
            case 217783:
                points = 1600;
                break;
            case 217800:
            case 217801:
                points = 1650;
                break;
            case 217807: // ely
            case 217808: // ely
            case 217809: // ely
            case 217810: // ely
            case 217811: // asmo
            case 217812: // asmo
            case 217813: // asmo
            case 217814: // asmo
            case 217815: // ely
            case 217816: // asmo
                points = 2500;
                break;
            case 217847:
                points = 4600;
                break;
            case 217795:
            case 217794:
                points = 5000;
                break;
            case 217806:
            case 218562:
            case 218565:
                points = 5800;
                break;
            case 217819:
                points = 7200;
                break;
        }
        if (points != 0)
        {
            GetPlayerReward(killer.GetObjectId()).AddPoints(points);
            SendPacket(npc.GetObjectTemplate().GetL10n(), points);
        }
    }

    private bool AllDeadOrDespawned(params int[] npcIds)
    {
        foreach (Npc npc in instance.GetNpcs())
        {
            foreach (int npcId in npcIds)
            {
                if (npc.GetNpcId() == npcId && !npc.IsDead())
                    return false;
            }
        }
        return true;
    }

    private void StartStage3Round1_1(int npcId)
    {
        int bossId = npcId switch
        {
            217844 => 217845,
            217842 => 217843,
            _ => 0,
        };
        if (GetNpc(bossId) != null)
            return;
        Sp(bossId, 1287.6239f, 1724.2721f, 317.1485f, (byte)6, 2000);
        DeleteAliveNpcs(npcId, 217840, 218560, 218561, 217841);
    }

    public override void DoReward(Player player)
    {
        CruciblePlayerReward playerReward = GetPlayerReward(player.GetObjectId());
        if (playerReward == null)
        {
            return;
        }
        float reward = 0.01f * playerReward.GetPoints() + 120;
        if (!playerReward.IsRewarded())
        {
            playerReward.SetRewarded();
            ItemService.AddItem(player, 186000130, (int)reward);
            playerReward.SetInsignia((int)reward);
            instanceScore.SetInstanceProgressionType(InstanceProgressionType.END_PROGRESS);
        }
        PacketSendUtility.SendPacket(player, new SM_INSTANCE_SCORE(instance.GetMapId(), new CrucibleScoreWriter(instanceScore)));
    }

    private void StartBonusStage2()
    {
        if (!ChangeStage(StageType.PASS_STAGE_2, 0))
            return;

        ThreadPoolManager.GetInstance().Schedule(() => ChangeStage(StageType.START_BONUS_STAGE_2, 1000), 2000L);

        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed)
            {
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDArena_Solo_SB1_START_BROADCAST());
                SpawnTemplate template = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(mapId, 217802, 1780.5665f, 307.40463f, 469.25f, (byte)0);
                template.SetWalkerId("3003200001");
                Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(template, instance.GetInstanceId());
            }
        }, 6000L);

        bonusTimer = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            spawnCount++;
            SpawnBonus2();
            if (spawnCount == 10)
                bonusTimer.Cancel();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(10000), System.TimeSpan.FromMilliseconds(4000));
    }

    private void SpawnBonus2()
    {
        switch (spawnCount)
        {
            case 1:
                Sp(217803, 1789.923f, 310.853f, 469.25f, (byte)0, 0);
                break;
            case 2:
                Sp(217803, 1780.9126f, 315.63382f, 469.25f, (byte)0, 0);
                break;
            case 3:
                Sp(217803, 1784.4752f, 300.1912f, 469.25f, (byte)0, 0);
                break;
            case 4:
                Sp(217803, 1793.2847f, 312.59842f, 469.25f, (byte)0, 0);
                break;
            case 5:
                Sp(217803, 1777.6609f, 316.1443f, 469.25f, (byte)0, 0);
                break;
            case 6:
                Sp(217803, 1776.5625f, 299.30347f, 469.25f, (byte)0, 0);
                break;
            case 7:
                Sp(217803, 1793.6469f, 301.40973f, 469.25f, (byte)0, 0);
                break;
            case 8:
                Sp(217803, 1797.4891f, 310.50418f, 469.25f, (byte)0, 0);
                break;
            case 9:
                Sp(217803, 1782.552f, 319.43973f, 469.25f, (byte)0, 0);
                break;
            case 10:
                Sp(217803, 1776.1577f, 305.80396f, 469.25f, (byte)0, 0);
                break;
        }
    }

    public override void OnInstanceDestroy()
    {
        base.OnInstanceDestroy();
        if (bonusTimer != null)
        {
            bonusTimer.Cancel();
        }
    }

    private void PassStage2()
    {
        if (GetNpc(205675) == null) // record keeper
            Sp(205675, 1784.5883f, 306.98645f, 469.25f, (byte)0, 0);
    }

    public override bool OnDie(Player player, Creature lastAttacker)
    {
        base.OnDie(player, lastAttacker);
        int place = 0;
        if (IsInZone(ZoneName.Get("ILLUSION_STADIUM_4_300320000"), player)) // stage 1
        {
            place = 1;
        }
        else if (IsInZone(ZoneName.Get("ILLUSION_STADIUM_8_300320000"), player)) // stage 2
        {
            place = 2;
        }
        else if (IsInZone(ZoneName.Get("ILLUSION_STADIUM_5_300320000"), player)) // stage 3
        {
            place = 3;
        }
        else if (IsInZone(ZoneName.Get("ILLUSION_STADIUM_1_300320000"), player)) // stage 4
        {
            place = 4;
        }
        else if (IsInZone(ZoneName.Get("ILLUSION_STADIUM_6_300320000"), player)) // stage 4
        {
            place = 5;
        }
        else if (IsInZone(ZoneName.Get("ILLUSION_STADIUM_2_300320000"), player)) // stage 5
        {
            place = 6;
        }
        else if (IsInZone(ZoneName.Get("ILLUSION_STADIUM_7_300320000"), player)) // stage 6
        {
            place = 7;
        }
        GetPlayerReward(player.GetObjectId()).SetSpawnPosition(place);
        return true;
    }

    public override bool OnReviveEvent(Player player)
    {
        base.OnReviveEvent(player);
        switch (GetPlayerReward(player.GetObjectId()).GetSpawnPosition())
        {
            case 1:
                Teleport(player, 380.35417f, 1663.3583f, 97.60156f, (byte)0);
                break;
            case 2:
                Teleport(player, 1819.8119f, 304.92932f, 469.4142f, (byte)0);
                break;
            case 3:
                Teleport(player, 1354.9386f, 1748.1531f, 318.6173f, (byte)70);
                break;
            case 4:
                Teleport(player, 1294.1417f, 234.49684f, 406.0368f, (byte)0);
                break;
            case 5:
                Teleport(player, 1307.3776f, 790.7324f, 437.29678f, (byte)0);
                break;
            case 6:
                Teleport(player, 381.7477f, 346.63913f, 96.74763f, (byte)0);
                break;
            case 7:
                Teleport(player, 1750.2677f, 1253.5453f, 389.11765f, (byte)10);
                break;
            default:
                InstanceService.DestroyInstance(player.GetPosition().GetWorldMapInstance());
                break;
        }

        if (player.GetInventory().GetFirstItemByItemId(186000134) == null)
        {
            DoReward(player);
        }
        return true;
    }

    public override void LeaveInstance(Player player)
    {
        InstanceService.DestroyInstance(player.GetPosition().GetWorldMapInstance());
    }

    public override void OnStopTraining(Player player)
    {
        DoReward(player);
    }

    public override void OnChangeStage(StageType type)
    {
        if (!ChangeStage(type, 2000))
            return;
        int npcId = 0, barrelId = 0;
        Player player = null;
        if (instance.GetPlayersInside().Count != 0)
        {
            player = instance.GetPlayersInside()[0];
        }
        switch (stageType)
        {
            case StageType.START_STAGE_1_ROUND_1:
                if (player != null)
                {
                    switch (player.GetRace())
                    {
                        case Race.ELYOS:
                            npcId = 217784;
                            break;
                        case Race.ASMODIANS:
                            npcId = 217785;
                            break;
                    }
                    Sp(npcId, 334.85098f, 1657.8495f, 95.77262f, (byte)0, 4000);
                    Sp(npcId, 334.74506f, 1668.7478f, 95.67427f, (byte)0, 4000);
                    Sp(npcId, 350.63846f, 1663.84f, 95.385f, (byte)0, 4000);
                }
                break;
            case StageType.START_STAGE_2_ROUND_1:
                switch (Rnd.Get(1, 2))
                {
                    case 1:
                        Sp(217800, 1779.9993f, 305.76697f, 469.25f, (byte)30, 4000);
                        Sp(217801, 1779.9845f, 308.82013f, 469.25f, (byte)90, 4000);
                        break;
                    case 2:
                        Sp(217797, 1771.5549f, 307.09192f, 469.25f, (byte)30, 4000);
                        Sp(217798, 1775.9717f, 317.69516f, 469.25f, (byte)90, 4000);
                        Sp(217799, 1775.9646f, 296.3559f, 469.25f, (byte)90, 4000);
                        break;
                }
                break;
            case StageType.START_STAGE_3_ROUND_1:
                switch (Rnd.Get(1, 2))
                {
                    case 1:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_CHAT_IDArena_Solo_S4_System2());
                        npcId = 217844;
                        barrelId = 218560;
                        break;
                    case 2:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_CHAT_IDArena_Solo_S4_System1());
                        npcId = 217842;
                        barrelId = 217840;
                        break;
                }
                Sp(barrelId, 1302.4689f, 1732.1583f, 316.4486f, (byte)0, 2000);
                Sp(npcId, 1288.6692f, 1730.5212f, 316.85333f, (byte)0, 4000);
                Sp(npcId, 1296.7896f, 1726.1091f, 316.82837f, (byte)0, 4000);
                Sp(npcId, 1294.3018f, 1730.9f, 316.875f, (byte)0, 4000);
                Sp(npcId, 1292.1957f, 1726.921f, 316.875f, (byte)0, 4000);
                Sp(npcId, 1293.0376f, 1722.3871f, 316.93515f, (byte)0, 4000);
                break;
            case StageType.START_STAGE_4_ROUND_1:
                npcId = Rnd.Get(1, 3) switch
                {
                    1 => 217788,
                    2 => 217789,
                    _ => 217790,
                };
                Sp(217786, 1263.4213f, 791.8533f, 436.64014f, (byte)60, 4000);
                Sp(217786, 1267.2097f, 804.04456f, 436.64008f, (byte)60, 4000);
                Sp(217786, 1267.0653f, 781.0253f, 436.64017f, (byte)60, 4000);
                Sp(npcId, 1253.5984f, 791.6149f, 436.64008f, (byte)0, 4000);
                break;
            case StageType.START_ALTERNATIVE_STAGE_4_ROUND_1:
                npcId = Rnd.Get(1, 3) switch
                {
                    1 => 217791, // http://www.aiondatabase.com/npc/217805 spawn 2 5%
                    2 => 217792,
                    _ => 217793,
                };
                Sp(217787, 1252.525f, 248.50781f, 405.38016f, (byte)60, 4000);
                Sp(217787, 1250.0901f, 237.69656f, 405.39676f, (byte)60, 4000);
                Sp(217787, 1253.0117f, 225.77977f, 405.3801f, (byte)60, 4000);
                Sp(npcId, 1242.3618f, 237.89081f, 405.3801f, (byte)0, 4000);
                break;
            case StageType.START_STAGE_5_ROUND_1:
                if (player != null)
                {
                    npcId = player.GetRace() switch
                    {
                        Race.ELYOS => Rnd.Get(1, 5) switch
                        {
                            1 => 217807,
                            2 => 217808,
                            3 => 217809,
                            4 => 217810,
                            _ => 217815,
                        },
                        Race.ASMODIANS => Rnd.Get(1, 5) switch
                        {
                            1 => 217811,
                            2 => 217812,
                            3 => 217813,
                            4 => 217814,
                            _ => 217816,
                        },
                        _ => npcId,
                    };
                    Sp(npcId, 335.7365f, 337.93097f, 96.0909f, (byte)0, 4000);
                }
                break;
            case StageType.START_STAGE_6_ROUND_1:
                Sp(217819, 1769.4579f, 1290.9342f, 389.11728f, (byte)80, 4000);
                break;
        }
    }

    private void Sp(int npcId, float x, float y, float z, byte h, int time)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed)
                Spawn(npcId, x, y, z, h);
        }, (long)time);
    }

    private bool ChangeStage(StageType type, int time)
    {
        if (this.stageType == type)
            return false;
        this.stageType = type;
        ThreadPoolManager.GetInstance().Schedule(SendEventPacket, (long)time);
        return true;
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 217758:
                Spawn(205674, 345.52954f, 1662.6697f, 95.25f, (byte)0);
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
            case 217758:
                dropItems.Add(DropRegistrationService.GetInstance().RegDropItem(1, winnerObj, npcId, 186000134, 1));
                break;
            case 218571:
                dropItems.Clear();
                switch (Rnd.Get(1, 3))
                {
                    case 1:
                        itemId = 182006429;
                        break;
                    case 2:
                        itemId = 182006430;
                        break;
                    case 3:
                        itemId = 182006431;
                        break;
                }
                dropItems.Add(DropRegistrationService.GetInstance().RegDropItem(1, winnerObj, npcId, itemId, 1));
                break;
            case 218185:
                dropItems.Clear();
                dropItems.Add(DropRegistrationService.GetInstance().RegDropItem(1, winnerObj, npcId, 188051349, 1));
                break;
            case 217827:
            case 217828:
            case 217829:
                int count = 0;
                switch (rewardCount)
                {
                    case 0:
                        switch (Rnd.Get(1, 3))
                        {
                            case 1:
                                count = 1;
                                break;
                            case 2:
                                count = 10;
                                break;
                        }
                        break;
                    case 1:
                        switch (Rnd.Get(1, 3))
                        {
                            case 1:
                                count = 2;
                                break;
                            case 2:
                                count = 18;
                                break;
                        }
                        break;
                    case 2:
                        switch (Rnd.Get(1, 3))
                        {
                            case 1:
                                count = 3;
                                break;
                            case 2:
                                count = 26;
                                break;
                            case 3:
                                count = 250;
                                break;
                        }
                        break;
                }

                switch (npcId)
                {
                    case 217827:
                        DeleteAliveNpcs(217828, 217829);
                        break;
                    case 217828:
                        DeleteAliveNpcs(217827, 217829);
                        break;
                    case 217829:
                        DeleteAliveNpcs(217827, 217828);
                        break;
                }
                if (count == 0)
                {
                    if (rewardCount == 0)
                    {
                        Sp(217837, npc.GetX(), npc.GetY(), npc.GetZ(), (byte)0, 0);
                    }
                    else if (rewardCount == 1)
                    {
                        Sp(217838, npc.GetX(), npc.GetY(), npc.GetZ(), (byte)0, 0);
                    }
                    npc.GetController().Delete();
                    rewardCount++;
                    return;
                }
                rewardCount++;
                if (npc.IsInsideZone(ZoneName.Get("ILLUSION_STADIUM_1_300320000")))
                {
                    Sp(205667, 1258.8464f, 237.85518f, 405.39673f, (byte)0, 0);
                }
                else if (npc.IsInsideZone(ZoneName.Get("ILLUSION_STADIUM_6_300320000")))
                {
                    Sp(205677, 1271.5472f, 791.36145f, 436.64017f, (byte)0, 0);
                }
                dropItems.Clear();
                dropItems.Add(DropRegistrationService.GetInstance().RegDropItem(1, winnerObj, npcId, 186000130, count));
                break;
        }
    }
}
