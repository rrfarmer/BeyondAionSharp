using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/DanuarReliquaryInstance_L (Cheatkiller, Estrayl) : DanuarReliquaryInstance. @InstanceID(301330000); overrides getExitId/getTreasureBoxId. 1:1.</summary>
[InstanceID(301330000)]
public class DanuarReliquaryInstance_L : DanuarReliquaryInstance
{
    public DanuarReliquaryInstance_L(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetExitId()
    {
        return 730843;
    }

    protected override int GetTreasureBoxId()
    {
        return 802183;
    }
}
