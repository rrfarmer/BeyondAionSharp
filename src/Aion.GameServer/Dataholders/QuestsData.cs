using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.Services;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/QuestsData (MrPoke). @XmlRootElement(quests); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("quests")]
public class QuestsData
{
    [XmlElement("quest")] private List<QuestTemplate> questsData;

    [XmlIgnore] private readonly Dictionary<int, QuestTemplate> questTemplates = new();
    [XmlIgnore] private readonly Dictionary<int, List<QuestTemplate>> sortedByFactionId = new();

    public void AfterUnmarshal(object parent)
    {
        questTemplates.Clear();
        sortedByFactionId.Clear();
        foreach (QuestTemplate quest in questsData)
        {
            questTemplates[quest.GetId()] = quest;
            int npcFactionId = quest.GetNpcFactionId();
            if (npcFactionId == 0 || quest.IsTimeBased())
                continue;
            if (!sortedByFactionId.ContainsKey(npcFactionId))
            {
                List<QuestTemplate> factionQuests = new();
                factionQuests.Add(quest);
                sortedByFactionId[npcFactionId] = factionQuests;
            }
            else
            {
                sortedByFactionId[npcFactionId].Add(quest);
            }
        }
        questsData = null;
    }

    public QuestTemplate GetQuestById(int id)
    {
        return questTemplates.TryGetValue(id, out var v) ? v : null;
    }

    public List<QuestTemplate> GetQuestsByNpcFaction(int npcFactionId, Player player)
    {
        List<QuestTemplate> factionQuests = sortedByFactionId[npcFactionId];
        List<QuestTemplate> quests = new();
        foreach (QuestTemplate questTemplate in factionQuests)
        {
            if (!QuestEngine.GetInstance().IsHaveHandler(questTemplate.GetId()))
                continue;
            if (QuestService.CheckStartConditions(player, questTemplate.GetId(), false))
                quests.Add(questTemplate);
        }
        return quests;
    }

    public int Size()
    {
        return questTemplates.Count;
    }

    public ICollection<QuestTemplate> GetQuestTemplates()
    {
        return questTemplates.Values;
    }
}
