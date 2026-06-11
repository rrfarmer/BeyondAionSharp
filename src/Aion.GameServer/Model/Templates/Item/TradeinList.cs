using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// List of tradein entries.
/// Java parity: model/templates/item/TradeinList (@XmlType("TradeinList")).
/// </summary>
[XmlType("TradeinList")]
public class TradeinList
{
    [XmlElement("tradein_item")] public List<TradeinItem>? TradeinItem { get; set; }

    public List<TradeinItem>? GetTradeinItem() => TradeinItem;
}
