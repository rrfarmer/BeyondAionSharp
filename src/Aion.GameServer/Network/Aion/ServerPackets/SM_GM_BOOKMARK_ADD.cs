using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GM_BOOKMARK_ADD (Yeats). Adds a GM teleport bookmark (name + worldId + x/y/z).</summary>
public class SM_GM_BOOKMARK_ADD : AionServerPacket
{
    // //fsc 64 sdfffd [Teleportatiions Platz2] 120010000 230 250 290 1

    private string name;
    private int worldId;
    private float x, y, z;

    public SM_GM_BOOKMARK_ADD(string name, int worldId, float x, float y, float z)
    {
        this.name = name;
        this.worldId = worldId;
        this.x = x;
        this.y = y;
        this.z = z;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(name);
        WriteD(worldId);
        WriteF(x);
        WriteF(y);
        WriteF(z);
    }
}
