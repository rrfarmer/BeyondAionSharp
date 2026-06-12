using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.House;

/// <summary>
/// Java parity: model/house/HouseRegistry (Rolandas). implements Persistable→IPersistable. LinkedHashMap→Dictionary;
/// putIfAbsent(k,v)!=null→!TryAdd; map.remove(k,v)→conditional ReferenceEquals remove; generic
/// &lt;T extends AionObject &amp; Persistable&gt;→where T:AionObject,IPersistable; instanceof-pattern→is-pattern;
/// World.forEachPlayer lambda→ForEachPlayer.
/// NOTE: Java HouseObject&lt;?&gt; wildcard has no C# equivalent (HouseObject&lt;T&gt; is invariant w/ no non-generic base);
/// represented with constraint bound HouseObject&lt;PlaceableHouseObject&gt; — red-tolerated wildcard-erasure, fix at convergence.
/// House/HouseObject/HouseDecoration/UseableHouseObject/PartType/PlayerRegisteredItemsDAO red-tolerated.
/// </summary>
public class HouseRegistry : IPersistable
{
    private readonly House owner;
    private readonly Dictionary<int, HouseObject<PlaceableHouseObject>> objects = new();
    private readonly Dictionary<int, HouseDecoration> decors = new();
    private IPersistable.PersistentState persistentState = IPersistable.PersistentState.UPDATED;

    public HouseRegistry(House owner)
    {
        this.owner = owner;
    }

    public House GetOwner()
    {
        return owner;
    }

    /// <returns>All objects including deleted</returns>
    public List<HouseObject<PlaceableHouseObject>> GetObjects()
    {
        return new List<HouseObject<PlaceableHouseObject>>(objects.Values);
    }

    public List<HouseObject<PlaceableHouseObject>> GetSpawnedObjects()
    {
        List<HouseObject<PlaceableHouseObject>> temp = new();
        foreach (HouseObject<PlaceableHouseObject> obj in objects.Values)
        {
            if (obj.IsSpawnedByPlayer() && obj.GetPersistentState() != IPersistable.PersistentState.DELETED)
                temp.Add(obj);
        }
        return temp;
    }

    public List<HouseObject<PlaceableHouseObject>> GetNotSpawnedObjects()
    {
        List<HouseObject<PlaceableHouseObject>> temp = new();
        foreach (HouseObject<PlaceableHouseObject> obj in objects.Values)
        {
            if (!obj.IsSpawnedByPlayer() && obj.GetPersistentState() != IPersistable.PersistentState.DELETED)
                temp.Add(obj);
        }
        return temp;
    }

    public HouseObject<PlaceableHouseObject> GetObjectByObjId(int itemObjId)
    {
        return objects.GetValueOrDefault(itemObjId);
    }

    public bool PutObject(HouseObject<PlaceableHouseObject> houseObject, bool saveRegistry)
    {
        if (!objects.TryAdd(houseObject.GetObjectId(), houseObject))
            return false;
        if (houseObject.GetPersistentState() != IPersistable.PersistentState.UPDATED) // state is UPDATED when reloading registry and spawned objects get reused
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        if (saveRegistry)
            Save();
        return true;
    }

    public void DiscardObject(HouseObject<PlaceableHouseObject> obj, bool direct)
    {
        Discard(objects, obj, direct);
    }

    public List<HouseDecoration> GetDecors()
    {
        return new List<HouseDecoration>(decors.Values);
    }

    public List<HouseDecoration> GetUnusedDecors()
    {
        List<HouseDecoration> temp = new();
        foreach (HouseDecoration decor in decors.Values)
        {
            if (decor.GetPersistentState() != IPersistable.PersistentState.DELETED && decor.GetRoom() == -1)
                temp.Add(decor);
        }
        return temp;
    }

    public HouseDecoration GetDecorByObjId(int itemObjId)
    {
        return decors.GetValueOrDefault(itemObjId);
    }

    public bool PutDecor(HouseDecoration decor, bool saveRegistry)
    {
        if (!decors.TryAdd(decor.GetObjectId(), decor))
            return false;
        if (decor.GetPersistentState() != IPersistable.PersistentState.UPDATED)
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        if (saveRegistry)
            Save();
        return true;
    }

    public int? GetUsedDecorId(PartType partType, int room)
    {
        foreach (HouseDecoration decor in decors.Values)
        {
            if (decor.GetPersistentState() != IPersistable.PersistentState.DELETED && decor.GetTemplate().GetType_() == partType && decor.GetRoom() == room)
                return decor.GetTemplateId();
        }
        return GetOwner().GetBuilding().GetDefaultDecorId(partType);
    }

    public void SetUsed(HouseDecoration decor, int room)
    {
        if (decor.GetPersistentState() == IPersistable.PersistentState.DELETED || decor.GetRoom() == room)
            return;
        DiscardDecor(decor.GetTemplate().GetType_(), room);
        int? defaultPartId = GetOwner().GetBuilding().GetDefaultDecorId(decor.GetTemplate().GetType_());
        if (defaultPartId == decor.GetTemplateId())
        {
            decor.SetPersistentState(IPersistable.PersistentState.DELETED);
        }
        else
        {
            decor.SetRoom(room);
            if (decor.GetPersistentState() != IPersistable.PersistentState.NEW)
            {
                decor.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            }
        }
    }

    public void DiscardDecor(PartType partType, int roomNo)
    {
        foreach (HouseDecoration decor in GetDecors())
        {
            if (decor.GetTemplate().GetType_() == partType && decor.GetRoom() == roomNo)
                DiscardDecor(decor, false);
        }
    }

    public void DiscardDecor(HouseDecoration decor, bool direct)
    {
        Discard(decors, decor, direct);
    }

    private void Discard<T>(Dictionary<int, T> map, T obj, bool direct) where T : AionObject, IPersistable
    {
        if (obj.GetPersistentState() == IPersistable.PersistentState.NEW || direct)
        {
            if (map.TryGetValue(obj.GetObjectId(), out T cur) && ReferenceEquals(cur, obj))
                map.Remove(obj.GetObjectId());
        }
        else
        {
            obj.SetPersistentState(IPersistable.PersistentState.DELETED);
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        }
        // remove house object use cooldowns for this object
        if (obj is UseableHouseObject<PlaceableHouseObject> useableHouseObject && useableHouseObject.HasUseCooldown())
            Aion.GameServer.World.World.GetInstance().ForEachPlayer(player => player.GetHouseObjectCooldowns().Remove(obj.GetObjectId()));
    }

    /// <summary>Despawns all objects and updates DB.</summary>
    public void Reset()
    {
        List<HouseObject<PlaceableHouseObject>> spawnedObjects = GetSpawnedObjects();
        if (spawnedObjects.Count == 0)
        {
            if (GetOwner().GetOwnerId() != 0)
                PlayerRegisteredItemsDAO.ResetRegistry(GetOwner().GetOwnerId());
        }
        else
        {
            foreach (HouseObject<PlaceableHouseObject> obj in spawnedObjects)
                obj.RemoveFromHouse();
        }
        foreach (HouseDecoration decor in decors.Values)
        {
            if (decor.GetRoom() != -1)
                DiscardDecor(decor, false);
        }
        Save();
    }

    public void Save()
    {
        if (persistentState == IPersistable.PersistentState.UPDATE_REQUIRED)
            PlayerRegisteredItemsDAO.Store(this, GetOwner().GetOwnerId());
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        this.persistentState = persistentState;
    }

    public int Size()
    {
        return objects.Count + decors.Count;
    }
}
