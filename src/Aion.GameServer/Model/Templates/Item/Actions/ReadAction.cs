using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/ReadAction.</summary>
[XmlType("ReadAction")]
public class ReadAction : AbstractItemAction
{
    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // items combining <queststart> with <read> get their "used" message and usage animation from QuestStartAction already
        if (parentItem.GetItemTemplate().GetActions().GetItemActions().Any(a => a is QuestStartAction))
            return;

        int castingDelay = parentItem.GetItemTemplate().GetCastingDelay();
        if (castingDelay <= 0)
        {
            FinishUse(player, parentItem);
            return;
        }

        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), castingDelay, 0, 0),
            true);
        var observer = new ReadUseObserver(player, parentItem);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, parentItem);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(castingDelay)));
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class ReadUseObserver : Aion.GameServer.Controllers.Observer.ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public ReadUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        public override void Abort()
        {
            player.GetController().CancelUseItem(false);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0),
                true);
            player.GetObserveController().RemoveObserver(this);
        }
    }

    private void FinishUse(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
    {
        player.StartCooldown(parentItem);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(parentItem.GetL10n()));
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 1, 0), true);
    }
}
