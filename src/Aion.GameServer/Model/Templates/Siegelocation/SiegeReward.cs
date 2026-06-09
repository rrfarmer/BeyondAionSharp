using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/SiegeReward (Source).</summary>
[XmlType("SiegeReward")]
public class SiegeReward
{
    [XmlAttribute("top")] protected int top;
    [XmlAttribute("item_id")] protected int itemId;
    [XmlAttribute("item_count")] protected int itemCount;
    [XmlAttribute("item_id_defeat")] protected int itemIdDefeat;
    [XmlAttribute("item_count_defeat")] protected int itemCountDefeat;
    [XmlAttribute("gp_win")] protected int gpWin;
    [XmlAttribute("gp_defeat")] protected int gpDefeat;

    public int GetTop()
    {
        return top;
    }

    public int GetItemId()
    {
        return itemId;
    }

    public int GetItemCount()
    {
        return itemCount;
    }

    public int GetGpForWin()
    {
        return gpWin;
    }

    public int GetGpForDefeat()
    {
        return gpDefeat;
    }

    public int GetItemIdDefeat()
    {
        return itemIdDefeat;
    }

    public int GetItemCountDefeat()
    {
        return itemCountDefeat;
    }

    public bool HasItemRewardsForWin()
    {
        return itemId > 0 && itemCount > 0;
    }

    public bool HasItemRewardsForDefeat()
    {
        return itemIdDefeat > 0 && itemCountDefeat > 0;
    }
}
