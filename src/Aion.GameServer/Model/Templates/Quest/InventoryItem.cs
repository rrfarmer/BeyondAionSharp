using System.Globalization;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/InventoryItem (Rolandas).</summary>
[XmlType("InventoryItem")]
public class InventoryItem
{
    // Java parity: nullable Integer @XmlAttribute (absent -> null) via string proxies (XmlSerializer cannot
    // bind Nullable<int> to an attribute).
    private int? itemId;
    private int? count;

    [XmlAttribute("item_id")]
    public string ItemIdRaw
    {
        get => itemId?.ToString(CultureInfo.InvariantCulture);
        set => itemId = value == null ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("count")]
    public string CountRaw
    {
        get => count?.ToString(CultureInfo.InvariantCulture);
        set => count = value == null ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <summary>Gets the value of the itemId property.</summary>
    public int? GetItemId()
    {
        return itemId;
    }

    /// <returns>the count, or null if scripts should handle that the counts probably may depend on scenario</returns>
    public int? GetCount()
    {
        return count;
    }
}
