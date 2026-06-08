using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Spawns.Mercenaries;

/// <summary>Java parity: model/templates/spawns/mercenaries/MercenarySpawn.</summary>
[XmlType("MercenarySpawn")]
public class MercenarySpawn
{
    [XmlAttribute("siege_id")]        public int SiegeId { get; set; }
    [XmlElement("mercenary_race")]    public List<MercenaryRace>? MercenaryRaces { get; set; }

    public int GetSiegeId() => SiegeId;
    public List<MercenaryRace>? GetMercenaryRaces() => MercenaryRaces;
}
