using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/LegionDominionReward (Yeats, Sykra).</summary>
[XmlType("LegionDominionReward")]
public class LegionDominionReward
{
    [XmlAttribute("rank")] protected int rank;
    [XmlAttribute("item_id")] protected int itemId;
    [XmlAttribute("count")] protected int count;

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
