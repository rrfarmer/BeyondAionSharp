using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_REGISTRY (Rolandas). Lists not-spawned house objects (action 1) or default+unused decorations (action 2). HouseObject&lt;?&gt; erased to HouseObject&lt;PlaceableHouseObject&gt;; instanceof UseableItemObject->is+cast; getBuf()->GetBuf(). HouseRegistry/HouseObject/HouseDecoration red-tolerated.</summary>
// Java parity (writeImpl audited 1:1 vs game-server/.../SM_HOUSE_REGISTRY.java): 2026-06-17
public class SM_HOUSE_REGISTRY : AionServerPacket
{
    private readonly int action;

    public SM_HOUSE_REGISTRY(int action)
    {
        this.action = action;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player player = con.GetActivePlayer();
        if (player == null)
            return;
        HouseRegistry houseRegistry = player.GetActiveHouse().GetRegistry();

        WriteC(action);
        if (action == 1)
        { // Display registered objects
            if (houseRegistry == null)
            {
                WriteH(0);
                return;
            }
            WriteH(houseRegistry.GetNotSpawnedObjects().Count);
            foreach (HouseObject obj in houseRegistry.GetNotSpawnedObjects())
            {
                if (obj == null)
                    continue;
                WriteD(obj.GetObjectId());
                int templateId = obj.GetObjectTemplate().GetTemplateId();
                WriteD(templateId);
                WriteD(player.GetHouseObjectCooldowns().RemainingSeconds(obj.GetObjectId()));
                WriteD(obj.SecondsUntilExpiration());

                WriteDyeInfo(obj.GetColor());
                WriteD(0); // expiration as for armor ?

                WriteC(obj.GetObjectTemplate().GetTypeId());
                if (obj is UseableItemObject)
                {
                    ((UseableItemObject)obj).WriteUsageData(GetBuf());
                }
            }
        }
        else if (action == 2)
        { // Display default and registered decoration items
            List<int> defaultDecorIds = houseRegistry.GetOwner().GetBuilding().GetDefaultPartIds();
            List<HouseDecoration> unusedDecors = houseRegistry.GetUnusedDecors();
            WriteH(defaultDecorIds.Count + unusedDecors.Count);
            foreach (int defaultPartId in defaultDecorIds)
            {
                WriteD(0);
                WriteD(defaultPartId);
            }
            foreach (HouseDecoration houseDecor in unusedDecors)
            {
                WriteD(houseDecor.GetObjectId());
                WriteD(houseDecor.GetTemplateId());
            }
        }
    }
}
