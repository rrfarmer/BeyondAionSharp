using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Controllers.Attack;

/// <summary>
/// Accumulated damage from one attacker.
/// Java parity: controllers/attack/DamageInfo&lt;T extends AionObject&gt;.
/// </summary>
public class DamageInfo<T> where T : AionObject
{
    private readonly T _attacker;
    private int _damage;

    public DamageInfo(T attacker)
    {
        _attacker = attacker;
    }

    // Java parity: getAttacker()
    public T GetAttacker() => _attacker;

    // Java parity: getDamage()
    public int GetDamage() => _damage;

    // Java parity: package-private addDamage(int)
    internal void AddDamage(int damage) => _damage += damage;
}
