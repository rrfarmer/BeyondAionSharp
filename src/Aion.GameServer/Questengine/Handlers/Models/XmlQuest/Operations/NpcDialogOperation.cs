using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/NpcDialogOperation. Nullable Integer quest_id→int?.</summary>
[XmlType("NpcDialogOperation")]
public class NpcDialogOperation : QuestOperation
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("quest_id")] protected int? questId;

    public override void DoOperate(QuestEnv env)
    {
        Player player = env.GetPlayer();
        VisibleObject obj = env.GetVisibleObject();
        int qId = env.GetQuestId();
        if (questId != null)
            qId = questId.Value;
        if (qId == 0)
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(obj.GetObjectId(), id));
        else
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(obj.GetObjectId(), id, qId));
    }
}
