using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_OBJECT_USE_UPDATE (Rolandas). Updates a usable house object (postbox/storage/useable-item). HouseObject&lt;?&gt; erased to HouseObject&lt;PlaceableHouseObject&gt;; instanceof->is + cast; getCheckType() nullable Integer->int?. HouseObject/UseableItemObject/UseItemAction red-tolerated.</summary>
public class SM_OBJECT_USE_UPDATE : AionServerPacket
{
    private int usingPlayerId;
    private int ownerPlayerId;
    private int useCount;
    private UseItemAction action = null;
    private HouseObject<PlaceableHouseObject> obj;

    public SM_OBJECT_USE_UPDATE(int usingPlayerId, int ownerPlayerId, int useCount, HouseObject<PlaceableHouseObject> obj)
    {
        this.usingPlayerId = usingPlayerId;
        this.ownerPlayerId = ownerPlayerId;
        this.useCount = useCount;
        this.obj = obj;
        if (obj is UseableItemObject)
            this.action = ((UseableItemObject)obj).GetObjectTemplate().GetAction();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(obj.GetObjectTemplate().GetTypeId());
        if (obj is PostboxObject || obj is StorageObject)
        {
            WriteD(usingPlayerId);
            WriteC(1); // unk
            WriteD(obj.GetObjectId());
        }
        else if (obj is UseableItemObject)
        {
            WriteD(usingPlayerId);
            WriteD(ownerPlayerId);
            WriteD(obj.GetObjectId());
            WriteD(useCount);
            int checkType = 0;
            if (action != null && action.GetCheckType() != null)
                checkType = action.GetCheckType().Value;
            WriteC(checkType);
        }
    }
}
