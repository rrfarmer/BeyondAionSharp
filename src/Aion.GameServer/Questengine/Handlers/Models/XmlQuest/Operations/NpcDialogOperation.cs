using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/NpcDialogOperation. Nullable Integer quest_id→int?.</summary>
[XmlType("NpcDialogOperation")]
public class NpcDialogOperation : QuestOperation
{
    [XmlAttribute("id")] public int id;

    // Nullable Integer quest_id: XmlSerializer rejects [XmlAttribute] on Nullable<int>, so bind via a string proxy.
    public int? questId;

    [XmlAttribute("quest_id")]
    public string QuestIdRaw
    {
        get => questId?.ToString();
        set => questId = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value);
    }

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
