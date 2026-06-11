using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Trade;

/// <summary>Java parity: model/trade/TradeList (ATracer, Wakizashi, Neon). LinkedHashMap→Dictionary; Map.containsKey/get/put→TryGetValue/[]; AcquisitionType.equals→==; double-cast AP math preserved verbatim. PricesService/PacketSendUtility/SM_SYSTEM_MESSAGE/Acquisition red-tolerated.</summary>
public class TradeList
{
    private int sellerObjId;

    private List<TradeItem> tradeItems = new();

    private long requiredKinah;

    private int requiredAp;

    private Dictionary<int, long> requiredItems = new();

    public TradeList()
    {
    }

    public TradeList(int sellerObjId)
    {
        this.sellerObjId = sellerObjId;
    }

    public void AddItem(int itemId, long count)
    {
        AddTradeItem(new TradeItem(itemId, count));
    }

    public void AddTradeItem(TradeItem tradeItem)
    {
        tradeItems.Add(tradeItem);
    }

    /// <returns>price TradeList sum price</returns>
    public bool CalculateBuyListPrice(Player player, int modifier)
    {
        long availableKinah = player.GetInventory().GetKinah();
        requiredKinah = 0;

        foreach (TradeItem tradeItem in tradeItems)
        {
            requiredKinah += PricesService.GetBuyPrice(tradeItem.GetItemTemplate().GetPrice(), player.GetRace()) * tradeItem.GetCount() * modifier / 100;
        }

        return availableKinah >= requiredKinah;
    }

    public bool CalculateAbyssRewardBuyList(Player player, int modifier)
    {
        int ap = player.GetAbyssRank().GetAp();

        this.requiredAp = 0;
        this.requiredItems.Clear();

        foreach (TradeItem tradeItem in tradeItems)
        {
            Acquisition aquisition = tradeItem.GetItemTemplate().GetAcquisition();
            if (aquisition == null)
                continue;

            if (aquisition.GetType_() == AcquisitionType.AP || aquisition.GetType_() == AcquisitionType.ABYSS)
                requiredAp += (int)((aquisition.GetRequiredAp() * tradeItem.GetCount() * modifier / 100.0D) * PricesService.GetVendorBuyModifier()) / 100;

            int rewardItemId = aquisition.GetItemId();
            if (rewardItemId == 0) // no required item (medals, etc))
                continue;

            long alreadyAddedCount = 0;
            if (requiredItems.ContainsKey(rewardItemId))
                alreadyAddedCount = requiredItems[rewardItemId];
            if (alreadyAddedCount == 0)
                requiredItems[rewardItemId] = aquisition.GetItemCount() * tradeItem.GetCount();
            else
                requiredItems[rewardItemId] = alreadyAddedCount + aquisition.GetItemCount() * tradeItem.GetCount();
        }

        if (ap < requiredAp)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_ABYSSPOINT());
            return false;
        }

        foreach (int itemId in requiredItems.Keys)
        {
            long count = player.GetInventory().GetItemCountByItemId(itemId);
            if (requiredItems[itemId] < 1 || count < requiredItems[itemId])
                return false;
        }

        return true;
    }

    public List<TradeItem> GetTradeItems()
    {
        return tradeItems;
    }

    public int Size()
    {
        return tradeItems.Count;
    }

    public int GetSellerObjId()
    {
        return sellerObjId;
    }

    public int GetRequiredAp()
    {
        return requiredAp;
    }

    public long GetRequiredKinah()
    {
        return requiredKinah;
    }

    public Dictionary<int, long> GetRequiredItems()
    {
        return requiredItems;
    }
}
