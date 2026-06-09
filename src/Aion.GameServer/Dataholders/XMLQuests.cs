using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Questengine.Handlers.Models;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/XMLQuests (MrPoke). @XmlRootElement(quest_scripts); @XmlElements(16 element→XMLQuest subtype)→stacked [XmlElement]; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("quest_scripts")]
public class XMLQuests
{
    [XmlElement("report_to", typeof(ReportToData))]
    [XmlElement("monster_hunt", typeof(MonsterHuntData))]
    [XmlElement("xml_quest", typeof(XmlQuestData))]
    [XmlElement("item_collecting", typeof(ItemCollectingData))]
    [XmlElement("relic_rewards", typeof(RelicRewardsData))]
    [XmlElement("crafting_rewards", typeof(CraftingRewardsData))]
    [XmlElement("report_to_many", typeof(ReportToManyData))]
    [XmlElement("kill_in_world", typeof(KillInWorldData))]
    [XmlElement("kill_in_zone", typeof(KillInZoneData))]
    [XmlElement("skill_use", typeof(SkillUseData))]
    [XmlElement("kill_spawned", typeof(KillSpawnedData))]
    [XmlElement("mentor_monster_hunt", typeof(MentorMonsterHuntData))]
    [XmlElement("fountain_rewards", typeof(FountainRewardsData))]
    [XmlElement("item_order", typeof(ItemOrdersData))]
    [XmlElement("work_order", typeof(WorkOrdersData))]
    [XmlElement("report_on_levelup", typeof(ReportOnLevelUpData))]
    private List<XMLQuest> data;

    [XmlIgnore] private readonly Dictionary<int, XMLQuest> questsById = new();

    public void AfterUnmarshal(object parent)
    {
        if (data != null)
        {
            questsById.Clear();
            foreach (XMLQuest quest in data)
                questsById[quest.GetId()] = quest;
            data = null;
        }
    }

    public ICollection<XMLQuest> GetAllQuests()
    {
        return questsById.Values;
    }

    public XMLQuest GetQuest(int questId)
    {
        return questsById.TryGetValue(questId, out var v) ? v : null;
    }

    public void SetData(List<XMLQuest> data)
    {
        this.data = data;
        AfterUnmarshal(null);
    }
}
