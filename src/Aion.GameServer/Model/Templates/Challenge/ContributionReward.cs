using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Challenge;

/// <summary>Java parity: model/templates/challenge/ContributionReward.</summary>
[XmlType("ContributionReward")]
public class ContributionReward
{
    [XmlAttribute("item_count")] protected int itemCount;
    [XmlAttribute("reward_id")] protected int rewardId;
    [XmlAttribute("number")] protected int number;
    [XmlAttribute("rank")] protected int rank;

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
