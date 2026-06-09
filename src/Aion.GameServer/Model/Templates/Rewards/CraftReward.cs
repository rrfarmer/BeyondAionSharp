using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Model.Templates.Itemgroups;

namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/CraftReward. @XmlSeeAlso→[XmlInclude].</summary>
[XmlType("CraftReward")]
[XmlInclude(typeof(CraftRecipe))]
[XmlInclude(typeof(CraftItem))]
public abstract class CraftReward : ItemRaceEntry
{
    [XmlAttribute("skill")] private int skill;

    public int GetSkill()
    {
        return skill;
    }

    protected override bool MatchesQuest(QuestTemplate questTemplate)
    {
        return questTemplate.GetCombineSkill() == skill;
    }
}
