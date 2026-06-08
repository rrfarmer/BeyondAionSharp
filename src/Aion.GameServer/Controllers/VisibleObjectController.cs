using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Controllers;

/// <summary>
/// Controls a VisibleObject (movement, visibility, lifecycle).
/// Java parity: controllers/VisibleObjectController&lt;T extends VisibleObject&gt;.
/// </summary>
/// <remarks>
/// Java is a single generic class with a <c>T owner</c>. C# has no wildcard types, so a VisibleObject
/// stores this non-generic base (it can own any <c>VisibleObjectController&lt;subtype&gt;</c>); the typed
/// owner accessor lives on the generic subclass <see cref="VisibleObjectController{T}"/>.
/// </remarks>
public abstract class VisibleObjectController
{
    private VisibleObject _owner = null!;

    // Java parity: getOwner() (untyped accessor for the base)
    protected internal VisibleObject GetOwnerObject() => _owner;
    internal void SetOwnerObject(VisibleObject owner) => _owner = owner;

    // Java parity: see(VisibleObject)
    public virtual void See(VisibleObject obj) { }

    // Java parity: notSee(VisibleObject, ObjectDeleteAnimation)
    public virtual void NotSee(VisibleObject obj, ObjectDeleteAnimation animation) { }

    // Java parity: notKnow(VisibleObject)
    public virtual void NotKnow(VisibleObject obj) { }

    // Java parity: delete() — despawns (if spawned) and removes from world.
    public bool Delete() => World.World.GetInstance().RemoveObject(GetOwnerObject());

    // Java parity: deleteAndScheduleRespawn()
    public void DeleteAndScheduleRespawn()
    {
        if (Delete() && !RespawnService.HasRespawnTask(GetOwnerObject()))
            RespawnService.ScheduleRespawn(GetOwnerObject());
    }

    // Java parity: deleteIfAliveOrCancelRespawn()
    public void DeleteIfAliveOrCancelRespawn()
    {
        bool isDead = GetOwnerObject() is Creature creature && creature.IsDead();
        if (isDead || !Delete())
            RespawnService.CancelRespawn(GetOwnerObject());
    }

    // Java parity: onTargetChanged(VisibleObject, VisibleObject)
    public virtual void OnTargetChanged(VisibleObject oldTarget, VisibleObject newTarget) { }

    // Java parity: onBeforeSpawn()
    public virtual void OnBeforeSpawn()
    {
        if (GetOwnerObject().GetSpawn() != null && GetOwnerObject().GetSpawn()!.GetStaticId() > 0)
            GeoService.GetInstance().SpawnPlaceableObject(GetOwnerObject().GetWorldId(), GetOwnerObject().GetInstanceId(), GetOwnerObject().GetSpawn()!.GetStaticId());
    }

    // Java parity: onAfterSpawn()
    public virtual void OnAfterSpawn() { }

    // Java parity: onDespawn()
    public virtual void OnDespawn()
    {
        if (GetOwnerObject().GetSpawn() != null && GetOwnerObject().GetSpawn()!.GetStaticId() > 0)
            GeoService.GetInstance().DespawnPlaceableObject(GetOwnerObject().GetWorldId(), GetOwnerObject().GetInstanceId(), GetOwnerObject().GetSpawn()!.GetStaticId());
    }

    // Java parity: onDelete()
    public virtual void OnDelete() { }
}

/// <summary>
/// Java parity: the generic <c>VisibleObjectController&lt;T&gt;</c> typed-owner surface.
/// </summary>
public abstract class VisibleObjectController<T> : VisibleObjectController where T : VisibleObject
{
    // Java parity: setOwner(T)
    public void SetOwner(T owner) => SetOwnerObject(owner);

    // Java parity: final getOwner():T
    public T GetOwner() => (T)GetOwnerObject();
}
