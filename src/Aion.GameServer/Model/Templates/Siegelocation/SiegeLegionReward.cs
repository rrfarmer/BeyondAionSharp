using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/SiegeLegionReward (Source).</summary>
[XmlType("SiegeLegionReward")]
public class SiegeLegionReward
{
    [XmlAttribute("item_id")] private int itemId;
    [XmlAttribute("item_count")] private long itemCount;

    public int GetItemId()
    {
        return itemId;
    }

    public long GetItemCount()
    {
        return itemCount;
    }
}
