using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/StartQuestOperation.</summary>
[XmlType("StartQuestOperation")]
public class StartQuestOperation : QuestOperation
{
    [XmlAttribute("id")] protected int id;

    public override void DoOperate(QuestEnv env)
    {
        // TODO Auto-generated method stub
    }
}
