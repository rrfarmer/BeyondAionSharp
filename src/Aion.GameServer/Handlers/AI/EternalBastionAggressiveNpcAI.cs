using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionAggressiveNpcAI (Estrayl).</summary>
[AIName("eternal_bastion_aggressive")]
public class EternalBastionAggressiveNpcAI : AggressiveNpcAI
{
    public EternalBastionAggressiveNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        if (creature is Npc n && n.GetNpcId() == 284685) // Work-around to not aggro the shatter mine
            return;
        base.HandleCreatureAggro(creature);
    }
}
