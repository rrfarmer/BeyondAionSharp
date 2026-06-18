using System;
using System.Collections.Generic;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/pvp/KamarBattlefieldInstance (xTz) : BasicPvpInstance.
/// Morale Boost is applied to re-spawning players which are a part of the loosing side (ID: 10)
/// REINFORCE_MEMBER for 2 minutes; PREPARING for 1 minute; START_PROGRESS for 30 minutes. 1:1.
/// </summary>
[InstanceID(301120000)]
public class KamarBattlefieldInstance : BasicPvpInstance
{
    private static readonly List<WorldPosition> generalsPos = new List<WorldPosition>();
    private static readonly List<WorldPosition> garnonPos = new List<WorldPosition>();
    private const int MAX_PLAYERS_PER_FACTION = 12;
    private byte timeInMin = unchecked((byte)-1);

    static KamarBattlefieldInstance()
    {
        generalsPos.Add(new WorldPosition(301120000, 1437.7f, 1368.7f, 600.8967f, (byte)40));
        generalsPos.Add(new WorldPosition(301120000, 1172.2f, 1445, 586.55f, (byte)35));
        generalsPos.Add(new WorldPosition(301120000, 1428.67f, 1617.67f, 599.9493f, (byte)70));
        garnonPos.Add(new WorldPosition(301120000, 1138.4039f, 1619.2574f, 598.43506f, (byte)53));
        garnonPos.Add(new WorldPosition(301120000, 1184.5309f, 1408.2471f, 586.6199f, (byte)6));
        garnonPos.Add(new WorldPosition(301120000, 1241.9187f, 1557.2854f, 585.2431f, (byte)46));
        garnonPos.Add(new WorldPosition(301120000, 1270.4377f, 1455.0625f, 595.2903f, (byte)13));
        garnonPos.Add(new WorldPosition(301120000, 1325.634f, 1326.134f, 596.4888f, (byte)106));
        garnonPos.Add(new WorldPosition(301120000, 1346.7902f, 1717.1029f, 598.43396f, (byte)30));
        garnonPos.Add(new WorldPosition(301120000, 1410.7446f, 1579.752f, 595.7288f, (byte)93));
        garnonPos.Add(new WorldPosition(301120000, 1455.881f, 1392.8229f, 598.5873f, (byte)10));
        garnonPos.Add(new WorldPosition(301120000, 1540.113f, 1395.6737f, 596.625f, (byte)105));
    }

