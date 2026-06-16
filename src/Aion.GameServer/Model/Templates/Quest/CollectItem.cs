using System.Globalization;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/CollectItem (MrPoke).</summary>
[XmlType("CollectItem")]
public class CollectItem
{
    // Java parity: nullable Integer @XmlAttribute (absent -> null). XmlSerializer cannot bind Nullable<int>
    // to an attribute, so round-trip through string proxies (1:1 with JAXB: absent attribute -> null).
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

    public int? GetItemId()
    {
        return itemId;
    }

    public int? GetCount()
    {
        return count;
    }
}
