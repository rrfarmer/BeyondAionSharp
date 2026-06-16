using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Chest;

/// <summary>Java parity: model/templates/chest/KeyItem (Wakizashi).</summary>
[XmlType("KeyItem")]
public class KeyItem
{
    // Java parity: @XmlAttribute(name="item_ids") List<Integer> — space-separated.
    private List<int> itemIds;

    [XmlAttribute("item_ids")]
    public string ItemIdsRaw
    {
        get => itemIds == null ? null : string.Join(" ", itemIds);
        set => itemIds = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    // Public so XmlSerializer can bind this attribute (JAXB used a private field via @XmlAccessorType(FIELD)).
    [XmlAttribute("count")] public int count;

    public List<int> GetItemIds()
    {
        return itemIds;
    }

    public int GetCount()
    {
        return count;
    }
}
