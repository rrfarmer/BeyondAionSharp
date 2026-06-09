using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Services;

namespace Aion.GameServer.Model.Trade;

/// <summary>Java parity: model/trade/RepurchaseList (xTz).</summary>
public class RepurchaseList
{
    private readonly int sellerObjId;
    // Java parity: LinkedHashSet<Integer> (insertion-ordered, unique) — List<int> + Contains-guard preserves both.
    private List<int> repurchases = new List<int>();

    public RepurchaseList(int sellerObjId)
    {
        this.sellerObjId = sellerObjId;
    }

    public void AddRepurchaseItem(Player player, int itemObjectId, long count)
    {
        if (RepurchaseService.GetInstance().CanRepurchase(player, itemObjectId))
            if (!repurchases.Contains(itemObjectId))
                repurchases.Add(itemObjectId);
    }

    public List<int> GetRepurchaseItems()
    {
        return repurchases;
    }

    public int Size()
    {
        return repurchases.Count;
    }

    public int GetSellerObjId()
    {
        return sellerObjId;
    }
}
