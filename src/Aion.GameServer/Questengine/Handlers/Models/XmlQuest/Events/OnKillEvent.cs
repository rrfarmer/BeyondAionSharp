using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using ActionType = Aion.GameServer.Network.Aion.ServerPackets.SM_QUEST_ACTION.ActionType;
using Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Events;

/// <summary>Java parity: .../events/OnKillEvent.</summary>
[XmlType("OnKillEvent")]
public class OnKillEvent : QuestEvent
{
    [XmlElement("monster")] protected List<Monster> monster;
    [XmlElement("complite")] protected QuestOperations complite;

    public List<Monster> GetMonsters()
    {
        if (monster == null)
        {
            monster = new List<Monster>();
        }
        return this.monster;
    }

    public override bool Operate(QuestEnv env)
    {
        if (monster == null || !(env.GetVisibleObject() is Npc))
            return false;
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(env.GetQuestId());
        if (qs == null)
            return false;
        Npc npc = (Npc) env.GetVisibleObject();
        foreach (Monster m in monster)
        {
            if (m.GetNpcIds().Contains(npc.GetNpcId()))
            {
                int var = qs.GetQuestVarById(m.GetVar());
                if (var >= m.GetStartVar() && var < m.GetEndVar())
                {
                    qs.SetQuestVarById(m.GetVar(), var + 1);
                    PacketSendUtility.SendPacket(env.GetPlayer(), new SM_QUEST_ACTION(ActionType.UPDATE, qs));
                }
            }
        }
        if (complite != null)
        {
            foreach (Monster m in monster)
            {
                if (qs.GetQuestVarById(m.GetVar()) != m.GetEndVar())
                    return false;
            }
            complite.Operate(env);
        }
        return false;
    }
}
