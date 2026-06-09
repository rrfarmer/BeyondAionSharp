using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Chest;

/// <summary>Java parity: model/templates/chest/ChestTemplate (Wakizashi).</summary>
[XmlType("Chest")]
public class ChestTemplate
{
    [XmlAttribute("npc_id")] protected int npcId;
    [XmlElement("key_item")] protected List<KeyItem> keyItems;

    public int GetNpcId()
    {
        return npcId;
    }

    public List<KeyItem> GetKeyItems()
    {
        return keyItems;
    }
}
