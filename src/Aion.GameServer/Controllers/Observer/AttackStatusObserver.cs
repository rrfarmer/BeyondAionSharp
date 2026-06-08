using Aion.GameServer.Controllers.Attack;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Java parity: controllers/observer/AttackStatusObserver (ATracer).
/// </summary>
public class AttackStatusObserver : AttackCalcObserver
{
    protected int Value;
    protected AttackStatus Status;

    public AttackStatusObserver(int value, AttackStatus status)
    {
        Value = value;
        Status = status;
    }
}
