using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Conditions;

/// <summary>Java parity: .../conditions/QuestStatusCondition. Nullable Integer quest_id→int?.</summary>
[XmlType("QuestStatusCondition")]
public class QuestStatusCondition : QuestCondition
{
    [XmlAttribute("value")] public QuestStatus value;

    // Nullable Integer quest_id: XmlSerializer rejects [XmlAttribute] on Nullable<int>, so bind via a string proxy.
    public int? questId;

    [XmlAttribute("quest_id")]
    public string QuestIdRaw
    {
        get => questId?.ToString();
        set => questId = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value);
    }

    public override bool DoCheck(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int qstatus = 0;
        int id = env.GetQuestId();
        if (questId != null)
            id = questId.Value;
        QuestState qs = player.GetQuestStateList().GetQuestState(id);
        if (qs != null)
            qstatus = qs.GetStatus().Value();
        switch (GetOp())
        {
            case ConditionOperation.EQUAL:
                return qstatus == value.Value();
            case ConditionOperation.GREATER:
                return qstatus > value.Value();
            case ConditionOperation.GREATER_EQUAL:
                return qstatus >= value.Value();
            case ConditionOperation.LESSER:
                return qstatus < value.Value();
            case ConditionOperation.LESSER_EQUAL:
                return qstatus <= value.Value();
            case ConditionOperation.NOT_EQUAL:
                return qstatus != value.Value();
            default:
                return false;
        }
    }
}
