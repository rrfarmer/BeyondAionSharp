using System.Collections.Generic;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SECONDARY_SHOW_DECOMPOSABLE (xTz). Secondary decomposition-result preview (objId + indexed ResultedItem list). ResultedItem red-tolerated.</summary>
public class SM_SECONDARY_SHOW_DECOMPOSABLE : AionServerPacket
{
    private ICollection<ResultedItem> itemsCollections;
    private int objectId;

    public SM_SECONDARY_SHOW_DECOMPOSABLE(int objectId, ICollection<ResultedItem> itemsCollections)
    {
        this.itemsCollections = itemsCollections;
        this.objectId = objectId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(objectId);
        WriteD(0);
        WriteC(itemsCollections.Count);
        int index = 0;
        foreach (ResultedItem item in itemsCollections)
        {
            WriteC(index);
            WriteD(item.GetItemId());
            WriteD(item.GetMinCount());
            WriteC(0);
            WriteC(0);
            WriteC(0);
            WriteC(1);
            index++;
        }
    }
}
