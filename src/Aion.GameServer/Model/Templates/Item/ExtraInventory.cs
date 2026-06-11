using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// Extra-inventory id reference.
/// Java parity: model/templates/item/ExtraInventory (@XmlType("ExtraInventory")).
/// </summary>
[XmlType("ExtraInventory")]
public class ExtraInventory
{
    [XmlAttribute("id")] public int Id { get; set; }

    public int GetId() => Id;
}
