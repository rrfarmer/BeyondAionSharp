using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/ophidianBridge/DefenseWallAI (Tibald).</summary>
[AIName("defense_wall")]
public class DefenseWallAI : GeneralNpcAI
{
    public DefenseWallAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
