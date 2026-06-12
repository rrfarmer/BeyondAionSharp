using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Java parity: controllers/observer/DialogObserver (nrg).
/// </summary>
public abstract class DialogObserver : ActionObserver
{
    protected readonly Player Responder;
    protected readonly Creature Requester;
    private readonly int _maxDistance;

    protected DialogObserver(Creature requester, Player responder, int maxDistance)
        : base(ObserverType.MOVE)
    {
        Responder = responder;
        Requester = requester;
        _maxDistance = maxDistance;
    }

    public override void Moved()
    {
        if (!PositionUtil.IsInRange(Responder, Requester, _maxDistance))
            TooFar();
    }

    /// <summary>
    /// Is called when player is too far away from dialog serving object
    /// </summary>
    public abstract void TooFar();
}
