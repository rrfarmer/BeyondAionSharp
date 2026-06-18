using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/LegionsKysisBarracks (Estrayl, AION 4.8) : KysisBarracks. @InstanceID(301240000). 1:1.</summary>
[InstanceID(301240000)]
public class LegionsKysisBarracks : KysisBarracks
{
    public LegionsKysisBarracks(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetChestId()
    {
        return 702294;
    }
}
