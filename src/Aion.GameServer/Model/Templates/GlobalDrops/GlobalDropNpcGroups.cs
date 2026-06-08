using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropNpcGroups.</summary>
[XmlType("GlobalDropNpcGroups")]
public class GlobalDropNpcGroups
{
    [XmlElement("gd_npc_group")] public List<GlobalDropNpcGroup>? GdNpcGroups { get; set; }
    public List<GlobalDropNpcGroup> GetGlobalDropNpcGroups() => GdNpcGroups ??= [];
}
