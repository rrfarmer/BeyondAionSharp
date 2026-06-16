using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/Rewards.</summary>
[XmlType("Rewards")]
public class Rewards
{
    [XmlElement("selectable_reward_item")] public List<QuestItems> selectableRewardItem;
    [XmlElement("reward_item")] public List<QuestItems> rewardItem;
    [XmlAttribute("gold")] public long kinah;
    [XmlAttribute("exp")] public int exp;
    [XmlAttribute("ap")] public int abyssPoints;
    [XmlAttribute("dp")] public int divinePoints;
    [XmlAttribute("gp")] public int gloryPoints;
    [XmlAttribute("title")] public int title;
    [XmlAttribute("extend_inventory")] public int extendInventory;
    [XmlAttribute("extend_stigma")] public int extendStigma;

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

    [XmlAttribute("icheck")] public int inventoryItemCheck;

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
