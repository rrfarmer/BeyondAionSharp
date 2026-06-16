using System.Xml.Serialization;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/CraftItem.</summary>
[XmlType("CraftItem")]
public class CraftItem : CraftReward
{
    [XmlAttribute("minLevel")] public int minLevel;
    [XmlAttribute("maxLevel")] public int maxLevel;

    public int GetMinLevel()
    {
        return minLevel;
    }

    public int GetMaxLevel()
    {
        return maxLevel;
    }

    public override long GetCount()
    {
        return Rnd.Get(3, 5);
    }

    protected override bool MatchesQuest(QuestTemplate questTemplate)
    {
        if (!base.MatchesQuest(questTemplate))
            return false;
        if (questTemplate.GetCombineSkillPoint() < minLevel)
            return false;
        if (questTemplate.GetCombineSkillPoint() > maxLevel)
            return false;
        return true;
    }
}
