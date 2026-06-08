using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Spawns.Riftspawns;

/// <summary>Java parity: model/templates/spawns/riftspawns/RiftSpawn.</summary>
[XmlType("RiftSpawn")]
public class RiftSpawn
{
    [XmlAttribute("id")]    public int Id    { get; set; }
    [XmlAttribute("world")] public int World { get; set; }
    [XmlElement("spawn")]   public List<Spawn>? Spawns { get; set; } = [];

    public int GetId()       => Id;
    public int GetWorldId()  => World;
    public List<Spawn> GetSpawns() => Spawns ??= [];
}
