using System.Xml.Serialization;
using Aion.GameServer.Model.Siege;

namespace Aion.GameServer.Model.Templates.Spawns.Siegespawns;

/// <summary>Java parity: model/templates/spawns/siegespawns/SiegeSpawn.</summary>
[XmlType("SiegeSpawn")]
public class SiegeSpawn
{
    [XmlAttribute("siege_id")] public int SiegeId { get; set; }
    [XmlElement("siege_race")] public List<SiegeRaceTemplate>? SiegeRaceTemplates { get; set; }

    public int GetSiegeId() => SiegeId;
    public List<SiegeRaceTemplate>? GetSiegeRaceTemplates() => SiegeRaceTemplates;

    [XmlType("SiegeRaceTemplate")]
    public class SiegeRaceTemplate
    {
        [XmlAttribute("race")]    public SiegeRace Race { get; set; }
        [XmlElement("siege_mod")] public List<SiegeModTemplate>? SiegeModTemplates { get; set; }

        public SiegeRace GetSiegeRace() => Race;
        public List<SiegeModTemplate>? GetSiegeModTemplates() => SiegeModTemplates;

        [XmlType("SiegeModTemplate")]
        public class SiegeModTemplate
        {
            [XmlAttribute("mod")]   public SiegeModType SiegeMod { get; set; }
            [XmlElement("spawn")]   public List<Spawn>? Spawns   { get; set; }

            public List<Spawn>?    GetSpawns()       => Spawns;
            public SiegeModType    GetSiegeModType() => SiegeMod;
        }
    }
}
