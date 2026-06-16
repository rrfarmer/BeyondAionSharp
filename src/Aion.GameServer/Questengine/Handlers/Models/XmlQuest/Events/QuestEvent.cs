using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Conditions;
using Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Events;

/// <summary>Java parity: .../events/QuestEvent. @XmlSeeAlso→[XmlInclude]; ids @XmlAttribute List<Integer>→Raw space-sep.</summary>
[XmlType("QuestEvent")]
[XmlInclude(typeof(OnKillEvent))]
[XmlInclude(typeof(OnTalkEvent))]
public abstract class QuestEvent
{
    [XmlElement("conditions")] public QuestConditions conditions;
    [XmlElement("operations")] public QuestOperations operations;

    private List<int> ids;

    [XmlAttribute("ids")]
    public string IdsRaw
    {
        get => ids == null ? null : string.Join(" ", ids);
        set => ids = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    public virtual bool Operate(QuestEnv env)
    {
        return false;
    }

    public List<int> GetIds()
    {
        if (ids == null)
        {
            ids = new List<int>();
        }
        return this.ids;
    }
}
