using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/FullRewardItem.</summary>
[XmlType("FullRewardItem")]
public class FullRewardItem : IdLevelReward
{
    [XmlAttribute("count")] private long count;
    [XmlAttribute("chance")] private float chance;

    public override long GetCount()
    {
        return count;
    }

    public override float GetChance()
    {
        return chance;
    }
}
