using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.World.Knownlist;

/// <summary>
/// Every object the owner currently knows (visible and invisible). Knowing is a two-way relation.
/// Java parity: world/knownlist/KnownList.
/// </summary>
public class KnownList
{
    protected readonly VisibleObject Owner;
    protected readonly System.Collections.Concurrent.ConcurrentDictionary<int, KnownObject> KnownObjects = new();

    public KnownList(VisibleObject owner)
    {
        Owner = owner;
    }

    // Java parity: update() (virtual — overridden by NpcKnownList)
    public virtual void Update()
    {
        lock (this)
        {
            ForgetObjectsOrUpdateVisibility();
            FindVisibleObjects();
        }
    }

    // Java parity: clear(ObjectDeleteAnimation)
    public void Clear(ObjectDeleteAnimation animation)
    {
        lock (this)
        {
            foreach (KnownObject obj in KnownObjects.Values)
            {
                Del(obj.Get(), ObjectDeleteAnimation.None);
                obj.Get().GetKnownList().Del(Owner, animation);
            }
        }
    }

    // Java parity: knows(VisibleObject)
    public bool Knows(VisibleObject obj) => KnownObjects.ContainsKey(obj.ObjectId);

    // Java parity: sees(VisibleObject)
    public bool Sees(VisibleObject obj) =>
        KnownObjects.TryGetValue(obj.ObjectId, out var ko) && ko.IsVisible();

    // Java parity: add(VisibleObject) — protected in Java (package+subclass access); C# protected internal to allow same-assembly subclass cross-instance call.
    protected internal bool Add(VisibleObject obj)
    {
        if (!IsAwareOf(obj))
            return false;
        var knownObject = new KnownObject(obj);
        if (!KnownObjects.TryAdd(obj.ObjectId, knownObject))
            return false;
        UpdateVisibility(knownObject);
        return true;
    }

    // Java parity: updateVisibleObject(VisibleObject)
    public void UpdateVisibleObject(VisibleObject obj)
    {
        if (KnownObjects.TryGetValue(obj.ObjectId, out var knownObject))
            UpdateVisibility(knownObject);
    }

    private void UpdateVisibility(KnownObject knownObject)
    {
        bool visible = Owner.CanSee(knownObject.Get());
        if (!knownObject.UpdateVisible(visible))
            return;
        if (visible)
            NotifySee(knownObject.Get());
        else
            NotifyNotSee(knownObject.Get(), ObjectDeleteAnimation.FadeOut);
        UpdatePetVisibility(knownObject); // pet spawn packet must follow SM_PLAYER_INFO
    }

    private void UpdatePetVisibility(KnownObject knownObject)
    {
        if (knownObject.Get() is Player player && player.GetPet() is Pet pet)
        {
            if (KnownObjects.TryGetValue(pet.ObjectId, out var petKnownObject))
                UpdateVisibility(petKnownObject);
        }
    }

    // Java parity: del(VisibleObject, ObjectDeleteAnimation) — private
    private void Del(VisibleObject obj, ObjectDeleteAnimation animation)
    {
        if (KnownObjects.TryRemove(obj.ObjectId, out var knownObject))
        {
            if (knownObject.UpdateVisible(false))
                NotifyNotSee(obj, animation);
            NotifyNotKnow(obj);
        }
    }

    private void NotifySee(VisibleObject obj)
    {
        try { Owner.GetController().See(obj); }
        catch (Exception e) { Log(e); }
    }

    private void NotifyNotSee(VisibleObject obj, ObjectDeleteAnimation animation)
    {
        try { Owner.GetController().NotSee(obj, animation); }
        catch (Exception e) { Log(e); }
    }

    private void NotifyNotKnow(VisibleObject obj)
    {
        try { Owner.GetController().NotKnow(obj); }
        catch (Exception e) { Log(e); }
    }

    private static void Log(Exception e) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance.LogError(e, "");

