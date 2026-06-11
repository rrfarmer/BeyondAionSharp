using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/TuningAction.</summary>
[XmlType("TuningAction")]
public class TuningAction : AbstractItemAction
{
    [XmlAttribute("target")] private UseTarget target;
    [XmlAttribute("no_reduce")] private bool shouldNotReduceTuneCount;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (targetItem.IsEquipped())
            return false;
        if (!targetItem.IsIdentified())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_REIDENTIFY_DIDNT_IDENTIFY(targetItem.GetL10n()));
            return false;
        }
        if (!targetItem.GetItemTemplate().CanTune())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_REIDENTIFY_CANNOT_REIDENTIFY(targetItem.GetL10n()));
            return false;
        }
        if (target == UseTarget.WEAPON && !targetItem.GetItemTemplate().IsWeapon()
            || target == UseTarget.ARMOR && !targetItem.GetItemTemplate().IsArmor())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_REIDENTIFY_WRONG_SELECT(parentItem.GetL10n(), targetItem.GetL10n()));
            return false;
        }
        if (targetItem.GetItemTemplate().GetLevel() > parentItem.GetItemTemplate().GetLevel())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_REIDENTIFY_WRONG_LEVEL(parentItem.GetL10n(), targetItem.GetL10n()));
            return false;
        }

        return shouldNotReduceTuneCount || targetItem.GetTuneCount() < targetItem.GetItemTemplate().GetMaxTuneCount();
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int tuningScrollItemId = parentItem.GetItemId();
        int tuningScrollObjectId = parentItem.GetObjectId();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), tuningScrollItemId, 5000, 12, 0), true);
        ItemUseObserver observer = new TuneUseObserver(player, parentItem, targetItem, tuningScrollItemId, tuningScrollObjectId);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), tuningScrollObjectId, tuningScrollItemId, 0, 13, 0), true);
            if (!player.GetInventory().DecreaseByObjectId(tuningScrollObjectId, 1))
                return ValueTask.CompletedTask;

            int newOptionalSockets, newEnchantBonus, newStatBonusId;
            if (shouldNotReduceTuneCount) // only tune attributes (bonus stats)
            {
                newOptionalSockets = targetItem.GetOptionalSockets();
                newEnchantBonus = targetItem.GetEnchantBonus();
            }
            else
            {
                targetItem.SetTuneCount(targetItem.GetTuneCount() + 1);
                player.GetInventory().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                newOptionalSockets = Aion.Commons.Utils.Rnd.Get(0, targetItem.GetItemTemplate().GetOptionSlotBonus());
                newEnchantBonus = Aion.Commons.Utils.Rnd.Get(0, targetItem.GetItemTemplate().GetMaxEnchantBonus());
            }
            newStatBonusId = GetRandomStatBonusIdFor(targetItem);
            Aion.GameServer.Model.Items.PendingTuneResult result = new Aion.GameServer.Model.Items.PendingTuneResult(newOptionalSockets, newEnchantBonus, newStatBonusId, shouldNotReduceTuneCount);
            targetItem.SetPendingTuneResult(result);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmTuneResult(targetItem, tuningScrollItemId, result));
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_REIDENTIFY_SUCCEED(targetItem.GetL10n()));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000)));
    }

    public static int GetRandomStatBonusIdFor(Item item)
    {
        return DataManager.ITEM_RANDOM_BONUSES.SelectRandomBonusNumber(Aion.GameServer.Model.Templates.Items.Bonuses.StatBonusType.INVENTORY, item.GetItemTemplate().GetStatBonusSetId());
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class TuneUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;
        private readonly Item targetItem;
        private readonly int tuningScrollItemId;
        private readonly int tuningScrollObjectId;

        public TuneUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, int tuningScrollItemId, int tuningScrollObjectId)
        {
            this.player = player;
            this.parentItem = parentItem;
            this.targetItem = targetItem;
            this.tuningScrollItemId = tuningScrollItemId;
            this.tuningScrollObjectId = tuningScrollObjectId;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            player.RemoveItemCoolDown(parentItem.GetItemTemplate().GetUseLimits().GetDelayId());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_REIDENTIFY_CANCELED(targetItem.GetL10n()));
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), tuningScrollObjectId, tuningScrollItemId, 0, 14, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
