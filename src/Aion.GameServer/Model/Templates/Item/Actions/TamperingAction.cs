using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/TamperingAction.</summary>
public class TamperingAction : AbstractItemAction
{
    private static readonly ILogger log = NullLogger.Instance;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int maxTemp = targetItem.GetItemTemplate().GetMaxTampering();
        if (!(maxTemp > 0) || targetItem.GetTempering() >= maxTemp)
        {
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int parentItemId = parentItem.GetItemId();
        int parntObjectId = parentItem.GetObjectId();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItemId, 5000, 0, 0), true);
        ItemUseObserver observer = new TamperUseObserver(player, parentItem, targetItem, parentItemId, parntObjectId);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);

            if (player.GetInventory().GetItemByObjId(targetItem.GetObjectId()) == null && !targetItem.IsEquipped())
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ENCHANT_ITEM_NO_TARGET_ITEM());
                Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parntObjectId, parentItemId, 0, 2, 0));
                return ValueTask.CompletedTask;
            }

            if (!player.GetInventory().DecreaseByObjectId(parntObjectId, 1))
            {
                Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parntObjectId, parentItemId, 0, 2, 0));
                return ValueTask.CompletedTask;
            }

            int maxTemp = targetItem.GetItemTemplate().GetMaxTampering();
            if (targetItem.GetTempering() < maxTemp)
            {
                float temperingChance = CalculateChance(player, targetItem);
                if (Aion.GameServer.Commons.Utils.Rnd.Chance() < temperingChance)
                {
                    SetTemperingLevel(targetItem, player, targetItem.GetTempering() + 1);
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_AUTHORIZE_SUCCEEDED(targetItem.GetL10n(), targetItem.GetTempering()));
                    Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parntObjectId, parentItemId, 0, 1, 0));

                    if (Aion.GameServer.Configs.Main.CustomConfig.ENABLE_ENCHANT_ANNOUNCE && targetItem.GetTempering() == 10)
                    {
                        Aion.GameServer.Utils.PacketSendUtility.BroadcastToWorld(
                            Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_AUTHORIZE_SUCCEEDED_MAX(player.GetName(), targetItem.GetItemTemplate().GetL10n(), targetItem.GetTempering()),
                            Aion.GameServer.Utils.Collections.Predicates.Players.SameRace(player));
                    }

                    if (Aion.GameServer.Configs.Main.LoggingConfig.LOG_TAMPERING)
                        log.LogInformation("Player " + player.GetName() + " successfully tampered item " + targetItem.GetItemId() + "(" + targetItem.GetObjectId() + ") to level " + targetItem.GetTempering());
                }
                else
                {
                    SetTemperingLevel(targetItem, player, 0);
                    if (targetItem.GetItemTemplate().GetItemGroup() == ItemGroup.PLUME)
                    {
                        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_AUTHORIZE_FAILED_TSHIRT(targetItem.GetL10n()));
                        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parntObjectId, parentItemId, 0, 2, 0));
                        if (targetItem.IsEquipped())
                            player.GetEquipment().DecreaseEquippedItemCount(targetItem.GetObjectId(), 1);
                        else
                            player.GetInventory().DecreaseByObjectId(targetItem.GetObjectId(), 1);
                    }
                    else
                    {
                        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_AUTHORIZE_FAILED(targetItem.GetL10n()));
                        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parntObjectId, parentItemId, 0, 2, 0));
                    }

                    if (Aion.GameServer.Configs.Main.LoggingConfig.LOG_TAMPERING)
                        log.LogInformation("Player " + player.GetName() + " failed to tamper item " + targetItem.GetItemId() + "(" + targetItem.GetObjectId() + ").");
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000)));
    }

    public static void SetTemperingLevel(Item item, Aion.GameServer.Model.GameObjects.Players.Player player, int temperingLevel)
    {
        int oldTemperingLevel = item.GetTempering();
        item.SetTempering(temperingLevel);
        if (item.GetItemTemplate().GetItemGroup() == ItemGroup.PLUME)
        {
            if (item.GetTempering() > 4)
            {
                int rndBonusValue = item.GetRndPlumeBonusValue();
                for (int i = oldTemperingLevel; i < item.GetTempering(); i++) // Random chance to get 4-7 ATK/20-32 MBoost
                    rndBonusValue += item.GetItemTemplate().GetTemperingName().Equals("TSHIRT_PHYSICAL") ? Aion.GameServer.Commons.Utils.Rnd.Get(0, 3) : Aion.GameServer.Commons.Utils.Rnd.Get(0, 12);
                item.SetRndPlumeBonusValue(rndBonusValue);
            }
            else
            {
                item.SetRndPlumeBonusValue(0);
            }
        }
        if (item.GetTemperingEffect() != null)
        {
            item.GetTemperingEffect().EndEffect(player);
            item.SetTemperingEffect(null);
        }
        if (item.IsEquipped() && item.GetTempering() > 0)
            Aion.GameServer.Model.Enchants.TemperingEffect.Apply(player, item);

        Aion.GameServer.Services.Items.ItemPacketService.UpdateItemAfterInfoChange(player, item, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType.STATS_CHANGE);
        if (item.IsEquipped())
            player.GetEquipment().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        else
            player.GetInventory().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    private float CalculateChance(Aion.GameServer.Model.GameObjects.Players.Player player, Item item)
    {
        if (item.GetTempering() == 0) // +0 -> +1 is always safe
            return 100;
        if (item.GetItemTemplate().GetItemGroup() == ItemGroup.PLUME)
            return Math.Max(25, 100 - (item.GetTempering() * 10));
        return Aion.GameServer.Model.GameObjects.Players.Rates.Get(player, Aion.GameServer.Configs.Main.RatesConfig.TEMPERING_CHANCES);
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class TamperUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;
        private readonly Item targetItem;
        private readonly int parentItemId;
        private readonly int parntObjectId;

        public TamperUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, int parentItemId, int parntObjectId)
        {
            this.player = player;
            this.parentItem = parentItem;
            this.targetItem = targetItem;
            this.parentItemId = parentItemId;
            this.parntObjectId = parntObjectId;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            player.RemoveItemCoolDown(parentItem.GetItemTemplate().GetUseLimits().GetDelayId());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_AUTHORIZE_CANCEL(targetItem.GetL10n()));
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parntObjectId, parentItemId, 0, 3, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
