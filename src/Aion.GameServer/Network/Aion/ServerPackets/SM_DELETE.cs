using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DELETE (-Nemesiss-, Neon). Tells the client an object is no longer visible, with an optional delete animation (NONE when out of range). ObjectDeleteAnimation/VisibleObject/AionServerPacket red-tolerated.</summary>
public class SM_DELETE : AionServerPacket
{
    /// <summary>Object that is no longer visible.</summary>
    private readonly int objectId;

    /// <summary>Animation seen before the object disappears.</summary>
    private readonly int animationId;

    public SM_DELETE(VisibleObject obj)
        : this(obj, ObjectDeleteAnimation.FADE_OUT, true)
    {
    }

    public SM_DELETE(VisibleObject obj, bool inRange)
        : this(obj, ObjectDeleteAnimation.FADE_OUT, inRange)
    {
    }

    public SM_DELETE(VisibleObject obj, ObjectDeleteAnimation animation)
        : this(obj, animation, true)
    {
    }

    private SM_DELETE(VisibleObject obj, ObjectDeleteAnimation animation, bool inRange)
    {
        this.objectId = obj.GetObjectId();
        this.animationId = inRange ? animation.GetId() : ObjectDeleteAnimation.NONE.GetId();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(objectId);
        WriteC(animationId);
    }
}
