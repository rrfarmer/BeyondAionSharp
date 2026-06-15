using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// TODO: Aether Concentrators - currently not necessary since they are nearly impossible to use and need
/// about 100 player to be activated by default.
/// Java parity: ai/siege/EnragedAgent (@author Estrayl).
/// </summary>
[AIName("enraged_agent")]
public class EnragedAgent : SummonerAI
{
    public EnragedAgent(Npc owner)
        : base(owner)
    {
    }

    public override void OnEffectEnd(Effect effect)
    {
        switch (effect.GetSkillId())
        {
            case 18704:
                ThreadPoolManager.GetInstance()
                    .Schedule(_ => { SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18705, 60, GetAggroList().GetTarget(AggroTarget.MOST_HATED)).UseSkill(); return System.Threading.Tasks.ValueTask.CompletedTask; }, 650L);
                break;
        }
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP)
            stat.SetBaseRate(SiegeConfig.FORTRESS_PROTECTOR_HEALTH_MULTIPLIER);
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        GetAggroList().Clear(); // make sure old damages aren't counted in stopSiege
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        AbstractSiegeProtectorAI.StopSiege((SiegeNpc)GetOwner());
    }
}
