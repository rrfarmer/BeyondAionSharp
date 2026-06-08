using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Spawns.Mercenaries;

/// <summary>Java parity: model/templates/spawns/mercenaries/MercenaryRace.</summary>
[XmlType("MercenaryRace")]
public class MercenaryRace
{
    [XmlAttribute("race")]           public Race Race { get; set; }
    [XmlElement("mercenary_zone")]   public List<MercenaryZone>? MercenaryZones { get; set; }

    public Race GetRace() => Race;
    public List<MercenaryZone>? GetMercenaryZones() => MercenaryZones;
}
