using System.Collections.Generic;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SHOW_DECOMPOSABLE (xTz).</summary>
public class SM_SHOW_DECOMPOSABLE : AionServerPacket
{
    private readonly ICollection<DecomposedItem> itemsCollections;
    private readonly int objectId;

    public SM_SHOW_DECOMPOSABLE(int objectId, ICollection<DecomposedItem> itemsCollections)
    {
        this.itemsCollections = itemsCollections;
        this.objectId = objectId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteQ(objectId); // 64 bit object id
        WriteC(itemsCollections.Count);
        int index = 0;
        foreach (DecomposedItem item in itemsCollections)
        {
            WriteC(index);
            WriteD(item.GetItemId());
            WriteD(item.GetCount());
            // properties of the reward the client shows in the window, all of them zero because a decomposed item is created plain
            WriteC(0); // random bonus
            WriteC(0); // enchant level
            WriteC(0); // appraisal state
            WriteC(1); // constant, the client stops reading the record after it
            index++;
        }
    }
}
