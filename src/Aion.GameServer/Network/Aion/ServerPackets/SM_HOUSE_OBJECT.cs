using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_OBJECT (Rolandas). Spawns a single placed house object (template/pos/rotation/cooldown/expiration + type-specific use/npc data). HouseObject&lt;?&gt; erased to HouseObject&lt;PlaceableHouseObject&gt;; (UseableItemObject)/(NpcObject) casts; getBuf()->GetBuf(). HouseObject/NpcObject/UseableItemObject red-tolerated.</summary>
public class SM_HOUSE_OBJECT : AionServerPacket
{
    private HouseObject houseObject;

    public SM_HOUSE_OBJECT(HouseObject owner)
    {
        this.houseObject = owner;
    }

    protected override void WriteImpl(AionConnection con)
    {
        if (houseObject == null)
            return;
        Player player = con.GetActivePlayer();
        if (player == null)
            return;

        House house = houseObject.GetRegistry() == null ? null : houseObject.GetRegistry().GetOwner(); // may be null if it's a DummyHouseObject
        int templateId = houseObject.GetObjectTemplate().GetTemplateId();

        WriteD(house == null ? 0 : house.GetAddress().GetId()); // if painted 0 ?
        WriteD(house == null ? 0 : house.GetOwnerId()); // player which owns house
        WriteD(houseObject.GetObjectId()); // <outlet[X]> data in house scripts
        WriteD(houseObject.GetObjectId()); // <outDB[X]> data in house scripts (probably DB id), where [X] is number

        WriteD(templateId);
        WriteF(houseObject.GetX());
        WriteF(houseObject.GetY());
        WriteF(houseObject.GetZ());
        WriteH(houseObject.GetRotation());

        WriteD(player.GetHouseObjectCooldowns().RemainingSeconds(houseObject.GetObjectId()));
        WriteD(houseObject.SecondsUntilExpiration());

        WriteDyeInfo(houseObject == null ? null : houseObject.GetColor());
        WriteD(0); // expiration as for armor ?

        byte typeId = houseObject.GetObjectTemplate().GetTypeId();
        WriteC(typeId);

        switch (typeId)
        {
            case 1: // Use item
                ((UseableItemObject)houseObject).WriteUsageData(GetBuf());
                break;
            case 7: // Npc type
                NpcObject npcObj = (NpcObject)houseObject;
                WriteD(npcObj.GetNpcObjectId());
                break;
        }
    }
}
