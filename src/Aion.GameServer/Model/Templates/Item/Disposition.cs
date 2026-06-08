using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Disposition (required item id + count) descriptor.
/// Java parity: model/templates/item/Disposition (@XmlType("Disposition")).
/// </summary>
[XmlType("Disposition")]
public class Disposition
{
    [XmlAttribute("count")] public int Count { get; set; }
    [XmlAttribute("id")] public int Id { get; set; }

    public int GetCount() => Count;
    public int GetId() => Id;
}
