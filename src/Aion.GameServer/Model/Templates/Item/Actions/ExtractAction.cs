using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/ExtractAction.</summary>
[XmlType("ExtractAction")]
public class ExtractAction : AbstractItemAction
{
    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (targetItem == null)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_NO_TARGET_ITEM());
            return false;
        }
        if (!targetItem.GetItemTemplate().IsArmor() && !targetItem.GetItemTemplate().IsWeapon())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_IT_CAN_NOT_BE_DECOMPOSED(targetItem.GetL10n()));
            return false;
        }
        if (targetItem.IsEquipped())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_EQUIP_ITEM_CAN_NOT_BE_DECOMPOSED());
            return false;
        }

        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 5000, 0, 0));
        player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
        ItemUseObserver observer = new ExtractUseObserver(player, parentItem, targetItem);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            bool result = Aion.GameServer.Services.EnchantService.BreakItem(player, targetItem, parentItem);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, result ? 1 : 2, 0));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000)));
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class ExtractUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;
        private readonly Item targetItem;

        public ExtractUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem)
        {
            this.player = player;
            this.parentItem = parentItem;
            this.targetItem = targetItem;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_CANCELED(targetItem.GetL10n()));
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0));
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
