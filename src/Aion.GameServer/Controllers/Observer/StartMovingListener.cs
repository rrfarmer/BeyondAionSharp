namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Java parity: controllers/observer/StartMovingListener (ATracer).
/// </summary>
public class StartMovingListener : ActionObserver
{
    private bool _effectorMoved;

    public StartMovingListener()
        : base(ObserverType.MOVE)
    {
    }

    public bool IsEffectorMoved()
    {
        return _effectorMoved;
    }

    public override void Moved()
    {
        _effectorMoved = true;
    }
}
