using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MARK_FRIENDLIST (xTz). Marks the friend list (active player objId + flags).</summary>
public class SM_MARK_FRIENDLIST : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteD(con.GetActivePlayer().GetObjectId());
        WriteC(1);
        WriteH(0);
    }
}
