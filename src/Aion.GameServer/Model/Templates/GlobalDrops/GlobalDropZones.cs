using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropZones.</summary>
[XmlType("GlobalDropZones")]
public class GlobalDropZones
{
    [XmlElement("gd_zone")] public List<GlobalDropZone>? GdZones { get; set; }
    public List<GlobalDropZone> GetGlobalDropZones() => GdZones ??= [];
}
