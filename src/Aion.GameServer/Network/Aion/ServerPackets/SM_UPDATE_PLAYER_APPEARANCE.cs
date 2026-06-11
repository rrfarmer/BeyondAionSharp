using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_UPDATE_PLAYER_APPEARANCE (Avol, ATracer, Neon). Updates a player's equipped appearance via AbstractPlayerInfoPacket.WriteEquippedItems. Item red-tolerated.</summary>
public class SM_UPDATE_PLAYER_APPEARANCE : AbstractPlayerInfoPacket
{
    private int playerId;
    private List<Item> items;

    public SM_UPDATE_PLAYER_APPEARANCE(int playerId, List<Item> items)
    {
        this.playerId = playerId;
        this.items = items;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerId);
        WriteEquippedItems(items);
    }
}
