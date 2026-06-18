using System.Collections.Generic;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Flyring;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/pvparenas/HarmonyTrainingGroundsInstance (xTz) : PvPArenaInstance. @InstanceID(300570000). 1:1.</summary>
[InstanceID(300570000)]
public class HarmonyTrainingGroundsInstance : PvPArenaInstance
{
    public HarmonyTrainingGroundsInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetBoostMoraleEffectDuration(int rank)
    {
        return rank switch
        {
            0 => 14000,
            _ => 15000,
        };
    }

    protected override void SpawnRings()
    {
        SpawnFlyRing("PVP_ARENA_1", new Point3D(526.3772, 1800.0292, 176.74919), new Point3D(526.3772, 1800.0292, 179.74919),
            new Point3D(525.0156, 1797.356, 176.74919));
        SpawnFlyRing("PVP_ARENA_2", new Point3D(506.68332, 1801.1233, 176.29509), new Point3D(506.68332, 1801.1233, 179.29509),
            new Point3D(506.68332, 1798.1233, 176.29509));
        SpawnFlyRing("PVP_ARENA_3", new Point3D(537.7089, 1772.1154, 176.39908), new Point3D(537.7089, 1772.1154, 179.39908),
            new Point3D(534.8185, 1772.9186, 176.39908));
        SpawnFlyRing("PVP_ARENA_4", new Point3D(506.82697, 1761.2292, 176.5923), new Point3D(506.82697, 1761.2292, 179.5923),
            new Point3D(506.54703, 1764.2162, 176.5923));
        SpawnFlyRing("PVP_ARENA_5", new Point3D(526.93854, 1761.6971, 176.46439), new Point3D(526.93854, 1761.6971, 179.46439),
            new Point3D(525.2771, 1764.1951, 176.46439));
        SpawnFlyRing("PVP_ARENA_6", new Point3D(486.66653, 1761.2954, 176.52344), new Point3D(486.66653, 1761.2954, 179.52344),
            new Point3D(486.22333, 1764.2625, 176.52344));
        SpawnFlyRing("PVP_ARENA_7", new Point3D(463.7616, 1760.8594, 176.37796), new Point3D(463.7616, 1760.8594, 179.37796),
            new Point3D(465.9929, 1762.8647, 176.37796));
        SpawnFlyRing("PVP_ARENA_8", new Point3D(453.18936, 1775.0388, 176.37965), new Point3D(453.18936, 1775.0388, 179.37965),
            new Point3D(451.03714, 1772.9489, 176.37965));
        SpawnFlyRing("PVP_ARENA_9", new Point3D(453.36063, 1792.1831, 176.28424), new Point3D(453.36063, 1792.1831, 179.28424),
            new Point3D(450.8542, 1792.6652, 176.28424));
        SpawnFlyRing("PVP_ARENA_10", new Point3D(464.15717, 1801.1119, 176.49224), new Point3D(464.15717, 1801.1119, 179.4922),
            new Point3D(462.10913, 1803.3041, 176.49224));
        SpawnFlyRing("PVP_ARENA_11", new Point3D(486.1619, 1801.2965, 176.45914), new Point3D(486.1619, 1801.2965, 179.45914),
            new Point3D(485.39664, 1804.1973, 176.45914));
        SpawnFlyRing("PVP_ARENA_12", new Point3D(537.2858, 1789.3806, 176.13591), new Point3D(537.2858, 1789.3806, 179.13591),
            new Point3D(534.82043, 1787.6713, 176.13591));
        SpawnFlyRing("PVP_ARENA_13", new Point3D(519.96678, 1767.7379, 164.51219), new Point3D(519.96678, 1767.7379, 167.51219),
            new Point3D(518.04193, 1770.0391, 164.51219));
        SpawnFlyRing("PVP_ARENA_14", new Point3D(529.45605, 1780.3438, 152.91333), new Point3D(529.45605, 1780.3438, 155.91333),
            new Point3D(526.61993, 1779.3657, 152.91333));
        SpawnFlyRing("PVP_ARENA_15", new Point3D(520.3683, 1792.2515, 138.297), new Point3D(520.3683, 1792.2515, 141.297),
            new Point3D(520.271, 1789.253, 138.297));
        SpawnFlyRing("PVP_ARENA_16", new Point3D(469.53625, 1792.6782, 163.97906), new Point3D(469.53625, 1792.6782, 166.97906),
            new Point3D(467.26062, 1794.633, 163.97906));
        SpawnFlyRing("PVP_ARENA_17", new Point3D(459.51752, 1783.4421, 152.58247), new Point3D(459.51752, 1783.4421, 155.58247),
            new Point3D(456.87985, 1782.013, 152.58247));
        SpawnFlyRing("PVP_ARENA_18", new Point3D(469.3592, 1769.092, 137.91689), new Point3D(469.3592, 1769.092, 140.91689),
            new Point3D(470.37436, 1771.915, 137.91689));
        SpawnFlyRing("PVP_ARENA_19", new Point3D(494.79196, 1759.603, 151.13585), new Point3D(494.79196, 1759.603, 154.13585),
            new Point3D(494.51202, 1762.59, 151.13585));
        SpawnFlyRing("PVP_ARENA_20", new Point3D(503.06744, 1759.9893, 157.17598), new Point3D(503.06744, 1759.9893, 160.17598),
            new Point3D(502.7875, 1762.9762, 157.17598));
        SpawnFlyRing("PVP_ARENA_21", new Point3D(495.112, 1802.11895, 151.642), new Point3D(495.112, 1802.11895, 154.642),
            new Point3D(494.50687, 1805.1278, 151.642));
        SpawnFlyRing("PVP_ARENA_22", new Point3D(503.28552, 1801.8163, 142.87953), new Point3D(503.28552, 1801.8163, 145.87953),
            new Point3D(502.52026, 1804.717, 142.87953));
        SpawnFlyRing("PVP_ARENA_23", new Point3D(487.70947, 1802.1512, 157.30313), new Point3D(487.70947, 1802.1512, 160.30313),
            new Point3D(487.10434, 1805.0896, 157.30313));
        SpawnFlyRing("PVP_ARENA_24", new Point3D(486.5052, 1759.7152, 143.67566), new Point3D(486.5052, 1759.7152, 146.67566),
            new Point3D(485.90005, 1762.6536, 143.67566));
        SpawnFlyRing("PVP_ARENA_25", new Point3D(495.1142, 1769.1791, 151.02248), new Point3D(495.1142, 1769.1791, 154.02248),
            new Point3D(492.2781, 1768.201, 151.02248));
        SpawnFlyRing("PVP_ARENA_26", new Point3D(517.1142, 1780.5552, 151.54797), new Point3D(517.1142, 1780.5552, 154.54797),
            new Point3D(517.248, 1783.5222, 151.54797));
        SpawnFlyRing("PVP_ARENA_27", new Point3D(539.5928, 1768.1559, 153.20947), new Point3D(539.5928, 1768.1559, 156.20947),
            new Point3D(536.7507, 1769.1163, 153.20947));
        SpawnFlyRing("PVP_ARENA_28", new Point3D(494.78378, 1791.6841, 151.01425), new Point3D(494.78378, 1791.6841, 154.01425),
            new Point3D(497.77554, 1791.9062, 151.01425));
        SpawnFlyRing("PVP_ARENA_29", new Point3D(496.37292, 1781.1427, 151.12042), new Point3D(496.37292, 1781.1427, 154.12042),
            new Point3D(496.093, 1784.1296, 151.12042));
        SpawnFlyRing("PVP_ARENA_30", new Point3D(504.7736, 1781.0814, 151.68193), new Point3D(504.7736, 1781.0814, 154.68193),
            new Point3D(505.331, 1778.1337, 151.68193));
        SpawnFlyRing("PVP_ARENA_31", new Point3D(538.9759, 1791.9437, 153.94568), new Point3D(538.9759, 1791.9437, 156.94568),
            new Point3D(537.32996, 1789.4355, 153.94568));
        SpawnFlyRing("PVP_ARENA_32", new Point3D(486.21588, 1781.1268, 150.98273), new Point3D(486.21588, 1781.1268, 153.98273),
            new Point3D(486.6109, 1778, 150.98273));
        SpawnFlyRing("PVP_ARENA_33", new Point3D(472.12088, 1781.8315, 150.49611), new Point3D(472.12088, 1781.8315, 153.49611),
            new Point3D(472.839, 1778.9187, 150.49611));
        SpawnFlyRing("PVP_ARENA_34", new Point3D(496.78357, 1781.3253, 151.18472), new Point3D(496.78357, 1781.3253, 154.18472),
            new Point3D(496.64036, 1784.2924, 151.18472));
        SpawnFlyRing("PVP_ARENA_35", new Point3D(450.68466, 1771.1696, 153.57443), new Point3D(450.68466, 1771.1696, 156.57443),
            new Point3D(452.91595, 1771.1696, 153.57443));
        SpawnFlyRing("PVP_ARENA_36", new Point3D(452.1979, 1790.3647, 153.43126), new Point3D(452.1979, 1790.3647, 156.43126),
            new Point3D(449.41272, 1791.4795, 153.43126));
    }

