using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_POSITION (Sweetkr). Instantly moves an object to a position (objId + x/y/z + heading), cancelling client-side movement. VisibleObject red-tolerated.</summary>
public class SM_POSITION : AionServerPacket
{
    private readonly VisibleObject obj;

    public SM_POSITION(VisibleObject obj)
    {
        this.obj = obj;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(obj.GetObjectId());
        WriteF(obj.GetX());
        WriteF(obj.GetY());
        WriteF(obj.GetZ());
        WriteC(obj.GetHeading());
    }
}
