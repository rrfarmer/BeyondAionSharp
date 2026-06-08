using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Controllers.Attack;

/// <summary>
/// List of combined creature damages, grouped by their master (if present, like with Summons and summoned objects).
/// Java parity: controllers/attack/DamageList.
/// </summary>
public class DamageList
{
    private readonly Dictionary<Creature, DamageInfo<Creature>> _damageByCreature = new();

    // Java parity: package-private DamageList(Collection<AggroInfo> aggroInfos, Creature owner)
    internal DamageList(IEnumerable<AggroInfo> aggroInfos, Creature owner)
    {
        foreach (AggroInfo aggroInfo in aggroInfos)
        {
            if (aggroInfo.GetDamage() <= 0)
                continue;
            Creature attackerMaster = aggroInfo.GetAttacker().GetMaster();
            // Don't include damage from creatures outside the known list.
            if (!owner.GetKnownList().Knows(attackerMaster))
                continue;
            if (!_damageByCreature.TryGetValue(attackerMaster, out DamageInfo<Creature>? damageInfo))
            {
                damageInfo = new DamageInfo<Creature>(attackerMaster);
                _damageByCreature[attackerMaster] = damageInfo;
            }
            damageInfo.AddDamage(aggroInfo.GetDamage());
        }
    }

    // Java parity: toTeamDamages()
    public TeamDamageList ToTeamDamages() => new(this);

    // Java parity: getCreatureDamages()
    public ICollection<DamageInfo<Creature>> GetCreatureDamages() => _damageByCreature.Values;

    // Java parity: getMostDamage()
    public DamageInfo<Creature>? GetMostDamage() =>
        _damageByCreature.Values.MaxBy(d => d.GetDamage());

    // Java parity: getTotalDamage()
    public int GetTotalDamage() => _damageByCreature.Values.Sum(d => d.GetDamage());
}
