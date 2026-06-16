using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/LegionDominionReward (Yeats, Sykra).</summary>
[XmlType("LegionDominionReward")]
public class LegionDominionReward
{
    // Public so XmlSerializer can populate them (JAXB used protected fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("rank")] public int rank;
    [XmlAttribute("item_id")] public int itemId;
    [XmlAttribute("count")] public int count;

    public int GetRank()
    {
        return rank;
    }

    public int GetItemId()
    {
        return itemId;
    }

    public int GetCount()
    {
        return count;
    }
}
