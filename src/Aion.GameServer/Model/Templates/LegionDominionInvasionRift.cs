using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/LegionDominionInvasionRift (Sykra).</summary>
[XmlType("LegionDominionInvasionRift")]
public class LegionDominionInvasionRift
{
    [XmlAttribute("key_item_id")] private int keyItemId;
    [XmlAttribute("rift_id")] private int riftId;

    public int GetRiftId()
    {
        return riftId;
    }

    public int GetKeyItemId()
    {
        return keyItemId;
    }
}
