using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Drop;

/// <summary>Java parity: services/drop/DropDistributionService (xTz, Sykra). Singleton; handleRollOrBid dispatch (mode 2 roll / 3 bid); roll (Rnd.get max-roll, SM_GROUP_LOOT broadcast, dice msgs), bid (kinah/cap validation, pay msgs), distributeLoot (winner tracking, free-for-all fallback, DropService.requestDropItem/canDistribute). map.get→GetValueOrDefault; synchronized(dropItems)→lock; 0xFFFFFFFF (Java int -1)→unchecked((int)0xFFFFFFFF); getFirst→[0]. DropNpc/DropService/SM_GROUP_LOOT red-tolerated.</summary>
public class DropDistributionService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(DropDistributionService));

    public static DropDistributionService GetInstance()
    {
        return SingletonHolder.instance;
    }

    public void HandleRollOrBid(Player player, int mode, int roll, long bid, int itemId, int npcObjId, int index)
    {
        if (player == null)
            return;
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcObjId);
        if (dropNpc == null)
            return;
        HashSet<DropItem> dropItems = DropRegistrationService.GetInstance().GetCurrentDropMap().GetValueOrDefault(npcObjId);
        if (dropItems == null)
            return;
        DropItem requestedItem = null;
        lock (dropItems)
        {
            foreach (DropItem dropItem in dropItems)
                if (dropItem.GetIndex() == dropNpc.GetCurrentIndex())
                {
                    requestedItem = dropItem;
                    break;
                }
        }
        if (requestedItem == null)
            return;
        if (mode == 2)
            HandleRoll(player, roll, itemId, requestedItem, dropNpc);
        else if (mode == 3)
            HandleBid(player, bid, itemId, requestedItem, dropNpc);
        else
            log.LogWarning("{Player} requested invalid distributionMode {Mode} for dropItem[itemId={ItemId}, index={Index}, npcObjId={NpcObjId}]", player, mode, itemId, index, npcObjId);
    }

    private void HandleRoll(Player player, int roll, int itemId, DropItem requestedItem, DropNpc dropNpc)
    {
        int luck = 0;
        if (roll == 0)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DICE_GIVEUP_ME());
        }
        else
        {
            luck = Rnd.Get(1, dropNpc.GetMaxRoll());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DICE_RESULT_ME(luck, dropNpc.GetMaxRoll()));
        }
        foreach (Player member in dropNpc.GetInRangePlayers())
        {
            if (member == null)
            {
                log.LogWarning("member null Owner is in group? " + player.IsInGroup() + " Owner is in Alliance? " + player.IsInAlliance());
                continue;
            }
            PacketSendUtility.SendPacket(member, new SM_GROUP_LOOT(dropNpc.GetLootingTeamId(), member.GetObjectId(), itemId, (int)requestedItem.GetCount(),
                dropNpc.GetObjectId(), dropNpc.GetDistributionId(), luck, requestedItem.GetIndex()));
            if (!player.Equals(member) && member.IsOnline())
            {
                if (roll == 0)
                {
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_DICE_GIVEUP_OTHER(player.GetName()));
                }
                else
                {
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_DICE_RESULT_OTHER(player.GetName(), luck, dropNpc.GetMaxRoll()));
                }
            }
        }
        DistributeLoot(player, luck, itemId, requestedItem, dropNpc);
    }

    private void HandleBid(Player player, long bid, int itemId, DropItem requestedItem, DropNpc dropNpc)
    {
        if ((bid > 0 && player.GetInventory().GetKinah() < bid) || bid < 0 || bid > 999999999)
            bid = 0;
        PacketSendUtility.SendPacket(player, bid > 0 ? SM_SYSTEM_MESSAGE.STR_MSG_PAY_RESULT_ME() : SM_SYSTEM_MESSAGE.STR_MSG_PAY_GIVEUP_ME());
        foreach (Player member in dropNpc.GetInRangePlayers())
        {
            PacketSendUtility.SendPacket(member, new SM_GROUP_LOOT(dropNpc.GetLootingTeamId(), member.GetObjectId(), itemId, (int)requestedItem.GetCount(),
                dropNpc.GetObjectId(), dropNpc.GetDistributionId(), bid, requestedItem.GetIndex()));
            if (!player.Equals(member) && member.IsOnline())
            {
                if (bid > 0)
                {
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_PAY_RESULT_OTHER(player.GetName()));
                }
                else
                {
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_PAY_GIVEUP_OTHER(player.GetName()));
                }
            }
        }
        DistributeLoot(player, bid, itemId, requestedItem, dropNpc);
    }

    private void DistributeLoot(Player player, long luckyPlayer, int itemId, DropItem requestedItem, DropNpc dropNpc)
    {
        player.UnsetPlayerMode(PlayerMode.IN_ROLL);
        // Removes player from ARRAY once they have rolled or bid
        if (dropNpc.ContainsPlayerStatus(player))
            dropNpc.DelPlayerStatus(player);

        if (luckyPlayer > requestedItem.GetHighestValue())
        {
            requestedItem.SetHighestValue(luckyPlayer);
            requestedItem.SetWinningPlayer(player);
        }

        if (dropNpc.GetPlayerStatus().Count != 0)
            return;

        foreach (Player member in dropNpc.GetInRangePlayers())
        {
            if (member == null)
            {
                continue;
            }
            if (requestedItem.GetWinningPlayer() == null)
            {
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_PAY_ALL_GIVEUP());
            }
            PacketSendUtility.SendPacket(member,
                new SM_GROUP_LOOT(dropNpc.GetLootingTeamId(), requestedItem.GetWinningPlayer() != null ? requestedItem.GetWinningPlayer().GetObjectId() : 1, itemId,
                    (int)requestedItem.GetCount(), dropNpc.GetObjectId(), dropNpc.GetDistributionId(), unchecked((int)0xFFFFFFFF), requestedItem.GetIndex()));
        }

        LootGroupRules lgr = dropNpc.GetLootGroupRules();
        if (lgr != null)
            lgr.RemoveItemToBeDistributed(requestedItem);

        // Check if there is a Winning Player registered if not all members must have passed...
        if (requestedItem.GetWinningPlayer() == null)
        {
            requestedItem.IsFreeForAll(true);
            if (lgr != null && lgr.GetItemsToBeDistributed().Count != 0)
                DropService.GetInstance().CanDistribute(player, lgr.GetItemsToBeDistributed()[0]);
            return;
        }

        requestedItem.IsDistributeItem(true);
        DropService.GetInstance().RequestDropItem(player, dropNpc.GetObjectId(), dropNpc.GetCurrentIndex());
        if (lgr != null && lgr.GetItemsToBeDistributed().Count != 0)
            DropService.GetInstance().CanDistribute(player, lgr.GetItemsToBeDistributed()[0]);
    }

    private static class SingletonHolder
    {
        internal static readonly DropDistributionService instance = new DropDistributionService();
    }
}
