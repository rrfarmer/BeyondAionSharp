using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Services;

namespace Aion.GameServer.Model.Instance.Instanceposition;

/// <summary>Java parity: model/instance/instanceposition/GeneralInstancePosition.</summary>
public abstract class GeneralInstancePosition : InstancePositionHandler
{
    protected int mapId;
    protected int instanceId;

    public void Initialize(int mapId, int instanceId)
    {
        this.mapId = mapId;
        this.instanceId = instanceId;
    }

    protected void Teleport(Player player, float x, float y, float z, sbyte h)
    {
        TeleportService.TeleportTo(player, mapId, instanceId, x, y, z, h);
    }

    public abstract void Port(Player player, int zone, int position);
}
