using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/AggressiveNoLootNpcAI (Estrayl).</summary>
[AIName("aggressive_no_loot")]
public class AggressiveNoLootNpcAI : AggressiveNpcAI
{
    public AggressiveNoLootNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.ALLOW_DECAY => false,
            _ => base.Ask(question),
        };
    }
}
