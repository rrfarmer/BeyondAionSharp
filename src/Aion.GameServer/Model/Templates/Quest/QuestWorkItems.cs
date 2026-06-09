using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestWorkItems.</summary>
[XmlType("QuestWorkItems")]
public class QuestWorkItems
{
    [XmlElement("quest_work_item")] protected List<QuestItems> questWorkItem;

    /// <summary>Gets the value of the questWorkItem property.</summary>
    public List<QuestItems> GetQuestWorkItem()
    {
        return questWorkItem ?? new List<QuestItems>();
    }
}
