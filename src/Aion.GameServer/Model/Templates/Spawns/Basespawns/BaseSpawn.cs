using System.Xml.Serialization;
using Aion.GameServer.Model.Base;

namespace Aion.GameServer.Model.Templates.Spawns.Basespawns;

/// <summary>Java parity: model/templates/spawns/basespawns/BaseSpawn.</summary>
[XmlType("BaseSpawn")]
public class BaseSpawn
{
    [XmlAttribute("id")]    public int Id    { get; set; }
    [XmlAttribute("world")] public int World { get; set; }
    [XmlElement("occupier_template")] public List<BaseOccupierTemplate>? BaseOccupierTemplates { get; set; }

    public int GetId()      => Id;
    public int GetWorldId() => World;
    public List<BaseOccupierTemplate>? GetOccupierTemplates() => BaseOccupierTemplates;

    [XmlType("BaseOccupierTemplate")]
    public class BaseOccupierTemplate
    {
        [XmlAttribute("occupier")] public BaseOccupier Occupier { get; set; }
        [XmlElement("spawn")]      public List<Spawn>? Spawns   { get; set; }
        public BaseOccupier GetOccupier() => Occupier;
        public List<Spawn>? GetSpawns()   => Spawns;
    }
}
