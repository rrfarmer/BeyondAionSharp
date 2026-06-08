namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Result of an attacker's critical-related observer check.
/// Java parity: controllers/observer/AttackerCriticalStatus.
/// </summary>
public class AttackerCriticalStatus
{
    private bool _result;
    private int _count;
    private readonly int _value;
    private readonly bool _isPercent;

    public AttackerCriticalStatus(bool result)
    {
        _result = result;
    }

    public AttackerCriticalStatus(int count, int value, bool isPercent)
    {
        _count = count;
        _value = value;
        _isPercent = isPercent;
    }

    public int GetCount() => _count;
    public void SetCount(int count) => _count = count;
    public int GetValue() => _value;
    public bool IsPercent() => _isPercent;
    public bool IsResult() => _result;
    public void SetResult(bool result) => _result = result;
}
