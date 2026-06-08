using Aion.GameServer.Controllers.Attack;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Java parity: controllers/observer/AttackerCriticalStatusObserver (kecimis).
/// </summary>
public class AttackerCriticalStatusObserver : AttackCalcObserver
{
    protected AttackerCriticalStatus? AcStatus;
    protected AttackStatus Status;

    public AttackerCriticalStatusObserver(AttackStatus status, int count, int value, bool isPercent)
    {
        Status = status;
        AcStatus = new AttackerCriticalStatus(count, value, isPercent);
    }

    public int GetCount()
    {
        return AcStatus!.GetCount();
    }

    public void DecreaseCount()
    {
        AcStatus!.SetCount(AcStatus.GetCount() - 1);
    }
}
