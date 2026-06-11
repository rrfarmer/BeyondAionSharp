using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/QuestStartAction.</summary>
[XmlType("QuestStartAction")]
public class QuestStartAction : AbstractItemAction
{
    [XmlAttribute("questid")] protected int questid;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.QuestEngine.Model.QuestState qs = player.GetQuestStateList().GetQuestState(questid);
        if (qs == null || qs.IsStartable())
            return true;
        else if (qs.GetStatus() != Aion.GameServer.QuestEngine.Model.QuestStatus.COMPLETE)
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_QUEST_ACQUIRE_ERROR_WORKING_QUEST());
        else if (!qs.CanRepeat())
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_QUEST_ACQUIRE_ERROR_NONE_REPEATABLE(DataManager.QUEST_DATA.GetQuestById(questid).GetName()));

        return false;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId()));
        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnDialog(new Aion.GameServer.QuestEngine.Model.QuestEnv(null, player, questid, Aion.GameServer.Model.DialogAction.ASK_QUEST_ACCEPT));
    }
}
