using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/UseableHouseObject.</summary>
public abstract class UseableHouseObject<T> : HouseObject<T> where T : PlaceableHouseObject
{
    private readonly AtomicInteger usingPlayer = new AtomicInteger();

    public UseableHouseObject(HouseRegistry registry, int objId, int templateId)
        : base(registry, objId, templateId)
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

    public bool HasUseCooldown()
    {
        return false;
    }
}
