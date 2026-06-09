using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestItems (MrPoke).</summary>
[XmlType("QuestItems")]
public class QuestItems
{
    [XmlAttribute("item_id")] private int itemId;
    [XmlAttribute("count")] private long count = 1L;

    /// <summary>Constructor used by unmarshaller.</summary>
    public QuestItems()
    {
        this.count = 1L;
    }

    public QuestItems(int itemId, long count)
    {
        this.itemId = itemId;
        this.count = count;
    }

    /// <summary>Gets the value of the itemId property.</summary>
    public int GetItemId()
    {
        return itemId;
    }

    /// <summary>Gets the value of the count property.</summary>
    public long GetCount()
    {
        return count;
    }
}
