using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PRIVATE_STORE_NAME (Simple). Sends a player's private-store message (objId + name). Player red-tolerated.</summary>
public class SM_PRIVATE_STORE_NAME : AionServerPacket
{
    private int playerObjId;
    private string name;

    public SM_PRIVATE_STORE_NAME(Player player)
    {
        this.playerObjId = player.GetObjectId();
        this.name = player.GetStore().GetStoreMessage();
    }

    protected override void WriteImpl(AionConnection con)
    {
        // Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_PRIVATE_STORE_NAME.java): 2026-06-17. ctor reads live Player.getStore() graph.
        WriteD(playerObjId);
        WriteS(name);
    }
}
