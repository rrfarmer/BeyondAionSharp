using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/DancingFlameAI (@author xTz, Estrayl).</summary>
[AIName("dancing_flame")]
public class DancingFlameAI : GeneralNpcAI
{
    private ScheduledTask buffTask;

    public DancingFlameAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    private void StartBuffTask()
    {
        buffTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (GetKnownList().StreamPlayers().Any(p => IsInRange(p, 10)))
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), GetNpcId() == 282998 ? 20536 : 20535, 60, GetOwner()).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(10000), TimeSpan.FromMilliseconds(9000));
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        StartBuffTask();
    }

    protected override void HandleDespawned()
    {
        buffTask.Cancel(true);
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.ALLOW_DECAY:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
