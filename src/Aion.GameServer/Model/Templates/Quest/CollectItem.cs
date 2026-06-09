using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/CollectItem (MrPoke).</summary>
[XmlType("CollectItem")]
public class CollectItem
{
    [XmlAttribute("item_id")] protected int? itemId;
    [XmlAttribute("count")] protected int? count;

    public int? GetItemId()
    {
        return itemId;
    }

    public int? GetCount()
    {
        return count;
    }
}
