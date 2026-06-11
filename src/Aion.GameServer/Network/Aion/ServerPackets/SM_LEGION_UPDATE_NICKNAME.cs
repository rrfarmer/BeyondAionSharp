using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_NICKNAME (Simple). Updates a legion member's self-nickname (objId + nickname).</summary>
public class SM_LEGION_UPDATE_NICKNAME : AionServerPacket
{
    private int playerObjId;
    private string newNickname;

    public SM_LEGION_UPDATE_NICKNAME(int playerObjId, string newNickname)
    {
        this.playerObjId = playerObjId;
        this.newNickname = newNickname;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteS(newNickname);
    }
}
