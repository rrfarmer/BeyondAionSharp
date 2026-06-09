using System.Xml.Serialization;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Conditions;

/// <summary>Java parity: .../conditions/QuestCondition. @XmlSeeAlso→[XmlInclude] (subclasses red until ported).</summary>
[XmlType("QuestCondition")]
[XmlInclude(typeof(NpcIdCondition))]
[XmlInclude(typeof(DialogIdCondition))]
[XmlInclude(typeof(PcInventoryCondition))]
[XmlInclude(typeof(QuestVarCondition))]
[XmlInclude(typeof(QuestStatusCondition))]
public abstract class QuestCondition
{
    [XmlAttribute("op")] protected ConditionOperation op;

    public ConditionOperation GetOp()
    {
        return op;
    }

    public abstract bool DoCheck(QuestEnv env);
}
