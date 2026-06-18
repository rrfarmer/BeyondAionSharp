using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Decompose (Neon). Opens decomposable items.</summary>
public class Decompose : PlayerCommand
{
    public Decompose()
        : base("decompose", "Opens decomposable items.")
    {
        SetSyntaxInfo("<item> [count] - Decomposes the specified item (default: all, optional: number of items to decompose).");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        Item item = player.GetInventory().GetFirstItemByItemId(ChatUtil.GetItemId(paramsArr[0]));
        if (item == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_NO_TARGET_ITEM());
            return;
        }
        long count = paramsArr.Length == 1 ? long.MaxValue : long.Parse(paramsArr[1]);

        ItemActions itemActions = item.GetItemTemplate().GetActions();
        DecomposeAction decomposeAction = itemActions == null ? null
            : itemActions.GetItemActions().OfType<DecomposeAction>().FirstOrDefault();
        if (decomposeAction == null || DataManager.DECOMPOSABLE_ITEMS_DATA.GetSelectableItems(item.GetItemId()) != null) // exclude selectable decomposables
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_IT_CAN_NOT_BE_DECOMPOSED(item.GetItemTemplate().GetL10n()));
            return;
        }

        if (!decomposeAction.CanAct(player, item, item))
            return;

        StartTask(player, item.GetItemId(), count, decomposeAction);
    }

    private void StartTask(Player player, int itemId, long count, DecomposeAction decomposeAction)
    {
        DecomposeTask task = new DecomposeTask(this, player, itemId, count, decomposeAction);
        task.Schedule();
    }

    private void CancelTask(Player player, ItemUseObserver observer, string message)
    {
        player.GetController().CancelTask(Aion.GameServer.Model.TaskId.SKILL_USE);
        player.GetObserveController().RemoveObserver(observer);
        SendInfo(player, message);
    }

    // Java parity: the anonymous Runnable + ItemUseObserver instance state captured inside startTask().
    private sealed class DecomposeTask
    {
        private readonly Decompose outer;
        private readonly Player player;
        private readonly int itemId;
        private readonly DecomposeAction decomposeAction;

        private long remainingCount;
        private long totalCount;
        private readonly ItemUseObserver observer;

        public DecomposeTask(Decompose outer, Player player, int itemId, long count, DecomposeAction decomposeAction)
        {
            this.outer = outer;
            this.player = player;
            this.itemId = itemId;
            this.decomposeAction = decomposeAction;
            this.remainingCount = count;
            this.totalCount = 0;

            // use observer to abort task on move, attack, die, item use, etc.
            this.observer = new DecomposeObserver(this);
            player.GetObserveController().AddObserver(observer);
        }

        public void Schedule()
        {
            player.GetController().AddTask(Aion.GameServer.Model.TaskId.SKILL_USE,
                Aion.GameServer.Utils.ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
                    _ =>
                    {
                        Run();
                        return ValueTask.CompletedTask;
                    }, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(DecomposeAction.USAGE_DELAY + 100)));
        }

        public void Abort()
        {
            outer.CancelTask(player, observer, "Decomposing aborted: Processed " + Math.Max(0, totalCount - 1) + "x " + ChatUtil.Item(itemId) + ".");
        }

        private void Run()
        {
            Item item = player.GetInventory().GetFirstItemByItemId(itemId);
            remainingCount = Math.Min(player.GetInventory().GetItemCountByItemId(itemId), remainingCount);
            if (item == null || remainingCount <= 0)
            {
                outer.CancelTask(player, observer, "Decomposing finished: Processed " + totalCount + "x " + ChatUtil.Item(itemId) + ".");
                return;
            }

            if (!decomposeAction.CanAct(player, item, item))
            {
                outer.CancelTask(player, observer, "Decomposing aborted: Processed " + totalCount + "x " + ChatUtil.Item(itemId) + ".");
                return;
            }

            player.GetObserveController().NotifyItemuseObservers(item); // cancel hide
            decomposeAction.Act(player, item, item);
            remainingCount--;
            totalCount++;
        }

        private sealed class DecomposeObserver : ItemUseObserver
        {
            private readonly DecomposeTask task;

            public DecomposeObserver(DecomposeTask task)
            {
                this.task = task;
            }

            public override void Itemused(Item item)
            {
                if (item.GetItemId() != task.itemId)
                    Abort();
            }

            public override void Abort()
            {
                task.Abort();
            }
        }
    }
}
