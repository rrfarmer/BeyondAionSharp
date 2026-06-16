using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/ContributionReward.</summary>
[XmlType("ContributionReward")]
public class ContributionReward
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on protected fields).
    [XmlAttribute("item_count")] public int itemCount;
    [XmlAttribute("reward_id")] public int rewardId;
    [XmlAttribute("number")] public int number;
    [XmlAttribute("rank")] public int rank;

    public int GetItemCount()
    {
        return itemCount;
    }

    public int GetRewardId()
    {
        return rewardId;
    }

    public int GetNumber()
    {
        return number;
    }

    public int GetRank()
    {
        return rank;
    }
}
