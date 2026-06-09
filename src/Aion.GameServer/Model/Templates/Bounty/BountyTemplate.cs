using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Bounty;

/// <summary>Java parity: model/templates/bounty/BountyTemplate (Estrayl).</summary>
[XmlType("Bounty")]
public class BountyTemplate
{
    [XmlAttribute("item_id")] private int itemId;
    [XmlAttribute("count")] private int count;

    public int GetItemId()
    {
        return itemId;
    }

    public int GetCount()
    {
        return count;
    }
}
