using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Model.Instance.Instanceposition;

/// <summary>Java parity: model/instance/instanceposition/InstancePositionHandler (xTz).</summary>
public interface InstancePositionHandler
{
    void Initialize(int mapId, int instanceId);

    void Port(Player player, int zone, int position);
}
