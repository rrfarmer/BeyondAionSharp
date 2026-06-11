using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_SELF_INTRO (Simple). Updates a legion member's self-intro (objId + intro).</summary>
public class SM_LEGION_UPDATE_SELF_INTRO : AionServerPacket
{
    private string selfintro;
    private int playerObjId;

    public SM_LEGION_UPDATE_SELF_INTRO(int playerObjId, string selfintro)
    {
        this.selfintro = selfintro;
        this.playerObjId = playerObjId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteS(selfintro);
    }
}
