using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/AssemblyItemAction.</summary>
[XmlType("AssemblyItemAction")]
public class AssemblyItemAction : AbstractItemAction
{
    [XmlAttribute("item")] public int item;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Model.Templates.Items.AssemblyItem assemblyItem = GetAssemblyItem();
        if (assemblyItem == null)
        {
            return false;
        }
        foreach (int itemId in assemblyItem.GetParts())
        {
            if (player.GetInventory().GetFirstItemByItemId(itemId) == null)
            {
                return false;
            }
        }
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
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), castingDelay, 0, 0), true);
        ItemUseObserver observer = new AssemblyUseObserver(player, parentItem);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, parentItem);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(castingDelay)));
    }

    private void FinishUse(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
    {
        Aion.GameServer.Model.Templates.Items.AssemblyItem assemblyItem = GetAssemblyItem();
        var requiredCounts = new System.Collections.Generic.Dictionary<int, long>();
        foreach (int partId in assemblyItem.GetParts())
            requiredCounts[partId] = requiredCounts.TryGetValue(partId, out long c) ? c + 1 : 1;
        foreach (var requiredCount in requiredCounts)
        {
            if (player.GetInventory().GetItemCountByItemId(requiredCount.Key) < requiredCount.Value)
            {
                Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0), true);
                return;
            }
        }
        player.StartCooldown(parentItem);
        foreach (int itemId in assemblyItem.GetParts())
        {
            player.GetInventory().DecreaseByItemId(itemId, 1);
        }
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 1, 0), true);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(parentItem.GetL10n()));
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ASSEMBLY_ITEM_SUCCEEDED());
        Aion.GameServer.Services.Items.ItemService.AddItem(player, assemblyItem.GetId(), 1);
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class AssemblyUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public AssemblyUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        public override void Abort()
        {
            player.GetController().CancelUseItem(false);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ASSEMBLY_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }

    public Aion.GameServer.Model.Templates.Items.AssemblyItem GetAssemblyItem()
    {
        return DataManager.ASSEMBLY_ITEM_DATA.GetAssemblyItem(item);
    }
}
