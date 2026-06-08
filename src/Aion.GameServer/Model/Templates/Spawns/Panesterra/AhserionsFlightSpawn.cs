using System.Xml.Serialization;
using Aion.GameServer.Services.Panesterra.Ahserion;

namespace Aion.GameServer.Model.Templates.Spawns.Panesterra;

/// <summary>Java parity: model/templates/spawns/panesterra/AhserionsFlightSpawn.</summary>
[XmlType("AhserionsFlightSpawn")]
public class AhserionsFlightSpawn
{
    [XmlAttribute("faction")]              public PanesterraFaction Faction { get; set; }
    [XmlElement("ahserion_stage_spawn")]   public List<AhserionStageSpawnTemplate>? StageSpawnTemplates { get; set; }

    public PanesterraFaction GetFaction() => Faction;
    public List<AhserionStageSpawnTemplate>? GetStageSpawnTemplate() => StageSpawnTemplates;

    [XmlType("AhserionStageSpawnTemplate")]
    public class AhserionStageSpawnTemplate
    {
        [XmlAttribute("stage")] public int Stage { get; set; } = 0;
        [XmlElement("spawn")]   public List<Spawn>? Spawns { get; set; }

        public int GetStage() => Stage;
        public List<Spawn>? GetSpawns() => Spawns;
    }
}
