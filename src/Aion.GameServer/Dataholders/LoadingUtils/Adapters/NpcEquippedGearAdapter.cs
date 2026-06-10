using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Dataholders.Loadingutils.Adapters;

/// <summary>Java parity: dataholders/loadingutils/adapters/NpcEquippedGearAdapter (Luno). JAXB XmlAdapter&lt;NpcEquipmentList,NpcEquippedGear&gt;→plain class (no C# base, per SpaceSeparatedBytesAdapter idiom); throws Exception dropped. NpcEquipmentList/NpcEquippedGear red-tolerated.</summary>
public class NpcEquippedGearAdapter
{
    public NpcEquipmentList Marshal(NpcEquippedGear v)
    {
        // TODO Auto-generated method stub
        return null;
    }

    public NpcEquippedGear Unmarshal(NpcEquipmentList v)
    {
        return new NpcEquippedGear(v);
    }
}
