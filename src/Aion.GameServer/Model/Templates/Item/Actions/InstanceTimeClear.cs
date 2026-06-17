using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/InstanceTimeClear.</summary>
[XmlType("InstanceTimeClear")]
public class InstanceTimeClear : AbstractItemAction
{
    [XmlIgnore] private List<int> syncIds;

    // Java parity: @XmlAttribute List<Integer> sync_ids — space-separated.
    [XmlAttribute("sync_ids")]
    public string SyncIdsXml
    {
        get => syncIds == null ? null : string.Join(" ", syncIds);
        set
        {
            if (value == null) { syncIds = null; return; }
            string[] parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            syncIds = new List<int>(parts.Length);
            foreach (string p in parts)
                syncIds.Add(int.Parse(p));
        }
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int syncId = (int)@params[0];
        if (!syncIds.Contains(syncId))
            return false;
        int worldId = DataManager.INSTANCE_COOLTIME_DATA.GetWorldId(syncId);
        Aion.GameServer.Model.GameObjects.Players.PortalCooldown portalCooldown = player.GetPortalCooldownList().GetPortalCooldown(worldId);
        if (portalCooldown == null || (portalCooldown.GetReuseTime() < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() && portalCooldown.GetEnterCount() == 0))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_CANT_INSTANCE_COOL_TIME_INIT());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int syncId = (int)@params[0];
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 1000, 0, 0));

        ItemUseObserver observer = new InstanceTimeClearUseObserver(player, parentItem);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            if (parentItem.GetActivationCount() > 1)
            {
                parentItem.SetActivationCount(parentItem.GetActivationCount() - 1);
            }
            else
            {
                player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1);
            }

            int worldId = DataManager.INSTANCE_COOLTIME_DATA.GetWorldId(syncId);
            Aion.GameServer.Model.GameObjects.Players.PortalCooldown portalCD = player.GetPortalCooldownList().GetPortalCooldown(worldId);
            if (portalCD == null || portalCD.GetEnterCount() < 1)
                return ValueTask.CompletedTask; // don't spam with not needed packets!

            portalCD.DecreaseEnterCount();
            if (portalCD.GetEnterCount() < 1)
                player.GetPortalCooldownList().RemovePortalCooldown(worldId);

            player.GetPortalCooldownList().SendEntryInfo(worldId);
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 0));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(1000)));
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class InstanceTimeClearUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public InstanceTimeClearUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        public override void Abort()
        {
            // TODO: abort is invalid. Should we abort all or only the last syncid?
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            player.RemoveItemCoolDown(parentItem.GetItemTemplate().GetUseLimits().GetDelayId());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
