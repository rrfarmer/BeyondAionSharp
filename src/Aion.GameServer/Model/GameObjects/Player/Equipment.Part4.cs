using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/Equipment — partial #4 (Java ~699-802): soulBindItem (with
/// RequestResponseHandler/ActionObserver anonymous classes → private nested named classes), rank-limit checks.
/// </summary>
public partial class Equipment
{
    private bool SoulBindItem(Player player, Item item, long slot)
    {
        if (player.GetInventory().GetItemByObjId(item.GetObjectId()) == null)
            return false;
        if (player.IsDead())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_INVALID_STANCE(Aion.GameServer.Utils.ChatUtil.L10n(1400059)));
            return false;
        }
        else if (player.IsInPlayerMode(Aion.GameServer.Model.Actions.PlayerMode.RIDE))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_INVALID_STANCE(Aion.GameServer.Utils.ChatUtil.L10n(1400056)));
            return false;
        }
        else if (player.IsInState(CreatureState.Chair))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_INVALID_STANCE(Aion.GameServer.Utils.ChatUtil.L10n(1400058)));
            return false;
        }
        else if (player.IsInState(CreatureState.Resting))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_INVALID_STANCE(Aion.GameServer.Utils.ChatUtil.L10n(1400057)));
            return false;
        }
        else if (player.IsInState(CreatureState.Gliding))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_INVALID_STANCE(Aion.GameServer.Utils.ChatUtil.L10n(1400082)));
            return false;
        }
        else if (player.IsInState(CreatureState.Flying))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_INVALID_STANCE(Aion.GameServer.Utils.ChatUtil.L10n(1400055)));
            return false;
        }
        else if (player.IsInState(CreatureState.WeaponEquipped))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_INVALID_STANCE(Aion.GameServer.Utils.ChatUtil.L10n(1400079)));
            return false;
        }

        Aion.GameServer.Model.GameObjects.Players.RequestResponseHandler<Player> responseHandler = new SoulBindResponseHandler(player, this, item, slot);

        bool requested = player.GetResponseRequester().PutRequest(Aion.GameServer.Network.Aion.ServerPackets.SmQuestionWindow.STR_SOUL_BOUND_ITEM_DO_YOU_WANT_SOUL_BOUND, responseHandler);
        if (requested)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmQuestionWindow(Aion.GameServer.Network.Aion.ServerPackets.SmQuestionWindow.STR_SOUL_BOUND_ITEM_DO_YOU_WANT_SOUL_BOUND, 0, 0, item.GetL10n()));
        }
        else
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_CLOSE_OTHER_MSG_BOX_AND_RETRY());
        }
        return false;
    }

    // Java parity: anonymous RequestResponseHandler<Player> in soulBindItem.
    private sealed class SoulBindResponseHandler : Aion.GameServer.Model.GameObjects.Players.RequestResponseHandler<Player>
    {
        private readonly Equipment eq;
        private readonly Item item;
        private readonly long slot;

        public SoulBindResponseHandler(Player player, Equipment eq, Item item, long slot) : base(player)
        {
            this.eq = eq;
            this.item = item;
            this.slot = slot;
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            responder.GetController().CancelUseItem();

            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(responder,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(responder.GetObjectId(), item.GetObjectId(), item.GetItemId(), 5000, 4), true);

            responder.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);

            ActionObserver moveObserver = new SoulBindMoveObserver(responder, item);
            responder.GetObserveController().Attach(moveObserver);

            // item usage animation
            responder.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                responder.GetObserveController().RemoveObserver(moveObserver);

                Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(responder,
                    new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(responder.GetObjectId(), item.GetObjectId(), item.GetItemId(), 0, 6), true);
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(responder, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_ITEM_SUCCEED(item.GetL10n()));

                item.SetSoulBound(true);
                Aion.GameServer.Services.Item.ItemPacketService.UpdateItemAfterInfoChange(eq.owner, item);

                eq.Equip(slot, item);
                Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(responder, new Aion.GameServer.Network.Aion.ServerPackets.SmUpdatePlayerAppearance(responder.GetObjectId(), eq.GetEquippedForAppearance()), true);
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(5000)));
        }

        public override void DenyRequest(Player requester, Player responder)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(responder, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_ITEM_CANCELED(item.GetL10n()));
        }
    }

    // Java parity: anonymous ActionObserver(MOVE) in soulBindItem's acceptRequest.
    private sealed class SoulBindMoveObserver : ActionObserver
    {
        private readonly Player responder;
        private readonly Item item;

        public SoulBindMoveObserver(Player responder, Item item) : base(ObserverType.MOVE)
        {
            this.responder = responder;
            this.item = item;
        }

        public override void Moved()
        {
            responder.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(responder, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SOUL_BOUND_ITEM_CANCELED(item.GetL10n()));
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(responder,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(responder.GetObjectId(), item.GetObjectId(), item.GetItemId(), 0, 8), true);
        }
    }

    private bool VerifyRankLimits(Item item)
    {
        int rank = owner.GetAbyssRank().GetRank().GetId();
        if (!item.GetItemTemplate().GetUseLimits().VerifyRank(rank))
            return false;
        if (item.GetFusionedItemTemplate() != null)
            return item.GetFusionedItemTemplate().GetUseLimits().VerifyRank(rank);
        return true;
    }

    public void CheckRankLimitItems()
    {
        foreach (Item item in GetEquippedItems())
        {
            if (!VerifyRankLimits(item))
            {
                UnEquipItem(item.GetObjectId(), false);
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_UNEQUIP_RANKITEM(item.GetL10n()));
                // TODO: Check retail what happens with full inv and the task msgs.
            }
        }
    }
}
