using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GATHER_ANIMATION (orz, Yeats). Gathering animation (player/gatherable objIds + skillId + action).</summary>
public class SM_GATHER_ANIMATION : AionServerPacket
{
    private int playerObjId;
    private int gatherableObjId;
    private int skillId;
    private int action;

    public SM_GATHER_ANIMATION(int playerObjId, int gatherableObjId, int skillId, int action)
    {
        this.playerObjId = playerObjId;
        this.gatherableObjId = gatherableObjId;
        this.skillId = skillId;
        this.action = action;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteD(gatherableObjId);
        WriteH(skillId);
        WriteC(action);
    }
}
