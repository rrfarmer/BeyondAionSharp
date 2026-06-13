using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Limiteditems;
using Aion.GameServer.Model.Templates.Goods;
using Aion.GameServer.Services.Cron;
using TradeTab = Aion.GameServer.Model.Templates.Tradelist.TradeListTemplate.TradeTab;

namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: services/LimitedItemTradeService (xTz).
/// TYPE_A: BuyLimit == 0 &amp;&amp; SellLimit != 0; TYPE_B: BuyLimit != 0 &amp;&amp; SellLimit == 0; TYPE_C: BuyLimit != 0 &amp;&amp; SellLimit != 0.
/// Java singleton; Map.computeIfAbsent→TryGetValue-or-create; methodref limitedItem::setToDefault→Action.
/// </summary>
public class LimitedItemTradeService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(LimitedItemTradeService));
    private readonly Dictionary<int, LimitedTradeNpc> limitedTradeNpcs = new();

    public void Start()
    {
        foreach (var entry in DataManager.TRADE_LIST_DATA.GetTradeListTemplate())
        {
            int npcId = entry.Key;
            var npc = entry.Value;
            foreach (TradeTab list in npc.GetTradeTablist())
            {
                GoodsList goodsList = DataManager.GOODSLIST_DATA.GetGoodsListById(list.GetId());
                if (goodsList == null)
                {
                    log.LogWarning("No goodslist for tradelist of npc " + npcId);
                    continue;
                }
                List<LimitedItem> limitedItems = goodsList.GetLimitedItems();
                if (!limitedItems.IsEmpty())
                {
                    if (!limitedTradeNpcs.TryGetValue(npcId, out LimitedTradeNpc tradeNpc))
                    {
                        tradeNpc = new LimitedTradeNpc();
                        limitedTradeNpcs[npcId] = tradeNpc;
                    }
                    tradeNpc.AddLimitedItems(limitedItems);
                }
            }
        }

        foreach (LimitedTradeNpc limitedTradeNpc in limitedTradeNpcs.Values)
        {
            foreach (LimitedItem limitedItem in limitedTradeNpc.GetLimitedItems())
            {
                LimitedItem item = limitedItem;
                CronService.GetInstance().Schedule(() => item.SetToDefault(), item.GetSalesTime());
            }
        }
        log.LogInformation("Scheduled Limited Items based on cron expression size: " + limitedTradeNpcs.Count);
    }

    public LimitedItem? GetLimitedItem(int itemId, int npcId)
    {
        if (limitedTradeNpcs.ContainsKey(npcId))
        {
            foreach (LimitedItem limitedItem in limitedTradeNpcs[npcId].GetLimitedItems())
            {
                if (limitedItem.GetItemId() == itemId)
                {
                    return limitedItem;
                }
            }
        }
        return null;
    }

    public bool IsLimitedTradeNpc(int npcId)
    {
        return limitedTradeNpcs.ContainsKey(npcId);
    }

    public LimitedTradeNpc? GetLimitedTradeNpc(int npcId)
    {
        return limitedTradeNpcs.TryGetValue(npcId, out LimitedTradeNpc npc) ? npc : null;
    }

    public static LimitedItemTradeService GetInstance() => SingletonHolder.INSTANCE;

    private static class SingletonHolder
    {
        internal static readonly LimitedItemTradeService INSTANCE = new LimitedItemTradeService();
    }
}
