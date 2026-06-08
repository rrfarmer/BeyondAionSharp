using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Assembled-item id reference.
/// Java parity: model/templates/item/AssembledItem (@XmlType("AssembledItem")).
/// </summary>
[XmlType("AssembledItem")]
public class AssembledItem
{
    [XmlAttribute("id")] public int Id { get; set; }

    public int GetId() => Id;
}
