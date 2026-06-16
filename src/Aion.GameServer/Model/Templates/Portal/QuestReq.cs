using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/QuestReq (xTz).</summary>
[XmlType("QuestReq")]
public class QuestReq
{
    [XmlAttribute("quest_id")] public int questId;
    [XmlAttribute("quest_step")] public int questStep;

    public int GetQuestId()
    {
        return questId;
    }

    public void SetQuestId(int value)
    {
        this.questId = value;
    }

    public int GetQuestStep()
    {
        return questStep;
    }

    public void SetQuestStep(int value)
    {
        this.questStep = value;
    }
}
