using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropNpcGroup.</summary>
[XmlType("GlobalDropNpcGroup")]
public class GlobalDropNpcGroup
{
    [XmlAttribute("group")] public GroupDropType Group { get; set; }
    public GroupDropType GetGroup() => Group;
}
