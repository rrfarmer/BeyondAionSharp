using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders.Loadingutils.Adapters;

/// <summary>Java parity: dataholders/loadingutils/adapters/NpcEquipmentList (Luno).</summary>
public class NpcEquipmentList
{
    // Java parity: @XmlElement("item") @XmlIDREF ItemTemplate[] — references item templates by id.
    [XmlElement("item")]
    public ItemTemplate[] items;
}
