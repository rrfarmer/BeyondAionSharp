using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;

namespace Aion.GameServer.Controllers.Attack;

/// <summary>
/// List of combined creature damages, grouped by the team they belong to (Java parity: controllers/attack/TeamDamageList).
/// TemporaryPlayerTeam&lt;?&gt;→TemporaryPlayerTeam&lt;TeamMember&lt;Player&gt;&gt; (codebase bound, matches GetCurrentTeam + PlayerTeamDistributionService caller);
/// Map.compute (always non-null)→TryGetValue+assign; computeIfAbsent(DamageInfo::new)→TryGetValue+ctor; stream max/sum→MaxBy/Sum.
/// DamageInfo&lt;T&gt;/DamageList red-tolerated.
/// </summary>
public class TeamDamageList
{
    private readonly Dictionary<AionObject, DamageInfo<AionObject>> damageByCreatureOrTeam = new();
    private readonly Dictionary<TemporaryPlayerTeam<TeamMember<Player>>, DamageInfo<Player>> mostDamageByTeam = new();

    internal TeamDamageList(DamageList damageList)
    {
        foreach (DamageInfo<Creature> damageInfo in damageList.GetCreatureDamages())
        {
            AionObject creatureOrTeam = damageInfo.GetAttacker();
            TemporaryPlayerTeam<TeamMember<Player>> team = creatureOrTeam is Player player ? player.GetCurrentTeam() : null;
            if (team != null)
            {
                creatureOrTeam = team;
                DamageInfo<Player> memberDamage = (DamageInfo<Player>)(object)damageInfo;
                mostDamageByTeam.TryGetValue(team, out DamageInfo<Player> other);
                mostDamageByTeam[team] = other == null || memberDamage.GetDamage() > other.GetDamage() ? memberDamage : other;
            }
            if (!damageByCreatureOrTeam.TryGetValue(creatureOrTeam, out DamageInfo<AionObject> di))
            {
                di = new DamageInfo<AionObject>(creatureOrTeam);
                damageByCreatureOrTeam[creatureOrTeam] = di;
            }
            di.AddDamage(damageInfo.GetDamage());
        }
    }

    public ICollection<DamageInfo<AionObject>> GetCreatureOrTeamDamages()
    {
        return damageByCreatureOrTeam.Values;
    }

    public DamageInfo<AionObject> GetMostDamage()
    {
        return damageByCreatureOrTeam.Values.MaxBy(d => d.GetDamage());
    }

    public DamageInfo<Player> GetMostDamageByTeam(TemporaryPlayerTeam<TeamMember<Player>> team)
    {
        return mostDamageByTeam.GetValueOrDefault(team);
    }

    public int GetTotalDamage()
    {
        return damageByCreatureOrTeam.Values.Sum(d => d.GetDamage());
    }
}
