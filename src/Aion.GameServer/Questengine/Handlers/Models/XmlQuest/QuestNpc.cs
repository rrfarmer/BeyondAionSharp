using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest;

/// <summary>Java parity: questEngine/handlers/models/xmlQuest/QuestNpc (distinct from model/templates/quest/QuestNpc).</summary>
[XmlType("QuestNpc")]
public class QuestNpc
{
    [XmlElement("dialog")] protected List<QuestDialog> dialog;
    [XmlAttribute("id")] protected int id;

    public bool Operate(QuestEnv env, QuestState qs)
    {
        int npcId = -1;
        if (env.GetVisibleObject() is Npc npc)
            npcId = npc.GetNpcId();
        if (npcId != id)
            return false;
        foreach (QuestDialog questDialog in dialog)
        {
            if (questDialog.Operate(env, qs))
                return true;
        }
        return false;
    }
}
