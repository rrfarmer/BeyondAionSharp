using System.Collections.Concurrent;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/FlyRingController (xavier).</summary>
public class FlyRingController : VisibleObjectController<FlyRing>
{
    private readonly ConcurrentDictionary<int, FlyRingObserver> observed = new ConcurrentDictionary<int, FlyRingObserver>();

    public override void See(VisibleObject @object)
    {
        if (@object is Player)
        {
            Player p = (Player) @object;
            FlyRingObserver observer = new FlyRingObserver(GetOwner(), p);
            p.GetObserveController().AddObserver(observer);
            observed[p.GetObjectId()] = observer;
        }
    }

    public override void NotSee(VisibleObject @object, ObjectDeleteAnimation animation)
    {
        if (@object is Player)
        {
            Player p = (Player) @object;
            observed.TryRemove(p.GetObjectId(), out FlyRingObserver observer);
            p.GetObserveController().RemoveObserver(observer);
        }
    }
}
