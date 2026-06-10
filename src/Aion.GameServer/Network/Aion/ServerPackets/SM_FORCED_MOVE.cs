using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FORCED_MOVE (Sweetkr). Forces a creature to a target position (used for anti-speedhack move-back). Converges AntiHackService. Creature/AionServerPacket red-tolerated.</summary>
public class SM_FORCED_MOVE : AionServerPacket
{
    private Creature creature;
    private int objectId;
    private float x;
    private float y;
    private float z;

    public SM_FORCED_MOVE(Creature creature, Creature target)
        : this(creature, target.GetObjectId(), target.GetX(), target.GetY(), target.GetZ())
    {
    }

    public SM_FORCED_MOVE(Creature creature, int objectId, float x, float y, float z)
    {
        this.creature = creature;
        this.objectId = objectId;
        this.x = x;
        this.y = y;
        this.z = z;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(creature.GetObjectId());
        WriteD(objectId);// targets objectId
        WriteC(16); // unk
        WriteF(x);
        WriteF(y);
        WriteF(z);
    }
}
