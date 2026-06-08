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
        _hate += hate;
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
