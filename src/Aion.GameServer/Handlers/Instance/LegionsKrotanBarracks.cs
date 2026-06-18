using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/LegionsKrotanBarracks (Estrayl, AION 4.8) : KrotanBarracks. @InstanceID(301260000). 1:1.</summary>
[InstanceID(301260000)]
public class LegionsKrotanBarracks : KrotanBarracks
{
    public LegionsKrotanBarracks(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetChestId()
    {
        return 702290;
    }
}
