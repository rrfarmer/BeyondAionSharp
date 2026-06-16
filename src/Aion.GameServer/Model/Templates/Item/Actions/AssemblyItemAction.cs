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
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 1000, 0, 0), true);
        ItemUseObserver observer = new AssemblyUseObserver(player, parentItem);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Model.Templates.Items.AssemblyItem assemblyItem = GetAssemblyItem();
            foreach (int itemId in assemblyItem.GetParts())
            {
                if (!player.GetInventory().DecreaseByItemId(itemId, 1))
                {
                    return ValueTask.CompletedTask;
                }
            }
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 1, 0), true);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ASSEMBLY_ITEM_SUCCEEDED());
            Aion.GameServer.Services.Items.ItemService.AddItem(player, assemblyItem.GetId(), 1);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(1000)));
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
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            player.RemoveItemCoolDown(parentItem.GetItemTemplate().GetUseLimits().GetDelayId());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }

    public Aion.GameServer.Model.Templates.Items.AssemblyItem GetAssemblyItem()
    {
        return DataManager.ASSEMBLY_ITEM_DATA.GetAssemblyItem(item);
    }
}
