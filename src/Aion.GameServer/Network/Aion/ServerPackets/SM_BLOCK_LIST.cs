using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_BLOCK_LIST (Ben). Sends the player's block list (name + reason per entry). Converges PlayerEnterWorldService. BlockList/BlockedPlayer/AionServerPacket red-tolerated.</summary>
public class SM_BLOCK_LIST : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        BlockList list = con.GetActivePlayer().GetBlockList();
        WriteH(-list.GetSize());
        WriteC(0); // Unk
        foreach (BlockedPlayer player in list)
        {
            WriteS(player.GetName());
            WriteS(player.GetReason());
        }
    }
}
