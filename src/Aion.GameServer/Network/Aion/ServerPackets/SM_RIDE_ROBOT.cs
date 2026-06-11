using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RIDE_ROBOT (Cheatkiller). Player mounts/dismounts a robot (objId + robotId). Player red-tolerated.</summary>
public class SM_RIDE_ROBOT : AionServerPacket
{
    private int robotId;
    private int objectId;

    public SM_RIDE_ROBOT(Player player)
        : this(player, player.GetRobotId())
    {
    }

    public SM_RIDE_ROBOT(Player player, int robotId)
    {
        this.objectId = player.GetObjectId();
        this.robotId = robotId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(objectId);
        WriteD(robotId);
    }
}
