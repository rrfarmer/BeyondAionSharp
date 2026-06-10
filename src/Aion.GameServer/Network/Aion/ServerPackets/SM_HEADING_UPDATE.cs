using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HEADING_UPDATE (Nemesiss). Updates a visible object's heading (objId + heading). VisibleObject red-tolerated.</summary>
public class SM_HEADING_UPDATE : AionServerPacket
{
    private int objectId;
    private byte heading;

    public SM_HEADING_UPDATE(VisibleObject target)
    {
        this.objectId = target.GetObjectId();
        this.heading = target.GetHeading();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(objectId);
        WriteC(heading);
    }
}
