using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Cost/source descriptor for acquiring an item (AP or currency item + count).
/// Java parity: model/templates/item/Acquisition (@XmlType("Acquisition")).
/// </summary>
[XmlType("Acquisition")]
public class Acquisition
{
    [XmlAttribute("ap")] public int Ap { get; set; }
    [XmlAttribute("count")] public int ItemCount { get; set; }
    [XmlAttribute("item")] public int ItemId { get; set; }
    [XmlAttribute("type")] public AcquisitionType Type { get; set; }

    // Java getType() — C# can't name a method GetType() (reserved by Object); use the Type property.
    public int GetItemId() => ItemId;
    public int GetItemCount() => ItemCount;
    // Java parity: getRequiredAp()
    public int GetRequiredAp() => Ap;
}
