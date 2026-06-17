using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/FireTempleInstance (Gigi) : GeneralInstanceHandler. @InstanceID(320100000); Rnd.chance()→Rnd.Chance(); onInstanceCreate random boss/elite spawns 1:1.</summary>
[InstanceID(320100000)]
public class FireTempleInstance : GeneralInstanceHandler
{
    public FireTempleInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        // Random spawns of bosses
        if (Rnd.Chance() < 75) // Blue Crystal Molgat
        {
            Spawn(212839, 127.1218f, 176.1912f, 99.67548f, (byte)15);
        }
        else // elite mob spawns
        {
            Spawn(212790, 127.1218f, 176.1912f, 99.67548f, (byte)15);
        }

        if (Rnd.Chance() < 75) // Black Smoke Asparn
        {
            Spawn(212842, 322.3193f, 431.2696f, 134.5296f, (byte)80);
        }
        else // elite mob spawns
        {
            Spawn(212799, 322.3193f, 431.2696f, 134.5296f, (byte)80);
        }

        if (Rnd.Chance() < 75) // Lava Gatneri
        {
            Spawn(212840, 153.0038f, 299.7786f, 123.0186f, (byte)30);
        }
        else // elite mob spawns
        {
            Spawn(212794, 153.0038f, 299.7786f, 123.0186f, (byte)30);
        }

        if (Rnd.Chance() < 75) // Tough Sipus
        {
            Spawn(212843, 296.6911f, 201.9092f, 119.3652f, (byte)30);
        }
        else // elite mob spawns
        {
            Spawn(212803, 296.6911f, 201.9092f, 119.3652f, (byte)15);
        }

        if (Rnd.Chance() < 75) // Flame Branch Flavi
        {
            Spawn(212841, 350.9276f, 351.7389f, 146.8498f, (byte)45);
        }
        else // elite mob spawns
        {
            Spawn(212799, 350.9276f, 351.7389f, 146.8498f, (byte)45);
        }

        if (Rnd.Chance() < 75) // Broken Wing Kutisen
        {
            Spawn(212845, 298.7095f, 89.42245f, 128.7143f, (byte)15);
        }
        else // elite mob spawns
        {
            Spawn(214094, 298.7095f, 89.42245f, 128.7143f, (byte)15);
        }

        if (Rnd.Chance() < 10) // stronger kromede
        {
            Spawn(214621, 421.9935f, 93.18915f, 117.3053f, (byte)46);
        }
        else // normal kromede
        {
            Spawn(212846, 421.9935f, 93.18915f, 117.3053f, (byte)46);
        }
    }
}
