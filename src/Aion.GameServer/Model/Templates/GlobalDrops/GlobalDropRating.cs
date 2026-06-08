using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropRating.</summary>
[XmlType("GlobalDropRating")]
public class GlobalDropRating
{
    [XmlAttribute("rating")] public NpcRating Rating { get; set; }
    public NpcRating GetRating() => Rating;
}
