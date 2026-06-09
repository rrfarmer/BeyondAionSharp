using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/HousingNpc (Rolandas).</summary>
[XmlType("HousingNpc")]
public class HousingNpc : PlaceableHouseObject
{
    [XmlAttribute("npc_id")] protected int npcId;

    public int GetNpcId()
    {
        return npcId;
    }

    public override byte GetTypeId()
    {
        return 7;
    }
}
