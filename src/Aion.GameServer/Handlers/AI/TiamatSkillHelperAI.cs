using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/TiamatSkillHelperAI (@author Estrayl March 8th, 2018).</summary>
[AIName("tiamat_skill_helper")]
public class TiamatSkillHelperAI : NpcAI
{
    public TiamatSkillHelperAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        HandleSkillTask();
    }

    private void HandleSkillTask()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            WorldPosition p = GetPosition();
            Spawn(GetNpcId() + 1, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
            ThreadPoolManager.GetInstance().Schedule(() => AIActions.Die(this), 3000L);
        }, 1500L);
    }

    protected override void HandleDied()
    {
        RespawnService.ScheduleDecayTask(GetOwner(), 3000);
        base.HandleDied();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
