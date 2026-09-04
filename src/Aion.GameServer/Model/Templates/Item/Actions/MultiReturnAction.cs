using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/MultiReturnAction.</summary>
[XmlType("MultiReturnAction")]
public class MultiReturnAction : AbstractItemAction
{
    [XmlAttribute("id")] public int id;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item item, Item targetItem, params object[] @params)
    {
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item item, Item targetItem, params object[] @params)
    {
        int castingDelay = item.GetItemTemplate().GetCastingDelay();
        int indexReturn = (int)@params[0];
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), item.GetItemId(), castingDelay, 0, 0), true);

        ItemUseObserver observer = new MultiReturnUseObserver(player, item);
        if (castingDelay <= 0)
        {
            FinishUse(player, item, observer, indexReturn);
            return;
        }

        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, item, observer, indexReturn);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(castingDelay)));
    }

    private void FinishUse(Aion.GameServer.Model.GameObjects.Players.Player player, Item item, ItemUseObserver observer, int indexReturn)
    {
        Aion.GameServer.Model.Templates.Items.ReturnLocList loc = DataManager.MULTIRETURN_DATA.GetReturnLocListById(id)[indexReturn];
        if (loc != null && loc.GetAlias() != null && loc.GetWorldid() > 0)
        {
            if (!player.GetInventory().DecreaseByObjectId(item.GetObjectId(), 1))
            {
                observer.Abort();
                return;
            }
            player.StartCooldown(item);
            Aion.GameServer.Services.Teleport.TeleportService.UseTeleportScroll(player, loc.GetAlias().ToUpperInvariant(), loc.GetWorldid());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(item.GetL10n()));
        }
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class MultiReturnUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item item;

        public MultiReturnUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item item)
        {
            this.player = player;
            this.item = item;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), item.GetItemId(), 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