    // Java parity: forgetObjectsOrUpdateVisibility() — private
    private void ForgetObjectsOrUpdateVisibility()
    {
        foreach (KnownObject obj in KnownObjects.Values)
        {
            if (IsInRange(obj.Get()))
            {
                UpdateVisibility(obj);
            }
            else
            {
                Del(obj.Get(), ObjectDeleteAnimation.None);
                obj.Get().GetKnownList().Del(Owner, ObjectDeleteAnimation.None);
            }
        }
    }

    // Java parity: findVisibleObjects()
    protected virtual void FindVisibleObjects()
    {
        if (!Owner.IsSpawned())
            return;

        WorldPosition position = Owner.GetPosition();
        if (Owner is Player)
        {
            position.GetWorldMapInstance().ForEachNpc(npc =>
            {
                if (npc.IsFlag() && npc.GetKnownList().Add(Owner))
                    Add(npc);
            });
        }
        foreach (MapRegion region in position.GetMapRegion()!.GetNeighbours())
        {
            foreach (VisibleObject newObject in region.GetObjects().Values)
            {
                if (!IsAwareOf(newObject))
                    continue;
                if (Knows(newObject))
                    continue;
                if (!IsInRange(newObject))
                    continue;
                if (newObject.GetKnownList().Add(Owner))
                    Add(newObject);
            }
        }
    }

    // Java parity: isAwareOf(VisibleObject)
    protected virtual bool IsAwareOf(VisibleObject newObject) => newObject != null && !newObject.Equals(Owner);

    // Java parity: getVisibleDistance()
    protected virtual float GetVisibleDistance() => Owner.GetVisibleDistance();

    private bool IsInRange(VisibleObject newObject)
    {
        // two-way relation requires the max valid distance for both objects to avoid flickering
        float distance = Math.Max(GetVisibleDistance(), newObject.GetKnownList().GetVisibleDistance());
        return PositionUtil.IsInRange(Owner, newObject, distance);
    }

    // Java parity: findObject(Predicate)
    public VisibleObject? FindObject(Predicate<KnownObject> predicate)
    {
        foreach (KnownObject value in KnownObjects.Values)
        {
            if (predicate(value))
                return value.Get();
        }
        return null;
    }

    // Java parity: getObject(int)
    public VisibleObject? GetObject(int targetObjectId) =>
        KnownObjects.TryGetValue(targetObjectId, out var ko) ? ko.Get() : null;

    // Java parity: getPlayer(int)
    public Player? GetPlayer(int targetObjectId) =>
        KnownObjects.TryGetValue(targetObjectId, out var ko) && ko.Get() is Player player ? player : null;

    // Java parity: forEach(Consumer)
    public void ForEach(Action<KnownObject> action) =>
        CollectionUtil.ForEach(KnownObjects.Values, action, () => "KnownList owner: " + Owner);

    // Java parity: forEachObject(Consumer)
    public void ForEachObject(Action<VisibleObject> consumer) => ForEach(obj => consumer(obj.Get()));

    // Java parity: forEachNpc(Consumer)
    public void ForEachNpc(Action<Npc> consumer) => ForEach(o =>
    {
        if (o.Get() is Npc npc)
            consumer(npc);
    });

    // Java parity: forEachPlayer(Consumer)
    public void ForEachPlayer(Action<Player> consumer) => ForEach(obj =>
    {
        if (obj.Get() is Player player)
            consumer(player);
    });

    // Java parity: stream()
    public IEnumerable<KnownObject> Stream() => KnownObjects.Values;

    // Java parity: streamPlayers()
    public IEnumerable<Player> StreamPlayers() =>
        Stream().Where(o => o.Get() is Player).Select(o => (Player)o.Get());

    // Java parity: streamVisiblePlayers()
    public IEnumerable<Player> StreamVisiblePlayers() =>
        Stream().Where(o => o.IsVisible() && o.Get() is Player).Select(o => (Player)o.Get());
}
