using Aion.GameServer.Utils;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Java parity: model/gameobjects/UseableHouseObject.
/// C# generic-invariance break: NON-GENERIC base for the Java wildcard <c>UseableHouseObject&lt;?&gt;</c>;
/// all members are T-independent. The typed template accessor lives on <see cref="UseableHouseObject{T}"/>.
/// </summary>
public abstract class UseableHouseObject : HouseObject
{
    private readonly AtomicInteger usingPlayer = new AtomicInteger();

    protected UseableHouseObject(HouseRegistry registry, int objId, int templateId, Aion.GameServer.Controllers.PlaceableObjectController controller, bool autoReleaseObjectId)
        : base(registry, objId, templateId, controller, autoReleaseObjectId)
    {
    }

    public override bool CanExpireNow()
    {
        return !IsOccupied();
    }

    public bool IsOccupied()
    {
        return usingPlayer.Get() != 0;
    }

    public bool SetOccupant(Player player)
    {
        return usingPlayer.CompareAndSet(0, player.GetObjectId()) || usingPlayer.Get() == player.GetObjectId();
    }

    public bool ReleaseOccupant(Player player)
    {
        return usingPlayer.CompareAndSet(player.GetObjectId(), 0);
    }

    protected void ReleaseOccupant()
    {
        usingPlayer.Set(0);
    }

    public virtual bool HasUseCooldown()
    {
        return false;
    }
}

/// <summary>
/// Java parity: <c>UseableHouseObject&lt;T extends PlaceableHouseObject&gt;</c>. Adds only the strongly-typed
/// template accessor over the non-generic <see cref="UseableHouseObject"/> base.
/// </summary>
public abstract class UseableHouseObject<T> : UseableHouseObject where T : PlaceableHouseObject
{
    protected UseableHouseObject(HouseRegistry registry, int objId, int templateId)
        : base(registry, objId, templateId, new Aion.GameServer.Controllers.PlaceableObjectController(), false)
    {
    }

    public new T GetObjectTemplate()
    {
        return (T) base.GetObjectTemplate();
    }
}
