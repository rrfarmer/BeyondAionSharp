using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>Java parity: model/templates/globaldrops/GlobalDropNpc.</summary>
[XmlType("GlobalDropNpc")]
public class GlobalDropNpc
{
    [XmlAttribute("npc_id")] public int NpcId { get; set; }
    public int GetNpcId() => NpcId;
    public void SetNpcId(int value) => NpcId = value;
}