    private void SpawnFlyRing(string name, Point3D center, Point3D p1, Point3D p2)
    {
        new FlyRing(new FlyRingTemplate(name, mapId, center, p1, p2, 3), instance.GetInstanceId()).Spawn();
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        if (!instanceScore.IsStartProgress())
            return false;

        Npc npc = flyingRing switch
        {
            "PVP_ARENA_1" => GetNpc(526.5524f, 1799.9530f, 177.3270f),
            "PVP_ARENA_2" => GetNpc(506.4008f, 1801.0159f, 177.3270f),
            "PVP_ARENA_3" => GetNpc(537.6169f, 1772.0968f, 177.3270f),
            "PVP_ARENA_4" => GetNpc(506.2996f, 1761.2419f, 177.3270f),
            "PVP_ARENA_5" => GetNpc(526.5186f, 1761.3792f, 177.3270f),
            "PVP_ARENA_6" => GetNpc(485.9503f, 1761.1323f, 177.3270f),
            "PVP_ARENA_7" => GetNpc(463.6774f, 1761.2948f, 177.3270f),
            "PVP_ARENA_8" => GetNpc(453.2310f, 1774.9258f, 177.3975f),
            "PVP_ARENA_9" => GetNpc(453.3799f, 1791.6423f, 177.3270f),
            "PVP_ARENA_10" => GetNpc(464.1622f, 1801.0581f, 177.3270f),
            "PVP_ARENA_11" => GetNpc(485.8056f, 1801.1987f, 177.3270f),
            "PVP_ARENA_12" => GetNpc(537.3194f, 1789.4381f, 177.3270f),
            "PVP_ARENA_13" => GetNpc(520.1588f, 1767.9170f, 165.3259f),
            "PVP_ARENA_14" => GetNpc(529.5792f, 1780.8058f, 153.6571f),
            "PVP_ARENA_15" => GetNpc(519.9453f, 1792.4106f, 139.4744f),
            "PVP_ARENA_16" => GetNpc(469.7408f, 1792.6573f, 165.4409f),
            "PVP_ARENA_17" => GetNpc(459.6954f, 1783.1649f, 153.1804f),
            "PVP_ARENA_18" => GetNpc(469.6530f, 1769.2192f, 138.8079f),
            "PVP_ARENA_19" => GetNpc(494.7642f, 1759.5282f, 152.5068f),
            "PVP_ARENA_20" => GetNpc(503.3351f, 1759.6985f, 158.4491f),
            "PVP_ARENA_21" => GetNpc(494.9348f, 1802.1798f, 152.5857f),
            "PVP_ARENA_22" => GetNpc(503.2661f, 1801.7520f, 143.9769f),
            "PVP_ARENA_23" => GetNpc(486.9509f, 1801.9956f, 158.4124f),
            "PVP_ARENA_24" => GetNpc(486.7321f, 1759.7345f, 144.8943f),
            "PVP_ARENA_25" => GetNpc(495.0289f, 1769.2734f, 152.1635f),
            "PVP_ARENA_26" => GetNpc(518.0588f, 1780.5404f, 152.5605f),
            "PVP_ARENA_27" => GetNpc(539.5499f, 1767.9496f, 154.2043f),
            "PVP_ARENA_28" => GetNpc(494.9674f, 1791.8362f, 152.1635f),
            "PVP_ARENA_29" => GetNpc(495.0894f, 1781.0751f, 152.1635f),
            "PVP_ARENA_30" => GetNpc(505.1635f, 1781.0885f, 152.3523f),
            "PVP_ARENA_31" => GetNpc(539.3393f, 1791.3956f, 154.9766f),
            "PVP_ARENA_32" => GetNpc(486.1324f, 1781.1040f, 152.0575f),
            "PVP_ARENA_33" => GetNpc(472.1582f, 1781.9821f, 151.6493f),
            "PVP_ARENA_34" => GetNpc(496.5575f, 1781.0718f, 152.1614f),
            "PVP_ARENA_35" => GetNpc(450.1526f, 1771.6342f, 154.4948f),
            "PVP_ARENA_36" => GetNpc(451.9798f, 1789.6945f, 154.4945f),
            _ => null,
        };
        if (npc != null && npc.IsSpawned())
        {
            npc.GetController().DeleteAndScheduleRespawn();
            HarmonyGroupReward reward = ((HarmonyArenaScore)instanceScore).GetGroupReward(player.GetObjectId());
            UpdatePoints(reward, player, npc, 100);
        }
        return false;
    }

