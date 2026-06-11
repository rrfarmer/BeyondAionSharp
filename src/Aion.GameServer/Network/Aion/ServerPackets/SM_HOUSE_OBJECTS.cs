using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_OBJECTS (Rolandas). Bulk house object positions (template + x/y/z). HouseObject&lt;?&gt; erased to HouseObject&lt;PlaceableHouseObject&gt;. HouseObject red-tolerated.</summary>
public class SM_HOUSE_OBJECTS : AionServerPacket
{
    private readonly List<HouseObject<PlaceableHouseObject>> objects;

    public SM_HOUSE_OBJECTS(List<HouseObject<PlaceableHouseObject>> objects)
    {
        this.objects = objects;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(objects.Count);
        foreach (HouseObject<PlaceableHouseObject> obj in objects)
        {
            WriteD(obj.GetObjectTemplate().GetTemplateId());
            WriteF(obj.GetX());
            WriteF(obj.GetY());
            WriteF(obj.GetZ());
        }
    }
}
