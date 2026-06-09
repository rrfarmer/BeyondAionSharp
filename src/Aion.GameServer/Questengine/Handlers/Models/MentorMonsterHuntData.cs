using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Questengine;
using Aion.GameServer.Questengine.Handlers.Template;

namespace Aion.GameServer.Questengine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/MentorMonsterHuntData (extends MonsterHuntData).</summary>
[XmlType("MentorMonsterHuntData")]
public class MentorMonsterHuntData : MonsterHuntData
{
    [XmlAttribute("min_mente_level")] protected int minMenteLevel = 1;
    [XmlAttribute("max_mente_level")] protected int maxMenteLevel = 99;

    public int GetMinMenteLevel()
    {
        return minMenteLevel;
    }

    public int GetMaxMenteLevel()
    {
        return maxMenteLevel;
    }

    public override void Register(QuestEngine questEngine)
    {
        List<Monster> monsters;
        QuestTemplate questTemplate = DataManager.QUEST_DATA.GetQuestById(id);

        if (questTemplate.GetQuestKill().Count != 0)
        {
            monsters = new List<Monster>();
            foreach (QuestKill qk in questTemplate.GetQuestKill())
            {
                Monster m = new Monster();
                if (qk.GetKillCount() > 0)
                    m.SetEndVar(qk.GetKillCount());
                if (qk.GetNpcIds() != null)
                    m.AddNpcIds(qk.GetNpcIds());
                if (qk.GetVar() > 0)
                    m.SetVar(qk.GetVar());
                if (qk.GetQuestStep() > 0)
                    m.SetStep(qk.GetQuestStep());
                if (qk.GetSequenceNumber() > 0)
                    m.SetVar(qk.GetSequenceNumber());
                monsters.Add(m);
            }
        }
        else
        {
            monsters = new List<Monster>();
        }

        questEngine.AddQuestHandler(new MentorMonsterHunt(id, startNpcIds, endNpcIds, monsters, minMenteLevel, maxMenteLevel, reward, rewardNextStep));
    }
}
