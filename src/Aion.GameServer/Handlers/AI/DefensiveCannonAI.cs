using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/DefensiveCannonAI (xTz, Neon).</summary>
[AIName("defensive_cannon")]
public class DefensiveCannonAI : ActionItemNpcAI
{
    private readonly AtomicBoolean canUse = new AtomicBoolean(true);

    public DefensiveCannonAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (canUse.CompareAndSet(true, false))
        {
            switch (GetNpcId())
            {
                case 831338:
                case 831339:
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20364, 60, player).UseNoAnimationSkill(); // Board Artillery Morph
                    break;
            }
            AIActions.DeleteOwner(this);
        }
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
