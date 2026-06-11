using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers.Template;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/CraftingRewardsData.</summary>
[XmlType("CraftingRewardsData")]
public class CraftingRewardsData : XMLQuest
{
    [XmlAttribute("start_npc_id")] protected int startNpcId;
    [XmlAttribute("end_npc_id")] protected int endNpcId;
    [XmlAttribute("skill_id")] protected int skillId;
    [XmlAttribute("level_reward")] protected int levelReward;

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new CraftingRewards(id, startNpcId, skillId, levelReward, endNpcId, questMovie));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        return null;
    }
}
