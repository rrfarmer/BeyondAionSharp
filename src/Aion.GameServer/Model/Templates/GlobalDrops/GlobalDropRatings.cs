using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropRatings.</summary>
[XmlType("GlobalDropRatings")]
public class GlobalDropRatings
{
    [XmlElement("gd_rating")] public List<GlobalDropRating>? GdRatings { get; set; }
    public List<GlobalDropRating> GetGlobalDropRatings() => GdRatings ??= [];
}
