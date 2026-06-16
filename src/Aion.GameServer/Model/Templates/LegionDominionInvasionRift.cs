using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/LegionDominionInvasionRift (Sykra).</summary>
[XmlType("LegionDominionInvasionRift")]
public class LegionDominionInvasionRift
{
    // Public so XmlSerializer can populate them (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("key_item_id")] public int keyItemId;
    [XmlAttribute("rift_id")] public int riftId;

    public int GetRiftId()
    {
        return riftId;
    }

    public int GetKeyItemId()
    {
        return keyItemId;
    }
}
