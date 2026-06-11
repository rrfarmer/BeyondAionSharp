using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Model.Trade;

/// <summary>Java parity: model/trade/Exchange (ATracer). Map→Dictionary. Player red-tolerated.</summary>
public class Exchange
{
    private Player activeplayer;
    private Player targetPlayer;

    private bool confirmed;
    private bool locked;

    private long kinahCount;

    private Dictionary<int, ExchangeItem> items = new();

    public Exchange(Player activeplayer, Player targetPlayer)
    {
        this.activeplayer = activeplayer;
        this.targetPlayer = targetPlayer;
    }

    public void Confirm()
    {
        confirmed = true;
    }

    /// <returns>the confirmed</returns>
    public bool IsConfirmed()
    {
        return confirmed;
    }

    public void Lock()
    {
        this.locked = true;
    }

    /// <returns>the locked</returns>
    public bool IsLocked()
    {
        return locked;
    }

    public void AddItem(int parentItemObjId, ExchangeItem exchangeItem)
    {
        this.items[parentItemObjId] = exchangeItem;
    }

    public void AddKinah(long countToAdd)
    {
        this.kinahCount += countToAdd;
    }

    /// <returns>the activeplayer</returns>
    public Player GetActiveplayer()
    {
        return activeplayer;
    }

    /// <returns>the targetPlayer</returns>
    public Player GetTargetPlayer()
    {
        return targetPlayer;
    }

    /// <returns>the kinahCount</returns>
    public long GetKinahCount()
    {
        return kinahCount;
    }

    /// <returns>the items</returns>
    public Dictionary<int, ExchangeItem> GetItems()
    {
        return items;
    }

    public bool IsExchangeListFull()
    {
        return items.Count >= 18;
    }
}
