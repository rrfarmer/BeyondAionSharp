using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Services.Siege;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/DredgionCommanderAI (@author Luzien, Estrayl).</summary>
[AIName("dredgion_commander")]
public class DredgionCommanderAI : SiegeNpcAI
{
    private Npc fortressBoss;

    public DredgionCommanderAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ => { FindFortressBoss(); return ValueTask.CompletedTask; }, 3000L);
    }

    private void FindFortressBoss()
    {
        fortressBoss = GetKnownList().Stream()
            .Where(knownObject => knownObject.Get() is Npc npc && (npc.GetRace() == Race.GCHIEF_LIGHT || npc.GetRace() == Race.GCHIEF_DARK))
            .Select(knownObject => (Npc)knownObject.Get())
            .FirstOrDefault();
        if (fortressBoss != null)
            GetAggroList().AddHate(fortressBoss, 500000);
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetDistanceToSpawnLocation() >= 25.0d && fortressBoss != null)
        {
            GetAggroList().AddHate(fortressBoss, 1000000);
            AIActions.TargetCreature(this, fortressBoss);
            GetOwner().GetMoveController().MoveToPoint(fortressBoss.GetX(), fortressBoss.GetY(), fortressBoss.GetZ());
        }
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        if (effected == fortressBoss)
            damage *= SiegeConfig.FORTRESS_PROTECTOR_HEALTH_MULTIPLIER;
        return damage;
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP)
            stat.SetBaseRate(SiegeConfig.FORTRESS_PROTECTOR_HEALTH_MULTIPLIER);
    }

    protected override void HandleDespawned()
    {
        fortressBoss = null;
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        FortressAssault assault = BalaurAssaultService.GetInstance().GetFortressAssaultBySiegeId(((SiegeNpc)GetOwner()).GetSiegeId());
        if (assault != null)
            assault.OnDredgionCommanderKilled();

        fortressBoss = null;
        base.HandleDied();
    }
}
