using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Broker;

/// <summary>Java parity: model/broker/BrokerPlayerCache (ATracer). Plain per-player broker search cache. Java signed byte→sbyte; BrokerItem red-tolerated.</summary>
public class BrokerPlayerCache
{
    private BrokerItem[] brokerListCache = new BrokerItem[0];
    private int brokerMaskCache;
    private sbyte brokerSoftTypeCache;
    private int brokerStartPageCache;
    private List<int> itemList = new();

    /// <returns>the brokerListCache</returns>
    public BrokerItem[] GetBrokerListCache()
    {
        return brokerListCache;
    }

    /// <param name="brokerListCache">the brokerListCache to set</param>
    public void SetBrokerListCache(BrokerItem[] brokerListCache)
    {
        this.brokerListCache = brokerListCache;
    }

    /// <returns>the brokerMaskCache</returns>
    public int GetBrokerMaskCache()
    {
        return brokerMaskCache;
    }

    /// <param name="brokerMaskCache">the brokerMaskCache to set</param>
    public void SetBrokerMaskCache(int brokerMaskCache)
    {
        this.brokerMaskCache = brokerMaskCache;
    }

    /// <returns>the brokerSoftTypeCache</returns>
    public sbyte GetBrokerSortTypeCache()
    {
        return brokerSoftTypeCache;
    }

    /// <param name="brokerSoftTypeCache">the brokerSoftTypeCache to set</param>
    public void SetBrokerSortTypeCache(sbyte brokerSoftTypeCache)
    {
        this.brokerSoftTypeCache = brokerSoftTypeCache;
    }

    /// <returns>the brokerStartPageCache</returns>
    public int GetBrokerStartPageCache()
    {
        return brokerStartPageCache;
    }

    /// <returns>the getSearchItemList</returns>
    public List<int> GetSearchItemList()
    {
        if (this.itemList == null)
            return null;
        return this.itemList;
    }

    /// <param name="brokerStartPageCache">the brokerStartPageCache to set</param>
    public void SetBrokerStartPageCache(int brokerStartPageCache)
    {
        this.brokerStartPageCache = brokerStartPageCache;
    }

    /// <param name="itemList">the searched item list to set</param>
    public void SetSearchItemsList(List<int> itemList)
    {
        this.itemList = itemList;
    }
}
