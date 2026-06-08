using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropTribe.</summary>
[XmlType("GlobalDropTribe")]
public class GlobalDropTribe
{
    [XmlAttribute("tribe")] public TribeClass Tribe { get; set; }
    public TribeClass GetTribe() => Tribe;
}
