using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Ingameshop;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_IN_GAME_SHOP_LIST (xTz, KID). Paged in-game shop item list (salesRanking 1) or top-sales list. computeIfAbsent->TryGetValue-or-add; Map.get->GetValueOrDefault. IGItem/InGameShopEn red-tolerated.</summary>
public class SM_IN_GAME_SHOP_LIST : AionServerPacket
{
    private Player player;
    private int nrList;
    private int salesRanking;

    public SM_IN_GAME_SHOP_LIST(Player player, int nrList, int salesRanking)
    {
        this.player = player;
        this.nrList = nrList;
        this.salesRanking = salesRanking;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Dictionary<int, List<IGItem>> allItems = new Dictionary<int, List<IGItem>>();
        List<IGItem> inAllItems;
        ICollection<IGItem> items;
        byte category = player.inGameShop.GetCategory();
        byte subCategory = player.inGameShop.GetSubCategory();
        if (salesRanking == 1)
        {
            items = InGameShopEn.GetInstance().GetItems(category);
            int size = 0;
            int tabSize = 9;
            int f = 0;
            foreach (IGItem a in items)
            {
                if (subCategory != 2)
                    if (a.GetSubCategory() != subCategory)
                        continue;

                if (size == tabSize)
                {
                    tabSize += 9;
                    f++;
                }
                if (!allItems.TryGetValue(f, out List<IGItem> list))
                {
                    list = new List<IGItem>();
                    allItems[f] = list;
                }
                list.Add(a);
                size++;
            }

            inAllItems = allItems.GetValueOrDefault(nrList);
            WriteD(salesRanking);
            WriteD(nrList);
            WriteD(size > 0 ? tabSize : 0);
            WriteH(inAllItems == null ? 0 : inAllItems.Count);
            if (inAllItems != null)
                foreach (IGItem item in inAllItems)
                    WriteD(item.GetObjectId());
        }
        else
        {
            List<int> salesRankingItems = InGameShopEn.GetInstance().GetTopSales(subCategory, category);
            WriteD(salesRanking);
            WriteD(nrList);
            WriteD((InGameShopEn.GetInstance().GetMaxList(subCategory, category) + 1) * 9);
            WriteH(salesRankingItems.Count);
            foreach (int id in salesRankingItems)
                WriteD(id);
        }
    }
}
