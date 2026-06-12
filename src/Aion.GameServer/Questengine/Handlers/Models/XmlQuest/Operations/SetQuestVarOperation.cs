using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using ActionType = Aion.GameServer.Network.Aion.ServerPackets.SM_QUEST_ACTION.ActionType;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/SetQuestVarOperation.</summary>
[XmlType("SetQuestVarOperation")]
public class SetQuestVarOperation : QuestOperation
{
    [XmlAttribute("var_id")] protected int varId;
    [XmlAttribute("value")] protected int value;

    public override void DoOperate(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int questId = env.GetQuestId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null)
        {
            qs.GetQuestVars().SetVarById(varId, value);
            PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(ActionType.UPDATE, qs));
        }
    }
}
