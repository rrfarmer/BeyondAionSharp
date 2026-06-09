using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest;

/// <summary>Java parity: questEngine/handlers/models/xmlQuest/QuestVar.</summary>
[XmlType("QuestVar")]
public class QuestVar
{
    [XmlElement("npc")] protected List<QuestNpc> npc;
    [XmlAttribute("value")] protected int value;

    public bool Operate(QuestEnv env, QuestState qs)
    {
        int var = -1;
        if (qs != null)
            var = qs.GetQuestVars().GetQuestVars();
        if (var != value)
            return false;
        foreach (QuestNpc questNpc in npc)
        {
            if (questNpc.Operate(env, qs))
                return true;
        }
        return false;
    }
}
