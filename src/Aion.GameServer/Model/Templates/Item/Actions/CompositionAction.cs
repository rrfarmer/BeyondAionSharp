using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/CompositionAction.</summary>
[XmlType("CompositionAction")]
public class CompositionAction : AbstractItemAction
{
    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        return false;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
    }

    public bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item tools, Item first, Item second)
    {
        if (!tools.GetItemTemplate().IsCombinationItem())
            return false;

        if (!first.GetItemTemplate().IsEnchantmentStone())
            return false;

        if (!second.GetItemTemplate().IsEnchantmentStone())
            return false;

        if (first.GetItemCount() < 1 || second.GetItemCount() < 1)
            return false;

        if (first.GetItemTemplate().GetLevel() > 95 || second.GetItemTemplate().GetLevel() > 95)
            return false;

        return true;
    }

    public void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item tools, Item first, Item second)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), tools.GetObjectId(), tools.GetItemTemplate().GetTemplateId(), 5000, 0, 0));
        player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);

        ItemUseObserver observer = new CompositionUseObserver(player, tools);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            bool result = player.GetInventory().DecreaseByItemId(tools.GetItemId(), 1);
            bool result1 = player.GetInventory().DecreaseByItemId(first.GetItemId(), 1);
            bool result2 = player.GetInventory().DecreaseByItemId(second.GetItemId(), 1);
            if (result && result1 && result2)
            {
                Aion.GameServer.Services.Items.ItemService.AddItem(player, GetItemId(CalcLevel(first.GetItemTemplate().GetLevel(), second.GetItemTemplate().GetLevel())), 1);
            }
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), tools.GetObjectId(), tools.GetItemTemplate().GetTemplateId(), 0, 1, 0));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000)));
    }

    private int CalcLevel(int first, int second)
    {
        int value = ((first + second) / 2);
        if (value < 11)
        {
            value = Aion.GameServer.Commons.Utils.Rnd.Get(1, 20);
        }
        else
        {
            int random = Aion.GameServer.Commons.Utils.Rnd.Get(1, 10);
            int bit = Aion.GameServer.Commons.Utils.Rnd.Get(0, 1);
            value = (bit == 0 ? value - random : value + random);
        }
        return value;
    }

    public int GetItemId(int value)
    {
        return 166000000 + value;
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class CompositionUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item tools;

        public CompositionUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item tools)
        {
            this.player = player;
            this.tools = tools;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), tools.GetObjectId(), tools.GetItemTemplate().GetTemplateId(), 0, 2, 0));
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
