using System.Collections.Generic;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/PrivateStore.</summary>
public class PrivateStore
{
    private readonly Player owner;
    // Java parity: LinkedHashMap — insertion-ordered.
    private Dictionary<int, Aion.GameServer.Model.Trade.TradePSItem> items;
    public const int MAX_STORE_MESSAGE_LENGTH = 255;

    private string storeMessage;

    public PrivateStore(Player owner)
    {
        this.owner = owner;
        this.items = new Dictionary<int, Aion.GameServer.Model.Trade.TradePSItem>();
    }

    public Player GetOwner()
    {
        return owner;
    }

    public Dictionary<int, Aion.GameServer.Model.Trade.TradePSItem> GetSoldItems()
    {
        return items;
    }

    public void AddItemToSell(int itemObjId, Aion.GameServer.Model.Trade.TradePSItem tradeItem)
    {
        items[itemObjId] = tradeItem;
    }

    public void RemoveItem(int itemObjId)
    {
        if (items.ContainsKey(itemObjId))
        {
            Dictionary<int, Aion.GameServer.Model.Trade.TradePSItem> newItems = new Dictionary<int, Aion.GameServer.Model.Trade.TradePSItem>();
            foreach (int itemObjIds in items.Keys)
            {
                if (itemObjId != itemObjIds)
                    newItems[itemObjIds] = items[itemObjIds];
            }
            this.items = newItems;
        }
    }

    public Aion.GameServer.Model.Trade.TradePSItem GetTradeItemByObjId(int itemObjId)
    {
        return items.TryGetValue(itemObjId, out Aion.GameServer.Model.Trade.TradePSItem item) ? item : null;
    }

    public void SetStoreMessage(string storeMessage)
    {
        // Part of SM_PLAYER_INFO and SM_PRIVATE_STORE, which must stay below the client's 8192 byte limit.
        this.storeMessage = storeMessage == null || storeMessage.Length <= MAX_STORE_MESSAGE_LENGTH ? storeMessage!
            : storeMessage.Substring(0, MAX_STORE_MESSAGE_LENGTH);
    }

    public string GetStoreMessage()
    {
        return storeMessage == null ? "" : storeMessage;
    }
}
