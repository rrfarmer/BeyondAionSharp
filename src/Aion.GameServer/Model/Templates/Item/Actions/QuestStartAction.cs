using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/QuestStartAction.</summary>
[XmlType("QuestStartAction")]
public class QuestStartAction : AbstractItemAction
{
    [XmlAttribute("questid")] public int questid;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // Retail always plays the cast; eligibility is only checked afterwards, in FinishUse()
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int castingDelay = parentItem.GetItemTemplate().GetCastingDelay();
        if (castingDelay <= 0)
        {
            FinishUse(player, parentItem);
            return;
        }
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), castingDelay, 0, 1), true);
        var observer = new QuestStartUseObserver(player, parentItem);

        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, parentItem);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(castingDelay)));
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class QuestStartUseObserver : Aion.GameServer.Controllers.Observer.ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public QuestStartUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0), true);
        }
    }

    private void FinishUse(Aion.GameServer.Model.GameObjects.Players.Player player, Item item)
    {
        player.StartCooldown(item);
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), item.GetItemId()));

        // retail stays silent when the quest is already active or cannot be repeated (anymore), but warns about
        // race/level/etc. restrictions before sending the use message (confirmed on retail 5.8)
        Aion.GameServer.QuestEngine.Model.QuestState qs = player.GetQuestStateList().GetQuestState(questid);
        bool canStart = (qs == null || qs.IsStartable()) && Aion.GameServer.Services.QuestService.CheckStartConditions(player, questid, true, 0, true, true, false);

        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(item.GetL10n()));

        if (!canStart)
            return; // quest not startable, or requirements not met (checkStartConditions already sent the message)

        // CM_USE_ITEM skips onItemUseEvent for QuestStartAction items so it doesn't fire before the cast; call it
        // here instead, falling back to the generic dialog routing if the item isn't a registered quest item
        var env = new Aion.GameServer.QuestEngine.Model.QuestEnv(null, player, questid, Aion.GameServer.Model.DialogAction.ASK_QUEST_ACCEPT);
        Aion.GameServer.QuestEngine.Handlers.HandlerResult result = Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnItemUseEvent(env, item);
        if (result != Aion.GameServer.QuestEngine.Handlers.HandlerResult.SUCCESS)
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnDialog(new Aion.GameServer.QuestEngine.Model.QuestEnv(null, player, questid, Aion.GameServer.Model.DialogAction.ASK_QUEST_ACCEPT));
    }
}
