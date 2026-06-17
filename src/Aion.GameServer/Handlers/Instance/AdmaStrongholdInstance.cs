using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/AdmaStrongholdInstance (xTz, Tibald) : GeneralInstanceHandler. @InstanceID(320130000); Rnd.nextBoolean()→Rnd.NextBoolean(), Rnd.get(1,3)→Rnd.Get(1,3); onInstanceCreate 1:1.</summary>
[InstanceID(320130000)]
public class AdmaStrongholdInstance : GeneralInstanceHandler
{
    public AdmaStrongholdInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        if (Rnd.NextBoolean())
        {
            switch (Rnd.Get(1, 3))
            {
                case 1:
                    Spawn(205224, 477.5849f, 398.20898f, 187.49918f, (byte)50);
                    break;
                case 2:
                    Spawn(205224, 347.76538f, 559.4453f, 180.33097f, (byte)105);
                    break;
                case 3:
                    Spawn(205224, 528.25116f, 555.0943f, 189.49f, (byte)78);
                    break;
            }
        }
    }
}
