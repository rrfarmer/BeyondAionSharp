using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Events;

/// <summary>Java parity: .../events/OnTalkEvent.</summary>
[XmlType("OnTalkEvent")]
public class OnTalkEvent : QuestEvent
{
    [XmlElement("var")] protected List<QuestVar> var;

    public override bool Operate(QuestEnv env)
    {
        if (conditions == null || conditions.CheckConditionOfSet(env))
        {
            QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(env.GetQuestId());
            foreach (QuestVar questVar in var)
            {
                if (questVar.Operate(env, qs))
                    return true;
            }
        }
        return false;
    }
}
