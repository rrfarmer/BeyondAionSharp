using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TELEPORT_LOC (Luno, orz, xTz). Teleports the player with a port animation. DataManager/TeleportAnimation red-tolerated.</summary>
public class SM_TELEPORT_LOC : AionServerPacket
{
    private byte portAnimation;
    private int mapId;
    private int instanceId;
    private float x, y, z;
    private byte heading;
    private bool isInstance;

    public SM_TELEPORT_LOC(int mapId, int instanceId, float x, float y, float z, byte heading, TeleportAnimation portAnimation)
    {
        this.isInstance = DataManager.WORLD_MAPS_DATA.GetTemplate(mapId).IsInstance();
        this.instanceId = instanceId;
        this.mapId = mapId;
        this.x = x;
        this.y = y;
        this.z = z;
        this.heading = heading;
        this.portAnimation = portAnimation.GetId();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(portAnimation);
        WriteD(mapId);// new 4.3 NA -->old //writeH(mapId & 0xFFFF);
        WriteD(isInstance ? instanceId : mapId); // mapId | instanceId
        WriteF(x);
        WriteF(y);
        WriteF(z);
        WriteC(heading);
    }
}
