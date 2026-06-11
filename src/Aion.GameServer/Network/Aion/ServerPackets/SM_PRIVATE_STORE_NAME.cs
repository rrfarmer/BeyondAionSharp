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
        WriteD(playerObjId);
        WriteS(name);
    }
}
