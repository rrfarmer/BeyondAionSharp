using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropZone.</summary>
[XmlType("GlobalDropZone")]
public class GlobalDropZone
{
    [XmlAttribute("zone")] public string Zone { get; set; } = string.Empty;
    public string GetZone() => Zone;
}
