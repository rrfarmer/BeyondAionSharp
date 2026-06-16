using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Chest;

/// <summary>Java parity: model/templates/chest/ChestTemplate (Wakizashi).</summary>
[XmlType("Chest")]
public class ChestTemplate
{
    // Public so XmlSerializer can bind these members (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("npc_id")] public int npcId;
    [XmlElement("key_item")] public List<KeyItem> keyItems;

    public int GetNpcId()
    {
        return npcId;
    }

    public List<KeyItem> GetKeyItems()
    {
        return keyItems;
    }
}
