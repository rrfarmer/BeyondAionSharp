using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/raksang/RaksangRubbleAI (@author xTz).</summary>
[AIName("raksang_rubble")]
public class RaksangRubbleAI : AggressiveNpcAI
{
    public RaksangRubbleAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19937, 46, GetOwner()).UseNoAnimationSkill();
            StartLifeTask();
            return ValueTask.CompletedTask;
        }, 1000L);
    }

    private void StartLifeTask()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, 9000L);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        AIActions.DeleteOwner(this);
    }
}
