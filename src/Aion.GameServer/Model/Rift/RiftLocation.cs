using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Rift;
using Aion.GameServer.Model.Templates.Spawns;

namespace Aion.GameServer.Model.Rift;

/// <summary>Java parity: model/rift/RiftLocation (Source). ConcurrentHashMap→ConcurrentDictionary.</summary>
public class RiftLocation
{
    private bool opened;
    private readonly RiftTemplate template;
    private readonly ConcurrentDictionary<int, SpawnTemplate> spawned = new();

    public RiftLocation(RiftTemplate template)
    {
        this.template = template;
    }

    public int GetId()
    {
        return template.GetId();
    }

    public int GetWorldId()
    {
        return template.GetWorldId();
    }

    public bool HasSpawns()
    {
        return template.HasSpawns();
    }

    public bool IsAutoCloseable()
    {
        return template.IsAutoCloseable();
    }

    public bool IsOpened()
    {
        return opened;
    }

    public void SetOpened(bool state)
    {
        opened = state;
    }

    public IDictionary<int, SpawnTemplate> GetSpawned()
    {
        return spawned;
    }

    public void AddSpawned(VisibleObject obj)
    {
        spawned[obj.ObjectId] = obj.GetSpawn();
    }

    public bool ReplaceSpawned(int oldObjectId, VisibleObject newObject)
    {
        if (spawned.TryRemove(oldObjectId, out _))
        {
            AddSpawned(newObject);
            return true;
        }
        return false;
    }
}
