using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/raksang/RaksangGargoyleAI (xTz).</summary>
[AIName("raksang_gargoyle")]
public class RaksangGargoyleAI : AggressiveNpcAI
{
    public RaksangGargoyleAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19126, 46, GetOwner()).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, 2000L);
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
