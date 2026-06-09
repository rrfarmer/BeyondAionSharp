using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Questengine;
using Aion.GameServer.Questengine.Handlers.Template;

namespace Aion.GameServer.Questengine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/ItemOrdersData.</summary>
[XmlType("ItemOrdersData")]
public class ItemOrdersData : XMLQuest
{
    [XmlAttribute("talk_npc_id1")] protected int talkNpcId1;
    [XmlAttribute("talk_npc_id2")] protected int talkNpcId2;
    [XmlAttribute("end_npc_id")] protected int endNpcId;

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new ItemOrders(id, talkNpcId1, talkNpcId2, endNpcId));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        return null;
    }
}
