using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/QuestOperations. @XmlElements→stacked [XmlElement(name,typeof)] (ActionItemUseOperation red until ported).</summary>
[XmlType("QuestOperations")]
public class QuestOperations
{
    [XmlElement("take_item", typeof(TakeItemOperation))]
    [XmlElement("npc_dialog", typeof(NpcDialogOperation))]
    [XmlElement("set_quest_status", typeof(SetQuestStatusOperation))]
    [XmlElement("give_item", typeof(GiveItemOperation))]
    [XmlElement("start_quest", typeof(StartQuestOperation))]
    [XmlElement("npc_use", typeof(ActionItemUseOperation))]
    [XmlElement("set_quest_var", typeof(SetQuestVarOperation))]
    [XmlElement("collect_items", typeof(CollectItemQuestOperation))]
    public List<QuestOperation> operations;

    // Nullable Boolean override: XmlSerializer rejects [XmlAttribute] on Nullable<bool>, so bind via a string proxy.
    public bool? @override;

    [XmlAttribute("override")]
    public string OverrideRaw
    {
        get => @override?.ToString().ToLowerInvariant();
        set => @override = string.IsNullOrEmpty(value) ? (bool?)null : bool.Parse(value);
    }

    public bool IsOverride()
    {
        if (@override == null)
        {
            return true;
        }
        else
        {
            return @override.Value;
        }
    }

    public bool Operate(QuestEnv env)
    {
        if (operations != null)
        {
            foreach (QuestOperation oper in operations)
            {
                oper.DoOperate(env);
            }
        }
        return IsOverride();
    }
}
