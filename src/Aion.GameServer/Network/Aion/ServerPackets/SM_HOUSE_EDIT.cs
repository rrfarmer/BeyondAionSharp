using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_EDIT (Rolandas). House decorate edit (add 3 / remove 4 / spawn-move 5 / despawn 7). HouseObject&lt;?&gt; erased to HouseObject&lt;PlaceableHouseObject&gt;; instanceof UseableItemObject->is+cast; getBuf()->GetBuf(); LoggerFactory->NullLogger. HouseObject/UseableItemObject/HouseDecoration red-tolerated.</summary>
// Java parity (writeImpl audited 1:1 vs game-server/.../SM_HOUSE_EDIT.java): 2026-06-17
public class SM_HOUSE_EDIT : AionServerPacket
{
    private int action;
    private int storeId;
    private int itemObjectId;
    private float x, y, z;
    private int rotation;

    public SM_HOUSE_EDIT(int action)
    {
        this.action = action;
    }

    public SM_HOUSE_EDIT(int action, int storeId, int itemObjectId)
        : this(action)
    {
        this.itemObjectId = itemObjectId;
        this.storeId = storeId;
    }

    public SM_HOUSE_EDIT(int action, int itemObjectId, float x, float y, float z, int rotation)
    {
        this.action = action;
        this.itemObjectId = itemObjectId;
        this.x = x;
        this.y = y;
        this.z = z;
        this.rotation = rotation;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player player = con.GetActivePlayer();
        if (player == null)
            return;
        House house = player.GetActiveHouse();
        HouseObject obj = house.GetRegistry().GetObjectByObjId(itemObjectId);

        if (action == 3)
        { // Add item
            int templateId;
            int typeId = 0;
            if (obj == null)
            {
                HouseDecoration deco = house.GetRegistry().GetDecorByObjId(itemObjectId);
                if (deco == null)
                {
                    NullLoggerFactory.Instance.CreateLogger(GetType().FullName).LogWarning("House item with object ID " + itemObjectId + " wasn't found in registry of " + house);
                    return;
                }
                templateId = deco.GetTemplateId();
            }
            else
            {
                templateId = obj.GetObjectTemplate().GetTemplateId();
                typeId = obj.GetObjectTemplate().GetTypeId();
            }
            WriteC(action);
            WriteC(storeId);
            WriteD(itemObjectId);
            WriteD(templateId);
            WriteD(obj == null ? 0 : obj.SecondsUntilExpiration());

            WriteDyeInfo(obj == null ? null : obj.GetColor());
            WriteD(0); // expiration as for armor ?

            WriteC(typeId);
            // Additional info about the usage
            if (obj is UseableItemObject)
            {
                WriteD(player.GetObjectId());
                ((UseableItemObject)obj).WriteUsageData(GetBuf());
            }
        }
        else if (action == 4)
        { // Remove from inventory
            WriteC(action);
            WriteC(storeId);
            WriteD(itemObjectId);
        }
        else if (action == 5)
        { // Spawn or move object
            WriteC(action);
            WriteD(house.GetAddress().GetId()); // if painted 0 ?
            WriteD(player.GetCommonData().GetPlayerObjId());
            WriteD(itemObjectId);
            WriteD(obj.GetObjectTemplate().GetTemplateId());
            WriteF(x);
            WriteF(y);
            WriteF(z);
            WriteH(rotation);
            WriteD(player.GetHouseObjectCooldowns().RemainingSeconds(itemObjectId));
            WriteD(obj.SecondsUntilExpiration());
            WriteDyeInfo(obj.GetColor());
            WriteD(0); // expiration as for armor ?

            WriteC(obj.GetObjectTemplate().GetTypeId());

            if (obj is UseableItemObject)
            {
                ((UseableItemObject)obj).WriteUsageData(GetBuf());
            }
        }
        else if (action == 7)
        { // Despawn object
            WriteC(action);
            WriteD(itemObjectId);
        }
        else
            WriteC(action);
    }
}