    protected override void UpdatePoints(PvPArenaPlayerReward receiver, Player player, VisibleObject victim, int rewardPoints)
    {
        base.UpdatePoints(receiver, player, victim, rewardPoints);
    }

    public override void OnInstanceCreate()
    {
        pointsPerKill = 800;
        pointsPerDeath = -150;
        base.OnInstanceCreate();
    }

    protected override float GetRunnerUpScoreMod(int victimRank)
    {
        return 4f;
    }

    protected override PvPArenaScore CreateNewArenaScore()
    {
        return new HarmonyArenaScore(instance);
    }

    protected override ICollection<PvPArenaPlayerReward> GetArenaRewards()
    {
        return new List<PvPArenaPlayerReward>(((HarmonyArenaScore)instanceScore).GetHarmonyGroupInside());
    }

    protected override PvPArenaPlayerReward GetStatReward(Player player)
    {
        return ((HarmonyArenaScore)instanceScore).GetGroupReward(player.GetObjectId());
    }

    protected override bool CanStart()
    {
        return ((HarmonyArenaScore)instanceScore).GetHarmonyGroupInside().Count > 1;
    }

    protected override bool ShouldMergeGroupDamage()
    {
        return true;
    }

    protected override void SendEntryPacket(Player player)
    {
        BroadcastUpdate(player, InstanceScoreType.UPDATE_RANK);
        BroadcastUpdate(player, InstanceScoreType.INIT_PLAYER);
        BroadcastUpdate(player, InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS);
        BroadcastUpdate(InstanceScoreType.UPDATE_INSTANCE_BUFFS_AND_SCORE);
    }

    protected override void BroadcastUpdate(Player target, InstanceScoreType scoreType)
    {
        PacketSendUtility.BroadcastToMap(instance,
            new SM_INSTANCE_SCORE(instance.GetMapId(), new HarmonyScoreWriter((HarmonyArenaScore)instanceScore, scoreType, target), GetTime()));
    }

    protected override void BroadcastUpdate(InstanceScoreType scoreType)
    {
        PacketSendUtility.BroadcastToMap(instance,
            new SM_INSTANCE_SCORE(instance.GetMapId(), new HarmonyScoreWriter((HarmonyArenaScore)instanceScore, scoreType), GetTime()));
    }

    protected override void BroadcastResults()
    {
        instance.ForEachPlayer(player => SendPacket(player, InstanceScoreType.SHOW_REWARD));
    }

    protected override void SendPacket(Player receiver, InstanceScoreType? scoreType)
    {
        PacketSendUtility.SendPacket(receiver,
            new SM_INSTANCE_SCORE(instance.GetMapId(), new HarmonyScoreWriter((HarmonyArenaScore)instanceScore, scoreType.Value, receiver), GetTime()));
    }

    private int GetTime()
    {
        return instanceScore.GetTime();
    }
}
