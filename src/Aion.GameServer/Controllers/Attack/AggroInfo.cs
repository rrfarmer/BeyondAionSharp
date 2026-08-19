using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Controllers.Attack;

/// <summary>
/// Per-attacker aggro entry: hate + damage tracking.
/// Java parity: controllers/attack/AggroInfo.
/// </summary>
public class AggroInfo
{
    private const int HATE_REDUCE_VALUE = 364; // most retail npcs lose 364 hate. TODO: find formula
    private readonly Creature _attacker;
    private int _hate;
    private int _damage;
    private long _lastInteractionTime;
    private int _hateReduceCount = 1;

    // Java parity: package-private AggroInfo(Creature)
    internal AggroInfo(Creature attacker)
    {
        _attacker = attacker;
    }

    // Java parity: getAttacker()
    public Creature GetAttacker() => _attacker;

    // Java parity: addDamage(int)
    public void AddDamage(int damage)
    {
        if (damage > 0)
            _damage += damage;
    }

    // Java parity: addHate(int)
    public void AddHate(int hate)
    {
        // Java parity except for the overflow, which Java shares and never exercises. Retail's
        // switch_target carries points_to_add=2147483647 to mean "top of the list, permanently"; adding
        // that to any existing hate wraps negative, and the clamp below then pins it to 1 -- turning the
        // strongest taunt in the game into the weakest. Widening to long and saturating keeps every
        // ordinary value identical and makes the extreme one mean what it says.
        long sum = (long)_hate + hate;
        _hate = sum > int.MaxValue ? int.MaxValue : (int)sum;
        if (_hate < 1)
            _hate = 1;
        _lastInteractionTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _hateReduceCount = 1;
    }

    // Java parity: getHate()
    public int GetHate() => _hate;

    // Java parity: setHate(int)
    public void SetHate(int hate) => _hate = hate;

    // Java parity: getDamage()
    public int GetDamage() => _damage;

    // Java parity: getLastInteractionTime()
    public long GetLastInteractionTime() => _lastInteractionTime;

    // Java parity: package-private reduceHate()
    internal void ReduceHate()
    {
        if (_hate > 1)
        {
            _hate -= HATE_REDUCE_VALUE * _hateReduceCount;
            _hateReduceCount++;
            if (_hate < 1)
                _hate = 1;
        }
    }
}
