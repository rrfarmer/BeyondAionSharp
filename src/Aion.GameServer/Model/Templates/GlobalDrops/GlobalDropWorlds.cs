using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropWorlds.</summary>
[XmlType("GlobalDropWorlds")]
public class GlobalDropWorlds
{
    [XmlElement("gd_world")] public List<GlobalDropWorld>? GdWorlds { get; set; }
    public List<GlobalDropWorld> GetGlobalDropWorlds() => GdWorlds ??= [];
}
