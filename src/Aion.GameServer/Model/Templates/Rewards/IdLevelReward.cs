using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Itemgroups;

namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/IdLevelReward.</summary>
[XmlType("IdLevelReward")]
public class IdLevelReward : ItemRaceEntry
{
    [XmlAttribute("level")] private int level;

    public int GetLevel()
    {
        return level;
    }

    protected override bool MatchesLevel(ItemTemplate itemTemplate, int bonusItemLevel)
    {
        return bonusItemLevel == 0 || level == bonusItemLevel;
    }
}
