using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/ExpExtractAction.</summary>
public class ExpExtractAction : AbstractItemAction
{
    [XmlAttribute("cost")] public long cost;
    [XmlAttribute("percent")] public bool isPercent;
    [XmlAttribute("item_id")] public int itemId;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Model.GameObjects.Players.PlayerCommonData cd = player.GetCommonData();
        long newExp = cd.GetExp() - GetRequiredExp(cd);
        return CanExtractExp(player, newExp);
    }

    private bool CanExtractExp(Aion.GameServer.Model.GameObjects.Players.Player player, long newExp)
    {
        if (player.GetInventory().IsFull())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_DECOMPRESS_INVENTORY_IS_FULL());
            return false;
        }
        if (newExp < DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(player.GetLevel()))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_EXP_EXTRACTION_USE_NOT_ENOUGH_EXP());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 5000, 0, 0));

        player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);

        ItemUseObserver observer = new ExpExtractUseObserver(player, parentItem);
        player.GetObserveController().Attach(observer);

        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);

            Aion.GameServer.Model.GameObjects.Players.PlayerCommonData cd = player.GetCommonData();
            long requiredExp = GetRequiredExp(cd);
            long newExp = cd.GetExp() - requiredExp;
            if (!CanExtractExp(player, newExp) || !player.GetInventory().DecreaseByItemId(parentItem.GetItemId(), 1))
            {
                player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                    new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0));
                return ValueTask.CompletedTask;
            }

            cd.SetExp(newExp);
            Aion.GameServer.Services.Items.ItemService.AddItem(player, itemId, 1);
            string rewardItem = DataManager.ITEM_DATA.GetItemTemplate(itemId).GetL10n();
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_EXP_EXTRACTION_USE(parentItem.GetL10n(), requiredExp, rewardItem));
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 1, 0));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000)));
    }

    private long GetRequiredExp(Aion.GameServer.Model.GameObjects.Players.PlayerCommonData cd)
    {
        if (isPercent)
        {
            return Math.Max(1, cd.GetExpNeed() * cost / 100L);
        }
        return cost;
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class ExpExtractUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public ExpExtractUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_CANCELED(parentItem.GetL10n()));
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0));
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
