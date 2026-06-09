using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/Rewards.</summary>
[XmlType("Rewards")]
public class Rewards
{
    [XmlElement("selectable_reward_item")] private List<QuestItems> selectableRewardItem;
    [XmlElement("reward_item")] private List<QuestItems> rewardItem;
    [XmlAttribute("gold")] private long kinah;
    [XmlAttribute("exp")] private int exp;
    [XmlAttribute("ap")] private int abyssPoints;
    [XmlAttribute("dp")] private int divinePoints;
    [XmlAttribute("gp")] private int gloryPoints;
    [XmlAttribute("title")] private int title;
    [XmlAttribute("extend_inventory")] private int extendInventory;
    [XmlAttribute("extend_stigma")] private int extendStigma;

    // Java parity: @XmlAttribute(name="ccheck") List<Integer> — space-separated.
    private List<int> collectItemChecks;

    [XmlAttribute("ccheck")]
    public string CollectItemChecksRaw
    {
        get => collectItemChecks == null ? null : string.Join(" ", collectItemChecks);
        set => collectItemChecks = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("icheck")] private int inventoryItemCheck;

    public List<QuestItems> GetSelectableRewardItem()
    {
        return selectableRewardItem ?? new List<QuestItems>();
    }

    public List<QuestItems> GetRewardItem()
    {
        return rewardItem ?? new List<QuestItems>();
    }

    public long GetKinah()
    {
        return kinah;
    }

    public int GetExp()
    {
        return exp;
    }

    public int GetAp()
    {
        return abyssPoints;
    }

    public int GetDp()
    {
        return divinePoints;
    }

    public int GetGp()
    {
        return gloryPoints;
    }

    public int GetTitle()
    {
        return title;
    }

    public int GetExtendInventory()
    {
        return extendInventory;
    }

    public int GetExtendStigma()
    {
        return extendStigma;
    }

    public List<int> GetCollectItemChecks()
    {
        return collectItemChecks ?? new List<int>();
    }

    public int GetInventoryItemCheck()
    {
        return inventoryItemCheck;
    }
}
