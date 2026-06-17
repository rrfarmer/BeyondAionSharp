using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_OBJECTS (Rolandas). Bulk house object positions (template + x/y/z). HouseObject&lt;?&gt; erased to HouseObject&lt;PlaceableHouseObject&gt;. HouseObject red-tolerated.</summary>
// Java parity (writeImpl audited 1:1 vs game-server/.../SM_HOUSE_OBJECTS.java): 2026-06-17 — reads live HouseObject graph getObjectTemplate()/x/y/z (T2 audit-only).
public class SM_HOUSE_OBJECTS : AionServerPacket
{
    private readonly List<HouseObject> objects;

    public SM_HOUSE_OBJECTS(List<HouseObject> objects)
    {
        this.objects = objects;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(objects.Count);
        foreach (HouseObject obj in objects)
        {
            WriteD(obj.GetObjectTemplate().GetTemplateId());
            WriteF(obj.GetX());
            WriteF(obj.GetY());
            WriteF(obj.GetZ());
        }
    }
}
