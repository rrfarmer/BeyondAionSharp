using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/RepurchaseService (xTz).</summary>
public class RepurchaseService
{
    private ConcurrentDictionary<int, ISet<Item>> repurchaseItems = new ConcurrentDictionary<int, ISet<Item>>();

    private RepurchaseService()
    {
    }

    /// <summary>Save items for repurchase for this player.</summary>
    public void AddRepurchaseItems(Player player, List<Item> items)
    {
        repurchaseItems[player.GetObjectId()] = new HashSet<Item>(items);
    }

    /// <summary>Delete all repurchase items for this player.</summary>
    public void RemoveRepurchaseItems(Player player)
    {
        repurchaseItems.TryRemove(player.GetObjectId(), out _);
    }

    public ISet<Item> GetRepurchaseItems(int playerObjectId)
    {
        return repurchaseItems.TryGetValue(playerObjectId, out ISet<Item> items) ? items : new HashSet<Item>();
    }

    public bool CanRepurchase(Player player, int itemObjectId)
    {
        return GetRepurchaseItems(player.GetObjectId()).Any(item => item.GetObjectId() == itemObjectId);
    }

    public void RepurchaseFromShop(Player player, RepurchaseList repurchaseList)
    {
        if (!PlayerRestrictions.CanTrade(player))
        {
            return;
        }
        repurchaseItems.TryGetValue(player.GetObjectId(), out ISet<Item> items);
        foreach (int itemObjectId in repurchaseList.GetRepurchaseItems())
        {
            if (player.GetInventory().IsFull())
            {
                PacketSendUtility.SendPacket(player, SmSystemMessage.DiceInventoryError());
                break;
            }

            Item repurchaseItem = items.FirstOrDefault(item => item.GetObjectId() == itemObjectId);
            if (repurchaseItem != null)
            {
                if (player.GetInventory().TryDecreaseKinah(repurchaseItem.GetRepurchasePrice()))
                {
                    ItemService.AddItem(player, repurchaseItem);
                    items.Remove(repurchaseItem);
                }
                else
                {
                    AuditLogger.Log(player, "tried to repurchase item " + repurchaseItem.GetItemId() + ", count: " + repurchaseItem.GetItemCount()
                            + " without kinah");
                }
            }
        }
    }

    public static RepurchaseService GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    private static class SingletonHolder
    {
        internal static readonly RepurchaseService INSTANCE = new RepurchaseService();
    }
}
