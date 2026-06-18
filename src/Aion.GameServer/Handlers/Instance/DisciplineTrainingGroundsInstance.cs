using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/pvparenas/DisciplineTrainingGroundsInstance (xTz) : PvPArenaInstance. @InstanceID(300430000). 1:1.</summary>
[InstanceID(300430000)]
public class DisciplineTrainingGroundsInstance : PvPArenaInstance
{
    public DisciplineTrainingGroundsInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected override void SetScoreCaps()
    {
        instanceScore.SetLowerScoreCap(10000);
        instanceScore.SetUpperScoreCap(50000);
        instanceScore.SetMaxScoreGap(1500);
    }

    public override void OnInstanceCreate()
    {
        pointsPerKill = 200;
        pointsPerDeath = -100;
        base.OnInstanceCreate();
    }

    protected override int GetBoostMoraleEffectDuration(int rank)
    {
        return rank switch
        {
            0 => 14000,
            1 => 16000,
            _ => 15000,
        };
    }

    protected override void SendPacket(Player player, InstanceScoreType? scoreType)
    {
        instance.ForEachPlayer(
            p => PacketSendUtility.SendPacket(p, new SM_INSTANCE_SCORE(instance.GetMapId(), new ArenaScoreWriter(instanceScore, p.GetObjectId(), true))));
    }
}
