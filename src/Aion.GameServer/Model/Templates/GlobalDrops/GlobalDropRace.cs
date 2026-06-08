using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropRace.</summary>
[XmlType("GlobalDropRace")]
public class GlobalDropRace
{
    [XmlAttribute("race")] public Race Race { get; set; }
    public Race GetRace() => Race;
}
