using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/rakes/SteelRakeCabineInstance (xTz) : GeneralInstanceHandler. @InstanceID(300460000); Rnd.get(1,2) Sweeper Nunukin pos + Madame Bovariki/Steel Rake Shaman swap spawns 1:1.</summary>
[InstanceID(300460000)]
public class SteelRakeCabineInstance : GeneralInstanceHandler
{
    public SteelRakeCabineInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        if (Rnd.Get(1, 2) == 1)
        { // Sweeper Nunukin
            Spawn(219026, 353.814f, 491.557f, 949.466f, (byte)119);
        }
        else
        {
            Spawn(219026, 354.7875f, 536.17004f, 949.4662f, (byte)7);
        }
        int chance = Rnd.Get(1, 2);
        // Madame Bovariki + Steel Rake Shaman
        Spawn(chance == 1 ? 219032 : 219003, 463.124f, 512.75f, 952.545f, (byte)0);
        Spawn(chance == 1 ? 219003 : 219032, 502.859f, 548.55f, 952.417f, (byte)85);
    }
}
