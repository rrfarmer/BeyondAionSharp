using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event.Upgradearcade;

/// <summary>Java parity: model/templates/event/upgradearcade/ArcadeRewardItem (ginho1).</summary>
[XmlType("ArcadeRewardItem")]
public class ArcadeRewardItem
{
    [XmlAttribute("item_id")] private int itemId;
    [XmlAttribute("normal_count")] private long normalCount;
    [XmlAttribute("frenzy_count")] private long frenzyCount;

    public int GetItemId()
    {
        return itemId;
    }

    public long GetNormalCount()
    {
        return normalCount;
    }

    public long GetFrenzyCount()
    {
        return frenzyCount;
    }
}
