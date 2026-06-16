using Aion.GameServer.Services;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/CollectItemQuestOperation. Boolean removeItems→bool?.</summary>
[XmlType("CollectItemQuestOperation")]
public class CollectItemQuestOperation : QuestOperation
{
    [XmlElement("true")] public QuestOperations _true;
    [XmlElement("false")] public QuestOperations _false;

    // Nullable Boolean removeItems: XmlSerializer rejects [XmlAttribute] on Nullable<bool>, so bind via a string proxy.
    public bool? removeItems;

    [XmlAttribute("removeItems")]
    public string RemoveItemsRaw
    {
        get => removeItems?.ToString().ToLowerInvariant();
        set => removeItems = string.IsNullOrEmpty(value) ? (bool?)null : bool.Parse(value);
    }

    public override void DoOperate(QuestEnv env)
    {
        if (QuestService.CollectItemCheck(env, removeItems == null ? true : false))
            _true.Operate(env);
        else
            _false.Operate(env);
    }
}
