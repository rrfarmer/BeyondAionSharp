using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropTribes.</summary>
[XmlType("GlobalDropTribes")]
public class GlobalDropTribes
{
    [XmlElement("gd_tribe")] public List<GlobalDropTribe>? GdTribes { get; set; }
    public List<GlobalDropTribe> GetGlobalDropTribes() => GdTribes ??= [];
}
