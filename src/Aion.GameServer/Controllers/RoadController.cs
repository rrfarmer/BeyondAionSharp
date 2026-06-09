using System.Collections.Concurrent;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Road;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/RoadController (SheppeR).</summary>
public class RoadController : VisibleObjectController<Road>
{
    private readonly ConcurrentDictionary<int, RoadObserver> observed = new ConcurrentDictionary<int, RoadObserver>();

    public override void See(VisibleObject @object)
    {
        if (@object is Player)
        {
            Player p = (Player) @object;
            RoadObserver observer = new RoadObserver(GetOwner(), p);
            p.GetObserveController().AddObserver(observer);
            observed[p.GetObjectId()] = observer;
        }
    }

    public override void NotSee(VisibleObject @object, ObjectDeleteAnimation animation)
    {
        if (@object is Player)
        {
            Player p = (Player) @object;
            observed.TryRemove(p.GetObjectId(), out RoadObserver observer);
            p.GetObserveController().RemoveObserver(observer);
        }
    }
}
