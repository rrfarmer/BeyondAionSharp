using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropMaps.</summary>
[XmlType("GlobalDropMaps")]
public class GlobalDropMaps
{
    [XmlElement("gd_map")] public List<GlobalDropMap>? GdMaps { get; set; }
    public List<GlobalDropMap> GetGlobalDropMaps() => GdMaps ??= [];
}
