using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Broker;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/BrokerItem (kosyachok). implements Comparable+Persistable→IComparable+IPersistable. java.sql.Timestamp→DateTimeOffset (setNanos(0)→truncate to seconds via FromUnixTimeSeconds); TimeUnit.DAYS.toMillis→days*86400000L; @SuppressWarnings(fallthrough) setPersistentState→goto default; anonymous Comparator statics→IComparer<BrokerItem> (Comparer.Create); IllegalArgumentException→ArgumentException; byte sortType→sbyte. Item/BrokerRace red-tolerated.</summary>
public class BrokerItem : IComparable<BrokerItem>, IPersistable
{
    private readonly Item item;
    private readonly int itemId;
    private readonly int itemUniqueId;
    private long itemCount;
    private readonly string itemCreator;
    private readonly long price;
    private readonly int sellerId;
    private readonly BrokerRace itemBrokerRace;
    private bool isSold, isCanceled;
    private bool isSettled;
    private readonly DateTimeOffset expireTime;
    private DateTimeOffset settleTime;
    private readonly bool splittingAvailable;
    private long averagePrice;

    internal IPersistable.PersistentState state;

    public BrokerItem(Item item, long price, int sellerId, bool splittingAvailable, BrokerRace itemBrokerRace)
    {
        this.item = item;
        this.itemId = item.GetItemTemplate().GetTemplateId();
        this.itemUniqueId = item.GetObjectId();
        this.itemCount = item.GetItemCount();
        this.itemCreator = item.GetItemCreator();
        this.price = price;
        this.sellerId = sellerId;
        this.itemBrokerRace = itemBrokerRace;
        this.isSold = false;
        this.isSettled = false;
        this.splittingAvailable = splittingAvailable;
        DateTimeOffset exp = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + CustomConfig.BROKER_REGISTRATION_EXPIRATION_DAYS * 24L * 60 * 60 * 1000);
        this.expireTime = DateTimeOffset.FromUnixTimeSeconds(exp.ToUnixTimeSeconds()); // db queries by this timestamp but doesn't store fractional seconds
        this.settleTime = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        this.state = IPersistable.PersistentState.NEW;
    }

    public BrokerItem(Item item, int itemId, int itemUniqueId, long itemCount, string itemCreator, long price, int sellerId, BrokerRace itemBrokerRace,
        bool isSold, bool isSettled, DateTimeOffset expireTime, DateTimeOffset settleTime, bool splittingAvailable)
    {
        this.item = item;
        this.itemId = itemId;
        this.itemUniqueId = itemUniqueId;
        this.itemCount = itemCount;
        this.itemCreator = itemCreator;
        this.price = price;
        this.sellerId = sellerId;
        this.itemBrokerRace = itemBrokerRace;
        this.isSold = isSold;
        this.isSettled = isSettled;
        this.expireTime = DateTimeOffset.FromUnixTimeSeconds(expireTime.ToUnixTimeSeconds()); // db queries by this timestamp but doesn't store fractional seconds
        this.settleTime = settleTime;
        this.splittingAvailable = splittingAvailable;
        this.state = IPersistable.PersistentState.NOACTION;
    }

    /// <returns>itemCreator</returns>
    public string GetItemCreator()
    {
        return itemCreator == null ? "" : itemCreator;
    }

    public Item GetItem()
    {
        return item;
    }

    public bool IsCanceled()
    {
        return isCanceled;
    }

    public void SetIsCanceled(bool isCanceled)
    {
        this.isCanceled = isCanceled;
    }

    public void RemoveItem()
    {
        // this.item = null;
        this.isSold = true;
        this.isSettled = true;
        this.settleTime = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public int GetItemId()
    {
        return itemId;
    }

    public int GetItemUniqueId()
    {
        return itemUniqueId;
    }

    public long GetPrice()
    {
        return price;
    }

    public int GetSellerId()
    {
        return sellerId;
    }

    public BrokerRace GetItemBrokerRace()
    {
        return itemBrokerRace;
    }

    public bool IsSold()
    {
        return this.isSold;
    }

    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.DELETED:
                if (this.state == IPersistable.PersistentState.NEW)
                    this.state = IPersistable.PersistentState.NOACTION;
                else
                    this.state = IPersistable.PersistentState.DELETED;
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (this.state == IPersistable.PersistentState.NEW)
                    break;
                goto default;
            default:
                this.state = persistentState;
                break;
        }
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return state;
    }

    public bool IsSettled()
    {
        return isSettled;
    }

    public void SetSettled()
    {
        this.isSettled = true;
        this.settleTime = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public DateTimeOffset GetExpireTime()
    {
        return expireTime;
    }

    public DateTimeOffset GetSettleTime()
    {
        return settleTime;
    }

    public long GetItemCount()
    {
        return itemCount;
    }

    public void DecreaseItemCount(long value)
    {
        this.itemCount -= value;
        this.item.DecreaseItemCount(value);
    }

    /// <returns>item level according to template</returns>
    private int GetItemLevel()
    {
        return item.GetItemTemplate().GetLevel();
    }

    /// <returns>price for one piece</returns>
    private long GetPiecePrice()
    {
        return GetPrice() / GetItemCount();
    }

    /// <returns>name of the item</returns>
    private string GetItemName()
    {
        return item.GetItemName();
    }

    public bool IsSplittingAvailable()
    {
        return splittingAvailable;
    }

    public long GetAveragePrice()
    {
        return averagePrice;
    }

    public void SetAveragePrice(long averagePrice)
    {
        this.averagePrice = averagePrice;
    }

    /// <summary>Default sorting: using itemUniqueId</summary>
    public int CompareTo(BrokerItem o)
    {
        return itemUniqueId > o.GetItemUniqueId() ? 1 : -1;
    }

    /// <summary>Sorting using price of item</summary>
    internal static readonly IComparer<BrokerItem> NAME_SORT_ASC = Comparer<BrokerItem>.Create((o1, o2) =>
    {
        if (o1 == null || o2 == null)
            return ComparePossiblyNull(o1, o2);
        return string.CompareOrdinal(o1.GetItemName(), o2.GetItemName());
    });

    internal static readonly IComparer<BrokerItem> NAME_SORT_DESC = Comparer<BrokerItem>.Create((o1, o2) =>
    {
        if (o1 == null || o2 == null)
            return ComparePossiblyNull(o1, o2);
        return string.CompareOrdinal(o1.GetItemName(), o2.GetItemName());
    });

    /// <summary>Sorting using price of item</summary>
    internal static readonly IComparer<BrokerItem> PRICE_SORT_ASC = Comparer<BrokerItem>.Create((o1, o2) =>
    {
        if (o1 == null || o2 == null)
            return ComparePossiblyNull(o1, o2);
        if (o1.GetPrice() == o2.GetPrice())
            return 0;
        return o1.GetPrice() > o2.GetPrice() ? 1 : -1;
    });

    internal static readonly IComparer<BrokerItem> PRICE_SORT_DESC = Comparer<BrokerItem>.Create((o1, o2) =>
    {
        if (o1 == null || o2 == null)
            return ComparePossiblyNull(o1, o2);
        if (o1.GetPrice() == o2.GetPrice())
            return 0;
        return o1.GetPrice() > o2.GetPrice() ? -1 : 1;
    });

    /// <summary>Sorting using piece price of item</summary>
    internal static readonly IComparer<BrokerItem> PIECE_PRICE_SORT_ASC = Comparer<BrokerItem>.Create((o1, o2) =>
    {
        if (o1 == null || o2 == null)
            return ComparePossiblyNull(o1, o2);
        if (o1.GetPiecePrice() == o2.GetPiecePrice())
            return 0;
        return o1.GetPiecePrice() > o2.GetPiecePrice() ? 1 : -1;
    });

    internal static readonly IComparer<BrokerItem> PIECE_PRICE_SORT_DESC = Comparer<BrokerItem>.Create((o1, o2) =>
    {
        if (o1 == null || o2 == null)
            return ComparePossiblyNull(o1, o2);
        if (o1.GetPiecePrice() == o2.GetPiecePrice())
            return 0;
        return o1.GetPiecePrice() > o2.GetPiecePrice() ? -1 : 1;
    });

    /// <summary>Sorting using level of item</summary>
    internal static readonly IComparer<BrokerItem> LEVEL_SORT_ASC = Comparer<BrokerItem>.Create((o1, o2) =>
    {
        if (o1 == null || o2 == null)
            return ComparePossiblyNull(o1, o2);
        if (o1.GetItemLevel() == o2.GetItemLevel())
            return 0;
        return o1.GetItemLevel() > o2.GetItemLevel() ? 1 : -1;
    });

    internal static readonly IComparer<BrokerItem> LEVEL_SORT_DESC = Comparer<BrokerItem>.Create((o1, o2) =>
    {
        if (o1 == null || o2 == null)
            return ComparePossiblyNull(o1, o2);
        if (o1.GetItemLevel() == o2.GetItemLevel())
            return 0;
        return o1.GetItemLevel() > o2.GetItemLevel() ? -1 : 1;
    });

    private static int ComparePossiblyNull<T>(T aThis, T aThat) where T : IComparable<T>
    {
        int result = 0;
        if (aThis == null && aThat != null)
        {
            result = -1;
        }
        else if (aThis != null && aThat == null)
        {
            result = 1;
        }
        return result;
    }

    /// <summary>
    /// 1 - by name;<br/>
    /// 2 - by level;<br/>
    /// 4 - by totalPrice;<br/>
    /// 6 - by price for piece (Math.round(item.getPrice() / item.getItemCount))<br/>
    /// </summary>
    public static IComparer<BrokerItem> GetComparatoryByType(sbyte sortType)
    {
        switch (sortType)
        {
            case 0:
                return NAME_SORT_ASC;
            case 1:
                return NAME_SORT_DESC;
            case 2:
                return LEVEL_SORT_ASC;
            case 3:
                return LEVEL_SORT_DESC;
            case 4:
                return PRICE_SORT_ASC;
            case 5:
                return PRICE_SORT_DESC;
            case 6:
                return PIECE_PRICE_SORT_ASC;
            case 7:
                return PIECE_PRICE_SORT_DESC;
            default:
                throw new ArgumentException("Illegal sort type for broker items");
        }
    }
}
