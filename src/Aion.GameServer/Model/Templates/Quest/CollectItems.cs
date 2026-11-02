using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/CollectItems (MrPoke).</summary>
[XmlType("CollectItems")]
public class CollectItems
{
    [XmlElement("collect_item")] public List<CollectItem> collectItem;

    [XmlAttribute("start_check")] public bool startCheck;

    /// <summary>Gets the value of the collectItem property.</summary>
    public List<CollectItem> GetCollectItem()
    {
        return collectItem == null ? new List<CollectItem>() : collectItem;
    }

    public bool GetStartCheck()
    {
        return startCheck;
    }
}
