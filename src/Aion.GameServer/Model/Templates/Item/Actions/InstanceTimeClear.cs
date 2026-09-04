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

    [XmlAttribute("recovery_instance_count")] public int recoveryInstanceCount = 1;

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
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_COOL_TIME_INIT());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int castingDelay = parentItem.GetItemTemplate().GetCastingDelay();
        int syncId = (int)@params[0];
        if (castingDelay <= 0)
        {
            FinishUse(player, parentItem, syncId);
            return;
        }
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), castingDelay, 0, 0));

        ItemUseObserver observer = new InstanceTimeClearUseObserver(player, parentItem);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, parentItem, syncId);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(castingDelay)));
    }

    private void FinishUse(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, int syncId)
    {
        int worldId = DataManager.INSTANCE_COOLTIME_DATA.GetWorldId(syncId);

        if (parentItem.GetActivationCount() > 1)
        {
            if (player.GetInventory().GetItemByObjId(parentItem.GetObjectId()) == null)
                return; // item was traded or sold during the casting delay
            parentItem.SetActivationCount(parentItem.GetActivationCount() - 1);
        }
        else if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
            return;

        player.StartCooldown(parentItem);

        Aion.GameServer.Model.GameObjects.Players.PortalCooldown portalCD = player.GetPortalCooldownList().GetOrCreatePortalCooldown(worldId);
        if (portalCD != null)
        {
            portalCD.DecreaseEnterCount(recoveryInstanceCount);
            player.GetPortalCooldownList().SendEntryInfo(worldId);
        }
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(parentItem.GetL10n()));
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 0));
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
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
