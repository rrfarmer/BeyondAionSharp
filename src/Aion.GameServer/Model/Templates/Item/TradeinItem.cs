using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// One tradein entry (item id + price).
/// Java parity: model/templates/item/TradeinItem (@XmlType("TradeinItem")).
/// </summary>
[XmlType("TradeinItem")]
public class TradeinItem
{
    [XmlAttribute("id")] public int Id { get; set; }
    [XmlAttribute("price")] public long Price { get; set; }

    public int GetId() => Id;
    public long GetPrice() => Price;

    public override string ToString() => "TradeinItem [id=" + Id + ", price=" + Price + "]";
}
