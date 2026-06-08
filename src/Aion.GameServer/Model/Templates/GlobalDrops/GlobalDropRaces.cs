using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropRaces.</summary>
[XmlType("GlobalDropRaces")]
public class GlobalDropRaces
{
    [XmlElement("gd_race")] public List<GlobalDropRace>? GdRaces { get; set; }
    public List<GlobalDropRace> GetGlobalDropRaces() => GdRaces ??= [];
}
