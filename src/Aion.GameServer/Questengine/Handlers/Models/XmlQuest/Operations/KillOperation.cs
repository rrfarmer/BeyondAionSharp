using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/KillOperation.</summary>
[XmlType("KillOperation")]
public class KillOperation : QuestOperation
{
    public override void DoOperate(QuestEnv env)
    {
        if (env.GetVisibleObject() is Npc npc)
            npc.GetController().Die(env.GetPlayer());
    }
}
