using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SHOW_NPC_ON_MAP (Lyahim). Marks an npc on the map (npc/world/instance + x/y/z). Player red-tolerated.</summary>
public class SM_SHOW_NPC_ON_MAP : AionServerPacket
{
    private Player player;
    private int npcid, worldid;
    private float x, y, z;

    public SM_SHOW_NPC_ON_MAP(Player player, int npcid, int worldid, float x, float y, float z)
    {
        this.player = player;
        this.npcid = npcid;
        this.worldid = worldid;
        this.x = x;
        this.y = y;
        this.z = z;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(this.npcid);
        WriteD(this.worldid);
        // default value: mapid + channelId(0)
        int instanceId = this.worldid;
        if (this.player.GetPosition().GetMapId() == this.worldid)
        {
            if (this.player.IsInInstance())
                instanceId = this.player.GetInstanceId();
            else
                instanceId = this.worldid + this.player.GetInstanceId() - 1; // mapid + channelId (instanceId-1)
        }
        WriteD(instanceId);

        WriteF(this.x);
        WriteF(this.y);
        WriteF(this.z);
    }
}
