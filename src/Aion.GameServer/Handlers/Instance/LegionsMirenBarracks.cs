using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/LegionsMirenBarracks (Estrayl, AION 4.8) : MirenBarracks. @InstanceID(301250000). 1:1.</summary>
[InstanceID(301250000)]
public class LegionsMirenBarracks : MirenBarracks
{
    public LegionsMirenBarracks(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetChestId()
    {
        return 702298;
    }
}
