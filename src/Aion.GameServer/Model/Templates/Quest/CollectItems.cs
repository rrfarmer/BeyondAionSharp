using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/CollectItems (MrPoke).</summary>
[XmlType("CollectItems")]
public class CollectItems
{
    [XmlElement("collect_item")] protected List<CollectItem> collectItem;

    // Java parity: Boolean start_check (nullable; getStartCheck null->false).
    [XmlAttribute("start_check")] protected bool? startCheck;

    /// <summary>Gets the value of the collectItem property.</summary>
    public List<CollectItem> GetCollectItem()
    {
        if (collectItem == null)
        {
            collectItem = new List<CollectItem>();
        }
        return this.collectItem;
    }

    public bool GetStartCheck()
    {
        if (startCheck == null)
            return false;
        return startCheck.Value;
    }
}
