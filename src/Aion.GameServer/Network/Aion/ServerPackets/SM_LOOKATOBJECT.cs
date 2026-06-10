using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LOOKATOBJECT (alexa026). Object turns to face its target (objId + targetId + heading). VisibleObject red-tolerated.</summary>
public class SM_LOOKATOBJECT : AionServerPacket
{
    private VisibleObject visibleObject;
    private int targetObjectId;
    private int heading;

    public SM_LOOKATOBJECT(VisibleObject visibleObject)
    {
        this.visibleObject = visibleObject;
        this.targetObjectId = visibleObject.GetTarget() == null ? 0 : visibleObject.GetTarget().GetObjectId();
        this.heading = visibleObject.GetHeading();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(visibleObject.GetObjectId());
        WriteD(targetObjectId);
        WriteC(heading);
    }
}
