using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ACCOUNT_PROPERTIES (pixfid, Rolandas, Yeats, Neon). Account flags incl. GM panel enable; mostly fixed/zero fields + account status. AdminConfig red-tolerated.</summary>
public class SM_ACCOUNT_PROPERTIES : AionServerPacket
{
    // Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_ACCOUNT_PROPERTIES.java): 2026-06-17. Reads con.getAccount().getAccessLevel(); rest fixed/zero fields.
    public SM_ACCOUNT_PROPERTIES()
    {
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(con.GetAccount().GetAccessLevel() >= AdminConfig.GM_PANEL ? 1 : 0); // enables GM panel and other windows, also disables client-side faction restriction for char creation
        WriteD(0); // always 0
        WriteH(0); // 0 or 52168 (C8 CB)
        WriteC(0); // 0 or 1
        WriteH(0); // 0, 4 or 5
        WriteC(0); // 0, 4 or 124
        WriteH(0); // can be 1
        WriteD(0); // 0 or 16 (or 31 = strong energy of repose)
        WriteD(0); // always 0
        WriteD(0); // purchased packet (8 = gold pack)
        WriteD(4); // account status (0 = gold-user, 1/2 = starter, 3/4 = veteran)
    }
}
