using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropMap.</summary>
[XmlType("GlobalDropMap")]
public class GlobalDropMap
{
    [XmlAttribute("map_id")] public int MapId { get; set; }
    public int GetMapId() => MapId;
}
