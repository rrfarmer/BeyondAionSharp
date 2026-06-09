using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/MultiReturnAction.</summary>
[XmlType("MultiReturnAction")]
public class MultiReturnAction : AbstractItemAction
{
    [XmlAttribute("id")] protected int id;

    [XmlIgnore] private const short USAGE_DELAY = 5000;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Player.Player player, Item item, Item targetItem, params object[] @params)
    {
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Player.Player player, Item item, Item targetItem, params object[] @params)
    {
        int indexReturn = (int)@params[0];
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), item.GetObjectId(), item.GetItemId(), USAGE_DELAY, 0, 0), true);

        ItemUseObserver observer = new MultiReturnUseObserver(player, item);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            Aion.GameServer.Model.Templates.Item.ReturnLocList loc = DataManager.MULTIRETURN_DATA.GetReturnLocListById(id)[indexReturn];
            if (loc != null && loc.GetAlias() != null && loc.GetWorldid() > 0)
            {
                if (!player.GetInventory().DecreaseByObjectId(item.GetObjectId(), 1))
                {
                    observer.Abort();
                    return ValueTask.CompletedTask;
                }
                player.GetObserveController().RemoveObserver(observer);
                Aion.GameServer.Services.Teleport.TeleportService.UseTeleportScroll(player, loc.GetAlias().ToUpperInvariant(), loc.GetWorldid());
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_USE_ITEM(item.GetL10n()));
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(USAGE_DELAY)));
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class MultiReturnUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Player.Player player;
        private readonly Item item;

        public MultiReturnUseObserver(Aion.GameServer.Model.GameObjects.Player.Player player, Item item)
        {
            this.player = player;
            this.item = item;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            player.RemoveItemCoolDown(item.GetItemTemplate().GetUseLimits().GetDelayId());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), item.GetObjectId(), item.GetItemId(), 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
