using Aion.GameServer.Network.Aion;
using ItemDeleteType = Aion.GameServer.Services.Item.ItemPacketService.ItemDeleteType;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DELETE_WAREHOUSE_ITEM (kosyachok). Removes a warehouse item from the client view (warehouse type + objId + delete mask). ItemDeleteType aliased from ItemPacketService (red-tolerated).</summary>
public class SM_DELETE_WAREHOUSE_ITEM : AionServerPacket
{
    private int warehouseType;
    private int itemObjId;
    private ItemDeleteType deleteType;

    public SM_DELETE_WAREHOUSE_ITEM(int warehouseType, int itemObjId, ItemDeleteType deleteType)
    {
        this.warehouseType = warehouseType;
        this.itemObjId = itemObjId;
        this.deleteType = deleteType;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(warehouseType);
        WriteD(itemObjId);
        WriteC(deleteType.GetMask());
    }
}
