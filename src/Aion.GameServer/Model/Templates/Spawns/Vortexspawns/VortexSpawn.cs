using System.Xml.Serialization;
using Aion.GameServer.Model.Vortex;

namespace Aion.GameServer.Model.Templates.Spawns.Vortexspawns;

/// <summary>Java parity: model/templates/spawns/vortexspawns/VortexSpawn.</summary>
[XmlType("VortexSpawn")]
public class VortexSpawn
{
    [XmlAttribute("id")]          public int Id { get; set; }
    [XmlElement("state_type")]    public List<VortexStateTemplate>? StateTemplates { get; set; }

    public int GetId() => Id;
    public List<VortexStateTemplate>? GetStateTemplates() => StateTemplates;

    [XmlType("VortexStateTemplate")]
    public class VortexStateTemplate
    {
        [XmlAttribute("state")]  public VortexStateType StateType { get; set; }
        [XmlElement("spawn")]    public List<Spawn>?    Spawns    { get; set; }

        public VortexStateType GetStateType() => StateType;
        public List<Spawn>?    GetSpawns()    => Spawns;
    }
}
