using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/rakes/SteelRakeInstance (xTz) : GeneralInstanceHandler. @InstanceID(300100000); Rnd.chance()&lt;75 Pegureronerk vs Kaneron Agent; Rnd.get(int[]) special delivery spawn 1:1.</summary>
[InstanceID(300100000)]
public class SteelRakeInstance : GeneralInstanceHandler
{
    public SteelRakeInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        if (Rnd.Chance() < 75)
        { // Pegureronerk
            Spawn(798376, 516.198364f, 489.708008f, 885.760315f, (byte)60);
        }
        else
        { // Kaneron Agent
            Spawn(215041, 516.198f, 489.708f, 885.76f, (byte)60);
        }
        int specialDelivery = Rnd.Get(new int[] { 215074, 215075, 215076, 215077, 215054, 215055 });
        Spawn(specialDelivery, 461.933350f, 510.545654f, 877.618103f, (byte)90);
    }
}
