using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropNpcs.</summary>
[XmlType("GlobalDropNpcs")]
public class GlobalDropNpcs
{
    [XmlElement("gd_npc")] public List<GlobalDropNpc>? GdNpcs { get; set; }
    public List<GlobalDropNpc> GetGlobalDropNpcs() => GdNpcs ??= [];
    public void AddNpcs(List<GlobalDropNpc> value) => GdNpcs = value;
}
