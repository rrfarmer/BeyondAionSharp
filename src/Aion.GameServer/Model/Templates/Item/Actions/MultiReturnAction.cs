using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/MultiReturnAction.</summary>
[XmlType("MultiReturnAction")]
public class MultiReturnAction : AbstractItemAction
{
    [XmlAttribute("id")] public int id;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item item, Item targetItem, params object[] @params)
    {
        return GetReturnLoc((int)@params[0]) != null;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item item, Item targetItem, params object[] @params)
    {
        int castingDelay = player.GetItemUseDelay(item.GetItemTemplate().GetCastingDelay());
        Aion.GameServer.Model.Templates.Items.ReturnLocList loc = GetReturnLoc((int)@params[0]);
        if (castingDelay <= 0)
        {
            FinishUse(player, item, loc);
            return;
        }
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), item.GetItemId(), castingDelay, ItemUseAnimation.USE_START), true);

        ItemUseObserver observer = new MultiReturnUseObserver(player, item);
        player.GetObserveController().AddObserver(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, item, loc);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(castingDelay)));
    }

    private void FinishUse(Aion.GameServer.Model.GameObjects.Players.Player player, Item item, Aion.GameServer.Model.Templates.Items.ReturnLocList loc)
    {
        if (!player.GetInventory().DecreaseByObjectId(item.GetObjectId(), 1))
            return;
        player.StartCooldown(item);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(item.GetL10n()));
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), item.GetItemId(), 0, ItemUseAnimation.USE_SUCCESS), true);
        Aion.GameServer.Services.Teleport.TeleportService.UseTeleportScroll(player, loc.GetAlias().ToUpperInvariant(), loc.GetWorldid());
    }

    private Aion.GameServer.Model.Templates.Items.ReturnLocList GetReturnLoc(int index)
    {
        var locs = DataManager.MULTIRETURN_DATA.GetReturnLocListById(id);
        if (locs == null || index < 0 || index >= locs.Count)
            return null;
        Aion.GameServer.Model.Templates.Items.ReturnLocList loc = locs[index];
        return loc != null && loc.GetAlias() != null && loc.GetWorldid() > 0 ? loc : null;
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class MultiReturnUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item item;

        public MultiReturnUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item item)
            : base(player)
        {
            this.player = player;
            this.item = item;
        }

        protected override void OnAbort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), item.GetItemId(), 0, ItemUseAnimation.USE_CANCEL), true);
        }
    }
}
