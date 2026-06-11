using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items.Purification;

/// <summary>Java parity: model/templates/item/purification/ItemPurificationTemplate (Ranastic, Navyan, Estrayl).</summary>
[XmlRoot("ItemPurification")]
public class ItemPurificationTemplate
{
    [XmlElement("purification_result")] private List<PurificationResult> purificationResults;
    [XmlAttribute("base_item_id")] private int baseItemId;

    public List<PurificationResult> GetPurificationResults()
    {
        return purificationResults;
    }

    public int GetBaseItemId()
    {
        return baseItemId;
    }
}
