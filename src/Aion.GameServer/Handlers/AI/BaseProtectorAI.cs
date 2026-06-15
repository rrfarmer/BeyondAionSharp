using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("base_protector")]
public class BaseProtectorAI : AggressiveNpcAI
{
    public BaseProtectorAI(Npc owner) : base(owner)
    {
    }

    protected new BaseSpawnTemplate GetSpawnTemplate()
    {
        return (BaseSpawnTemplate)base.GetSpawnTemplate();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        Base @base = BaseService.GetInstance().GetActiveBase(GetSpawnTemplate().GetId());
        if (@base == null)
            return;
        DamageInfo<AionObject> mostDamage = GetAggroList().GetFinalDamageList().ToTeamDamages().GetMostDamage();
        Creature bossKiller = mostDamage.GetAttacker() is TemporaryPlayerTeam team ? team.GetLeaderObject() : (Creature)mostDamage.GetAttacker();
        BaseOccupier newOccupier = @base.GetOccupier(bossKiller);
        BaseService.GetInstance().Capture(@base.GetId(), newOccupier);
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP && GetOwner().GetLevel() >= 65) // Avoid adjusting low-level zones
            stat.SetBaseRate(SiegeConfig.BASE_PROTECTOR_HEALTH_MULTIPLIER);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_LOOT or AIQuestion.REMOVE_EFFECTS_ON_MAP_REGION_DEACTIVATE => false,
            _ => base.Ask(question),
        };
    }
}
