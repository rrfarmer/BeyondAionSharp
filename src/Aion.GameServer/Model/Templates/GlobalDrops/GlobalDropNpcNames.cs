using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropNpcNames.</summary>
[XmlType("GlobalDropNpcNames")]
public class GlobalDropNpcNames
{
    [XmlElement("gd_npc_name")] public List<GlobalDropNpcName>? GdNpcNames { get; set; }
    public List<GlobalDropNpcName> GetGlobalDropNpcNames() => GdNpcNames ??= [];
}
