using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/FinishedQuestCond (antness).</summary>
[XmlType("FinishedQuest")]
public class FinishedQuestCond
{
    [XmlAttribute("quest_id")] protected int questId;
    [XmlAttribute("reward")] protected int reward = -1;

    public int GetQuestId()
    {
        return questId;
    }

    public int GetReward()
    {
        return reward;
    }
}
