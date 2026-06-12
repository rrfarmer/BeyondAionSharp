using Aion.GameServer.Services;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/CollectItemQuestOperation. Boolean removeItems→bool?.</summary>
[XmlType("CollectItemQuestOperation")]
public class CollectItemQuestOperation : QuestOperation
{
    [XmlElement("true")] protected QuestOperations _true;
    [XmlElement("false")] protected QuestOperations _false;
    [XmlAttribute("removeItems")] protected bool? removeItems;

    public override void DoOperate(QuestEnv env)
    {
        if (QuestService.CollectItemCheck(env, removeItems == null ? true : false))
            _true.Operate(env);
        else
            _false.Operate(env);
    }
}
