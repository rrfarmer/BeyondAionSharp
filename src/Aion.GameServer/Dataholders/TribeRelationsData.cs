using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Tribe;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/TribeRelationsData (ATracer). @XmlRootElement(tribe_relations); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("tribe_relations")]
public class TribeRelationsData
{
    [XmlElement("tribe")] public List<Tribe> tribeList;

    [XmlIgnore] private readonly Dictionary<TribeClass, Tribe> tribeNameMap = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (Tribe tribe in tribeList)
        {
            tribeNameMap[tribe.GetName()] = tribe;
        }
        tribeList = null;
    }

    public int Size()
    {
        return tribeNameMap.Count;
    }

    public TribeClass GetBaseTribe(TribeClass tribeName)
    {
        Tribe tribe = tribeNameMap[tribeName];
        return tribe.GetBase();
    }

    public bool IsAggressiveRelation(TribeClass tribeName1, TribeClass tribeName2)
    {
        Tribe tribe1 = tribeNameMap.TryGetValue(tribeName1, out var t1) ? t1 : null;
        Tribe tribe2 = tribeNameMap.TryGetValue(tribeName2, out var t2) ? t2 : null;
        if (tribe1 == null || tribe2 == null)
            return false;
        return tribe1.GetAggro().Contains(tribe2.GetBase()) || tribe1.GetAggro().Contains(tribeName2) || tribe2.GetAggro().Contains(tribe1.GetBase())
            || tribe2.GetAggro().Contains(tribeName1);
    }

    public bool IsSupportRelation(TribeClass tribeName1, TribeClass tribeName2)
    {
        Tribe tribe1 = tribeNameMap.TryGetValue(tribeName1, out var t1) ? t1 : null;
        Tribe tribe2 = tribeNameMap.TryGetValue(tribeName2, out var t2) ? t2 : null;
        if (tribe1 == null || tribe2 == null)
            return false;
        return tribe1.GetSupport().Contains(tribe2.GetBase()) || tribe1.GetSupport().Contains(tribeName2)
            || tribe2.GetSupport().Contains(tribe1.GetBase()) || tribe2.GetSupport().Contains(tribeName1);
    }

    public bool IsFriendlyRelation(TribeClass tribeName1, TribeClass tribeName2)
    {
        Tribe tribe1 = tribeNameMap.TryGetValue(tribeName1, out var t1) ? t1 : null;
        Tribe tribe2 = tribeNameMap.TryGetValue(tribeName2, out var t2) ? t2 : null;
        if (tribe1 == null || tribe2 == null)
            return false;
        return tribe1.GetFriend().Contains(tribe2.GetBase()) || tribe1.GetFriend().Contains(tribeName2) || tribe2.GetFriend().Contains(tribe1.GetBase())
            || tribe2.GetFriend().Contains(tribeName1);
    }

    public bool IsNeutralRelation(TribeClass tribeName1, TribeClass tribeName2)
    {
        Tribe tribe1 = tribeNameMap.TryGetValue(tribeName1, out var t1) ? t1 : null;
        Tribe tribe2 = tribeNameMap.TryGetValue(tribeName2, out var t2) ? t2 : null;
        if (tribe1 == null || tribe2 == null)
            return false;
        return tribe1.GetNeutral().Contains(tribe2.GetBase()) || tribe1.GetNeutral().Contains(tribeName2)
            || tribe2.GetNeutral().Contains(tribe1.GetBase()) || tribe2.GetNeutral().Contains(tribeName1);
    }

    public bool IsNoneRelation(TribeClass tribeName1, TribeClass tribeName2)
    {
        Tribe tribe1 = tribeNameMap.TryGetValue(tribeName1, out var t1) ? t1 : null;
        Tribe tribe2 = tribeNameMap.TryGetValue(tribeName2, out var t2) ? t2 : null;
        if (tribe1 == null || tribe2 == null)
            return false;
        return tribe1.GetNone().Contains(tribe2.GetBase()) || tribe1.GetNone().Contains(tribeName2) || tribe2.GetNone().Contains(tribe1.GetBase())
            || tribe2.GetNone().Contains(tribeName1);
    }

    public bool IsHostileRelation(TribeClass tribeName1, TribeClass tribeName2)
    {
        Tribe tribe1 = tribeNameMap.TryGetValue(tribeName1, out var t1) ? t1 : null;
        Tribe tribe2 = tribeNameMap.TryGetValue(tribeName2, out var t2) ? t2 : null;
        if (tribe1 == null || tribe2 == null)
            return false;
        return tribe1.GetHostile().Contains(tribe2.GetBase()) || tribe1.GetHostile().Contains(tribeName2)
            || tribe2.GetHostile().Contains(tribe1.GetBase()) || tribe2.GetHostile().Contains(tribeName1);
    }

    /// <summary>True, if tribeName can support tribeNameAskingForSupport.</summary>
    public bool CanSupport(TribeClass tribeName, TribeClass tribeNameAskingForSupport)
    {
        Tribe tribe = tribeNameMap.TryGetValue(tribeName, out var t) ? t : null;
        Tribe tribeAskingForSupport = tribeNameMap.TryGetValue(tribeNameAskingForSupport, out var ta) ? ta : null;
        if (tribe == null || tribeAskingForSupport == null)
            return false;
        return tribe.GetSupport().Contains(tribeNameAskingForSupport) || tribe.GetSupport().Contains(tribeAskingForSupport.GetBase());
    }
}
