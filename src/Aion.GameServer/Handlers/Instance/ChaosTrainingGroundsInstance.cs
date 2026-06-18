using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Flyring;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/pvparenas/ChaosTrainingGroundsInstance (xTz) : PvPArenaInstance. @InstanceID(300420000). 1:1.</summary>
[InstanceID(300420000)]
public class ChaosTrainingGroundsInstance : PvPArenaInstance
{
    public ChaosTrainingGroundsInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        pointsPerKill = 1000;
        pointsPerDeath = -125;
        base.OnInstanceCreate();
    }

    protected override int GetBoostMoraleEffectDuration(int rank)
    {
        return rank switch
        {
            0 or 1 => 14000,
            4 or 5 => 16000,
            6 or 7 => 17000,
            _ => 15000,
        };
    }

    protected override float GetRunnerUpScoreMod(int victimRank)
    {
        return victimRank switch
        {
            0 => 3f,
            1 => 2f,
            _ => 1f,
        };
    }

    protected override void SendPacket(Player player, InstanceScoreType? scoreType)
    {
        instance.ForEachPlayer(
            p => PacketSendUtility.SendPacket(p, new SM_INSTANCE_SCORE(instance.GetMapId(), new ArenaScoreWriter(instanceScore, p.GetObjectId(), false))));
    }

    public override void OnGather(Player player, Gatherable gatherable)
    {
        if (!instanceScore.IsStartProgress())
            return;
        UpdatePoints(GetPlayerSpecificReward(player), player, gatherable, 1250);
    }

    protected override void SpawnRings()
    {
        SpawnFlyRing("PVP_ARENA_1", new Point3D(674.66974, 1792.8499, 149.77501), new Point3D(674.66974, 1792.8499, 155.77501),
            new Point3D(678.83636, 1788.5325, 149.77501));
        SpawnFlyRing("PVP_ARENA_2", new Point3D(688.30615, 1769.7937, 149.88556), new Point3D(688.30615, 1769.7937, 155.88556),
            new Point3D(689.42096, 1763.8982, 149.88556));
        SpawnFlyRing("PVP_ARENA_3", new Point3D(664.2252, 1761.671, 170.95732), new Point3D(664.2252, 1761.671, 176.95732),
            new Point3D(669.2843, 1764.8967, 170.95732));
        SpawnFlyRing("PVP_ARENA_VOID_1", new Point3D(690.28625, 1753.8561, 192.07726), new Point3D(690.28625, 1753.8561, 198.07726),
            new Point3D(689.4365, 1747.9165, 192.07726));
        SpawnFlyRing("PVP_ARENA_VOID_2", new Point3D(690.1935, 1797.0029, 203.79236), new Point3D(690.1935, 1797.0029, 209.79236),
            new Point3D(692.8295, 1802.3928, 203.79236));
        SpawnFlyRing("PVP_ARENA_VOID_3", new Point3D(659.2784, 1766.0273, 207.25465), new Point3D(659.2784, 1766.0273, 213.25465),
            new Point3D(665.2619, 1766.4718, 207.25465));
    }

    private void SpawnFlyRing(string name, Point3D center, Point3D p1, Point3D p2)
    {
        new FlyRing(new FlyRingTemplate(name, mapId, center, p1, p2, 6), instance.GetInstanceId()).Spawn();
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        PvPArenaPlayerReward playerReward = GetPlayerSpecificReward(player);
        if (playerReward == null || !instanceScore.IsStartProgress())
        {
            return false;
        }
        Npc npc = flyingRing switch
        {
            "PVP_ARENA_1" => GetNpc(674.841f, 1793.065f, 150.964f), // Flame Loop
            "PVP_ARENA_2" => GetNpc(688.410f, 1769.611f, 150.964f), // Flame Loop
            "PVP_ARENA_3" => GetNpc(664.160f, 1761.933f, 171.504f), // Flame Loop
            "PVP_ARENA_VOID_1" => GetNpc(693.061f, 1752.479f, 186.750f), // Aether Vortex
            "PVP_ARENA_VOID_2" => GetNpc(688.061f, 1798.229f, 198.500f), // Aether Vortex
            "PVP_ARENA_VOID_3" => GetNpc(659.311f, 1768.979f, 201.500f), // Aether Vortex
            _ => null,
        };
        if (npc != null && npc.IsSpawned())
        {
            if (npc.GetNpcId() == 701184 || npc.GetNpcId() == 701198 || npc.GetNpcId() == 701212) // Flame Loop
            {
                npc.GetController().DeleteAndScheduleRespawn();
                UpdatePoints(playerReward, player, npc, 250);
            }
            else if (npc.GetNpcId() == 701183 || npc.GetNpcId() == 701197 || npc.GetNpcId() == 701211) // Aether Vortex
            {
                UseSkill(npc, player, 20059, 1);
                npc.GetController().DeleteAndScheduleRespawn();
            }
        }
        return false;
    }
}
