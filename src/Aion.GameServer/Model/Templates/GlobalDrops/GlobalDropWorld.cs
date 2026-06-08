using System.Xml.Serialization;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropWorld.</summary>
[XmlType("GlobalDropWorld")]
public class GlobalDropWorld
{
    [XmlAttribute("wd_type")] public WorldDropType WdType { get; set; }
    public WorldDropType GetWorldDropType() => WdType;
}
