using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/CraftRecipe.</summary>
[XmlType("CraftRecipe")]
public class CraftRecipe : CraftReward
{
    [XmlAttribute("level")] public int level;

    public int GetLevel()
    {
        return level;
    }

    protected override bool MatchesQuest(QuestTemplate questTemplate)
    {
        if (!base.MatchesQuest(questTemplate))
            return false;
        if (questTemplate.GetCombineSkillPoint() < level)
            return false;
        if (questTemplate.GetCombineSkillPoint() > GetMaxLevel())
            return false;
        return true;
    }

    private int GetMaxLevel()
    {
        return Math.Min(level + 40, level / 100 * 100 + 99);
    }
}