    public KamarBattlefieldInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected override void OnStart()
    {
        UpdateProgress(InstanceProgressionType.PREPARING);
        instance.GetPlayersInside().ForEach(PortToStartPosition); // split groups
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(EndPreparingAndStart, 60000L));
    }

    private void EndPreparingAndStart()
    {
        UpdateProgress(InstanceProgressionType.START_PROGRESS);
        OpenFirstDoors();

        WorldPosition pos = Rnd.Get(garnonPos);
        Spawn(801903, pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading());
        tasks.Add(ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            OnTimeProgressed();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(0), System.TimeSpan.FromMilliseconds(60000)));
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => OnStop(false), 1800000L));
    }

    private void OnTimeProgressed()
    {
        switch (++timeInMin)
        {
            case 5:
                Spawn(802016, 1440.3145f, 1227.4073f, 587.36328f, (byte)0, 223);
                Spawn(802017, 1109.5887f, 1532.7554f, 586.6358f, (byte)0, 221);
                Spawn(802018, 1213.4902f, 1363.4617f, 613.93866f, (byte)0, 225);
                Spawn(802019, 1527.215f, 1561.5153f, 613.47742f, (byte)0, 224);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDKamar_StartTeleporter_Spawn());
                break;
            case 10:
                Spawn(801772, 1353.1956f, 1413.8037f, 598.75f, (byte)0);
                Spawn(801772, 1356.0574f, 1479.6165f, 594.15155f, (byte)0);
                Spawn(801772, 1371.584f, 1550.1755f, 595.375f, (byte)0);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDKamar_YunSupply_Spawn());
                break;
            case 12:
                SpawnAndSetRespawn(701808, 1285.834f, 1489.1963f, 595.66486f, (byte)0, 180);
                SpawnAndSetRespawn(701912, 1414.2816f, 1463.925f, 598.7676f, (byte)0, 180);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDKamar_SeigeWeapon_Spawn());
                break;
            case 14:
                Spawn(801962, 1325.73f, 1521.42f, 700.0f, (byte)15);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDKamar_Dreadgion_Spawn());
                break;
            case 15:
                Spawn(232847, 1221.6609f, 1563.3887f, 585.343f, (byte)30);
                Spawn(232847, 1312.6637f, 1426.5917f, 596.912f, (byte)0);
                Spawn(232847, 1421.0524f, 1503.8083f, 597.0f, (byte)0);
                Spawn(232847, 1347.8895f, 1278.5276f, 593.75f, (byte)0);
                Spawn(232848, 1318.0083f, 1423.2358f, 697.1422f, (byte)0);
                Spawn(232848, 1352.3656f, 1281.6598f, 593.75f, (byte)0);
                Spawn(232848, 1415.9098f, 1507.7222f, 597.0f, (byte)0);
                Spawn(232848, 1226.0847f, 1566.771f, 585.25f, (byte)53);
                Spawn(232849, 1328.4695f, 1667.7284f, 598.75f, (byte)0);
                Spawn(232849, 1316.2865f, 1526.8649f, 594.4299f, (byte)100);
                Spawn(232849, 1168.7726f, 1606.4891f, 598.7017f, (byte)0);
                Spawn(232850, 1134.1378f, 1498.5004f, 585.3203f, (byte)15);
                Spawn(232850, 1529.4595f, 1402.4359f, 597.5f, (byte)20);
                Spawn(232850, 1322.879f, 1531.0671f, 594.4299f, (byte)100);
                Spawn(232851, 1531.8644f, 1454.7493f, 596.7186f, (byte)80);
                Spawn(232851, 1321.8517f, 1525.4725f, 594.4299f, (byte)100);
                Spawn(232851, 1133.2808f, 1504.6725f, 585.22835f, (byte)116);
                Spawn(233261, 1357.5049f, 1434.2639f, 598.875f, (byte)88);
                Spawn(233261, 1375.0513f, 1531.0963f, 597.12115f, (byte)16);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDKamar_DrakanH_Spawn());
                break;
            case 18:
                List<WorldPosition> temp = new List<WorldPosition>(generalsPos);
                int index = Rnd.NextInt(temp.Count);
                WorldPosition pos1 = temp[index];
                temp.RemoveAt(index);
                Spawn(232854, pos1.GetX(), pos1.GetY(), pos1.GetZ(), pos1.GetHeading());
                index = Rnd.NextInt(temp.Count);
                pos1 = temp[index];
                temp.RemoveAt(index);
                Spawn(232853, pos1.GetX(), pos1.GetY(), pos1.GetZ(), pos1.GetHeading());
                index = Rnd.NextInt(temp.Count);
                pos1 = temp[index];
                temp.RemoveAt(index);
                Spawn(232852, pos1.GetX(), pos1.GetY(), pos1.GetZ(), pos1.GetHeading());
                Spawn(232846, 1442.18f, 1370.7f, 600.6902f, (byte)40);
                Spawn(232846, 1434.45f, 1365.7f, 600.70776f, (byte)40);
                Spawn(232846, 1178.58f, 1445.6f, 586.5563f, (byte)35);
                Spawn(232846, 1166.8f, 1442.0f, 586.5563f, (byte)35);
                Spawn(232846, 1427.12f, 1621.19f, 599.9493f, (byte)70);
                Spawn(232846, 1431.09f, 1613.77f, 599.9493f, (byte)70);
                // spawn Bark
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDKamar_DrakanGeneral_Spawn());
                break;
            case 25:
                Spawn(232857, 1250.54f, 1646.07f, 584.9f, (byte)100);
                Spawn(232859, 1246.65f, 1645.06f, 584.9f, (byte)100);
                Spawn(232859, 1253.43f, 1649.13f, 584.9f, (byte)100);
                Spawn(232858, 1388.45f, 1438.7f, 600, (byte)40);
                Spawn(232860, 1394, 1440.34f, 600, (byte)40);
                Spawn(232860, 1385.74f, 1435.5f, 600, (byte)40);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDKamar_LightDarkGeneral_Spawn());
                break;
        }
    }

    protected override void SetAndDistributeRewards(Player player, PvpInstancePlayerReward reward, Race winningRace, bool isBossKilled)
    {
        int scorePoints = instanceScore.GetPointsByRace(reward.GetRace());
        if (reward.GetRace() == winningRace)
        {
            reward.SetBaseAp(instanceScore.GetWinnerApReward() + (isBossKilled ? 3850 : 0));
            reward.SetBonusAp(2 * scorePoints / MAX_PLAYERS_PER_FACTION);
            reward.SetBaseGp(100);
            reward.SetReward1(188052670, 1, 0); // Kamar Victory Box
            if (isBossKilled && Rnd.Chance() < 5)
                reward.SetReward2(188950020, 1, 0); // CUSTOM: Special Courier Pass (Abyss Mythic/Lv. 61-65)
        }
        else
        {
            reward.SetBaseAp(instanceScore.GetLoserApReward());
            reward.SetBonusAp(scorePoints / MAX_PLAYERS_PER_FACTION);
            reward.SetBaseGp(10);
            if (winningRace == Race.NONE)
                reward.SetBaseAp(instanceScore.GetDrawApReward()); // Base AP are overridden in a draw case
        }
        DistributeRewards(player, reward);
    }

    protected override void UpdatePoints(Player player, Race race, string npcL10n, int points)
    {
        base.UpdatePoints(player, race, npcL10n, points);

        int diff = Math.Abs(instanceScore.GetAsmodiansPoints() - instanceScore.GetElyosPoints());
        if (diff >= 20000)
            OnStop(false);
    }

    public override void OnDie(Npc npc)
    {
        Player player = npc.GetAggroList().GetMostPlayerDamage();
        if (player == null)
        {
            return;
        }
        int points = 0;
        switch (npc.GetNpcId())
        {
            case 232856:
            case 232855:
            case 232852:
                points = 1250;
                break;
            case 701807:
            case 701808:
            case 701911:
            case 701912:
                points = 225;
                break;
            case 232847:
            case 232848:
            case 232849:
            case 232850:
            case 232851:
            case 233261:
                points = 140;
                break;
            case 233260:
            case 232841:
            case 232842:
            case 232843:
            case 232844:
            case 232845:
            case 232846:
                points = 50;
                break;
            case 801771:
                points = 75;
                break;
            case 232853:
                points = 3500;
                OnStop(true);
                break;
        }
        if (points > 0)
        {
            UpdatePoints(player, player.GetRace(), npc.GetObjectTemplate().GetL10n(), points);
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        if (player == null)
        {
            return;
        }
        int points = 0;
        switch (npc.GetNpcId())
        {
            case 801903:
                points = 1500;
                break;
            case 801772:
                points = 525;
                break;
            case 801766:
            case 801767:
            case 801818:
            case 801819:
            case 801820:
            case 801821:
                points = 255;
                break;
            case 730861:
            case 730878:
            case 730879:
            case 730880:
                UpdatePoints(player, player.GetRace(), npc.GetObjectTemplate().GetL10n(), 200);
                Spawn(player.GetRace() == Race.ELYOS ? 701900 : 701901, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                npc.GetController().Delete();
                break;
        }
        if (points > 0)
        {
            UpdatePoints(player, player.GetRace(), npc.GetObjectTemplate().GetL10n(), points);
            npc.GetController().Delete();
        }
    }

    private void OpenFirstDoors()
    {
        instance.SetDoorState(4, true);
        instance.SetDoorState(8, true);
        instance.SetDoorState(10, true);
        instance.SetDoorState(11, true);
    }

    protected override int GetReinforceMemberPhaseDelay()
    {
        return 120000;
    }

    public override void OnInstanceCreate()
    {
        instanceScore = new PvpInstanceScore<PvpInstancePlayerReward>(8750, 1750, 5250); // No info found for draws, so let's guess
        base.OnInstanceCreate();
    }

    public override void PortToStartPosition(Player player)
    {
        bool useAlternativePos = player.IsInAlliance() && player.GetPlayerAllianceGroup().GetObjectId() == 1001;
        if (player.GetRace() == Race.ELYOS && raceStartPosition == 0 || player.GetRace() == Race.ASMODIANS && raceStartPosition != 0)
        {
            if (useAlternativePos)
                Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), 1099.0986f, 1541.5055f, 585.0f);
            else
                Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), 1535.6466f, 1573.8773f, 612.4217f);
        }
        else
        {
            if (useAlternativePos)
                Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), 1446.6449f, 1232.9314f, 585.0623f);
            else
                Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), 1204.9689f, 1350.8196f, 612.91205f);
        }
    }

    public override void OnEnterZone(Player player, ZoneInstance zone)
    {
        if (zone.GetZoneTemplate().GetName() == ZoneName.Get("LAMINA_301120000"))
        {
            instance.SetDoorState(144, true); // crash airship
        }
        else if (zone.GetZoneTemplate().GetName() == ZoneName.Get("SPERO_301120000"))
        {
            instance.SetDoorState(5, true); // crash airship
        }
    }
}
